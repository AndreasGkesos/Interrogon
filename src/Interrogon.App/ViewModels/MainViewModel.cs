using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Interrogon.Core;
using Interrogon.Core.Interfaces;
using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public enum SidebarView
{
    AllQuestions,
    Favorites,
    RecentlyViewed
}

public enum DrillLevel
{
    Types,
    Categories,
    Questions
}

public partial class MainViewModel : ObservableObject
{
    private readonly IQuestionRepository _questionRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITagRepository _tagRepository;

    private List<Question> _allQuestions = [];
    private List<Category> _allCategories = [];
    private List<Tag> _allTags = [];

    /// <summary>
    /// The drill level the user was actually at (via tile clicks) before a tag selection
    /// forced a jump to the question list. Null if the current DrillLevel reflects a real
    /// tile drill-down rather than a tag-driven shortcut. Used so deselecting the last tag
    /// returns to that real level instead of always bouncing back to the root tiles.
    /// </summary>
    private DrillLevel? _drillLevelBeforeTagJump;

    [ObservableProperty]
    private SidebarView _currentSidebarView = SidebarView.AllQuestions;

    [ObservableProperty]
    private DrillLevel _drillLevel = DrillLevel.Types;

    [ObservableProperty]
    private QuestionType? _selectedType;

    [ObservableProperty]
    private Guid? _selectedCategoryId;

    [ObservableProperty]
    private string _selectedCategoryName = string.Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _tagSearchText = string.Empty;

    [ObservableProperty]
    private Question? _selectedQuestion;

    [ObservableProperty]
    private bool _isNotesOpen;

    [ObservableProperty]
    private bool _isEditFormOpen;

    [ObservableProperty]
    private QuestionEditViewModel? _editingQuestion;

    public ObservableCollection<TypeTileViewModel> TypeTiles { get; } = [];
    public ObservableCollection<CategoryTileViewModel> CategoryTiles { get; } = [];
    public ObservableCollection<QuestionListItemViewModel> QuestionList { get; } = [];
    public ObservableCollection<TagChoiceViewModel> TagFacetChoices { get; } = [];

    /// <summary>
    /// The subset of <see cref="TagFacetChoices"/> whose name matches <see cref="TagSearchText"/>; this is
    /// what the sidebar displays. Display-only: question filtering always uses the full TagFacetChoices.
    /// </summary>
    public ObservableCollection<TagChoiceViewModel> VisibleTagFacetChoices { get; } = [];

    public bool ShowTypeTiles => DrillLevel == DrillLevel.Types && CurrentSidebarView == SidebarView.AllQuestions;
    public bool ShowCategoryTiles => DrillLevel == DrillLevel.Categories && CurrentSidebarView == SidebarView.AllQuestions;
    public bool ShowQuestionList => DrillLevel == DrillLevel.Questions || CurrentSidebarView != SidebarView.AllQuestions;
    public bool ShowBreadcrumb => CurrentSidebarView == SidebarView.AllQuestions && DrillLevel != DrillLevel.Types && SelectedQuestion is null;

    public string BreadcrumbText => SelectedType is { } type
        ? (SelectedCategoryId is null
            ? $"All Questions > {QuestionTypeDisplay.GetLabel(type)}"
            : $"All Questions > {QuestionTypeDisplay.GetLabel(type)} > {SelectedCategoryName}")
        : "All Questions";

    public MainViewModel(IQuestionRepository questionRepository, ICategoryRepository categoryRepository, ITagRepository tagRepository)
    {
        _questionRepository = questionRepository;
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
    }

    public async Task InitializeAsync()
    {
        await ReloadAllAsync();
    }

    private async Task ReloadAllAsync()
    {
        _allQuestions = (await _questionRepository.GetAllAsync()).ToList();
        _allCategories = (await _categoryRepository.GetAllAsync()).ToList();
        _allTags = (await _tagRepository.GetAllAsync()).ToList();

        RebuildTypeTiles();
        RebuildTagFacetChoices();
        RefreshCurrentView();
    }

    private void RebuildTypeTiles()
    {
        TypeTiles.Clear();
        foreach (var type in QuestionTypeDisplay.All)
        {
            var count = _allQuestions.Count(q => q.Type == type);
            TypeTiles.Add(new TypeTileViewModel(type, count));
        }
    }

    private void RebuildTagFacetChoices() => RebuildTagFacetChoices(_allQuestions);

    private void RebuildTagFacetChoices(IEnumerable<Question> scope)
    {
        var previouslySelected = TagFacetChoices.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        TagFacetChoices.Clear();

        var usageCounts = new Dictionary<Guid, int>();
        foreach (var question in scope)
        {
            foreach (var tagId in question.TagIds)
            {
                usageCounts[tagId] = usageCounts.GetValueOrDefault(tagId) + 1;
            }
        }

        var orderedTags = _allTags
            .Where(t => usageCounts.ContainsKey(t.Id))
            .OrderByDescending(t => usageCounts[t.Id])
            .ThenBy(t => t.Name);

        foreach (var tag in orderedTags)
        {
            var choice = new TagChoiceViewModel(tag.Id, tag.Name, usageCounts[tag.Id]) { IsSelected = previouslySelected.Contains(tag.Id) };
            choice.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(TagChoiceViewModel.IsSelected))
                {
                    OnTagFacetSelectionChanged();
                }
            };
            TagFacetChoices.Add(choice);
        }

        RefreshVisibleTagChoices();
    }

    partial void OnTagSearchTextChanged(string value) => RefreshVisibleTagChoices();

    private void RefreshVisibleTagChoices()
    {
        var needle = TagSearchText.Trim();

        var wanted = TagFacetChoices
            .Where(c => needle.Length == 0 || c.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var wantedSet = wanted.ToHashSet();

        // Update in place rather than Clear + re-Add: each change to the collection costs the UI
        // layout work, so only touching what actually changed keeps typing responsive.
        for (var i = VisibleTagFacetChoices.Count - 1; i >= 0; i--)
        {
            if (!wantedSet.Contains(VisibleTagFacetChoices[i]))
            {
                VisibleTagFacetChoices.RemoveAt(i);
            }
        }

        for (var i = 0; i < wanted.Count; i++)
        {
            if (i >= VisibleTagFacetChoices.Count || !ReferenceEquals(VisibleTagFacetChoices[i], wanted[i]))
            {
                VisibleTagFacetChoices.Insert(i, wanted[i]);
            }
        }
    }

    private void OnTagFacetSelectionChanged()
    {
        // Changing the tag filter while a question is open is a clear signal to go back
        // to the (now re-filtered) list rather than keep looking at the open question.
        if (SelectedQuestion is not null)
        {
            SelectedQuestion = null;
            IsNotesOpen = false;
        }

        var selectedTagIds = TagFacetChoices.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();

        if (selectedTagIds.Count > 0)
        {
            // Picking a tag is itself an act of narrowing focus, so it should immediately reveal
            // the matching questions rather than requiring a separate drill into a type/category tile.
            // Remember the level we jumped from, so deselecting the last tag can return there.
            if (DrillLevel != DrillLevel.Questions)
            {
                _drillLevelBeforeTagJump = DrillLevel;
                DrillLevel = DrillLevel.Questions;
                NotifyDrillVisibilityChanged();
            }
        }
        else if (_drillLevelBeforeTagJump is { } previousLevel)
        {
            // The last tag filter was just cleared and we only reached the question list
            // because of a tag pick (not a real tile drill-down) - go back to where we were.
            DrillLevel = previousLevel;
            _drillLevelBeforeTagJump = null;
            NotifyDrillVisibilityChanged();
        }

        // Re-scope the facet list to questions matching the current drill-down (type/category)
        // AND the currently selected tag(s), so the remaining tags shown are only ones that
        // actually still co-occur with what's selected - letting the user keep narrowing.
        // RebuildTagFacetChoices preserves the current selection internally, so an
        // already-selected tag stays checked even though it's filtered into its own scope here.
        var scopedByDrillDown = FilterByDrillDown(_allQuestions);
        var scopedByTags = selectedTagIds.Count > 0
            ? scopedByDrillDown.Where(q => selectedTagIds.All(q.TagIds.Contains))
            : scopedByDrillDown;
        RebuildTagFacetChoices(scopedByTags);

        RefreshCurrentView();
    }

    [RelayCommand]
    private void ShowAllQuestions()
    {
        SelectedQuestion = null;
        IsNotesOpen = false;
        CurrentSidebarView = SidebarView.AllQuestions;
        DrillLevel = DrillLevel.Types;
        SelectedType = null;
        SelectedCategoryId = null;
        _drillLevelBeforeTagJump = null;
        RebuildTagFacetChoices(_allQuestions);
        NotifyDrillVisibilityChanged();
        RefreshCurrentView();
    }

    [RelayCommand]
    private void ShowFavorites()
    {
        SelectedQuestion = null;
        IsNotesOpen = false;
        CurrentSidebarView = SidebarView.Favorites;
        NotifyDrillVisibilityChanged();
        RefreshCurrentView();
    }

    [RelayCommand]
    private void ShowRecentlyViewed()
    {
        SelectedQuestion = null;
        IsNotesOpen = false;
        CurrentSidebarView = SidebarView.RecentlyViewed;
        NotifyDrillVisibilityChanged();
        RefreshCurrentView();
    }

    [RelayCommand]
    private void SelectType(QuestionType type)
    {
        SelectedType = type;
        SelectedCategoryId = null;
        DrillLevel = DrillLevel.Categories;
        _drillLevelBeforeTagJump = null;

        var questionsInType = _allQuestions.Where(q => q.Type == type).ToList();

        var categoryCounts = questionsInType
            .SelectMany(q => q.CategoryIds)
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        CategoryTiles.Clear();
        foreach (var category in _allCategories.OrderBy(c => c.Name))
        {
            if (categoryCounts.TryGetValue(category.Id, out var count))
            {
                CategoryTiles.Add(new CategoryTileViewModel(category.Id, category.Name, count));
            }
        }

        RebuildTagFacetChoices(questionsInType);
        NotifyDrillVisibilityChanged();
        RefreshCurrentView();
    }

    [RelayCommand]
    private void SelectCategory(CategoryTileViewModel tile)
    {
        SelectedCategoryId = tile.CategoryId;
        SelectedCategoryName = tile.Name;
        DrillLevel = DrillLevel.Questions;
        _drillLevelBeforeTagJump = null;

        var questionsInCategory = _allQuestions
            .Where(q => (SelectedType is null || q.Type == SelectedType) && q.CategoryIds.Contains(tile.CategoryId));
        RebuildTagFacetChoices(questionsInCategory);

        NotifyDrillVisibilityChanged();
        RefreshCurrentView();
    }

    [RelayCommand]
    private void NavigateToBreadcrumbRoot()
    {
        ShowAllQuestions();
    }

    [RelayCommand]
    private void NavigateToBreadcrumbType()
    {
        if (SelectedType is { } type)
        {
            SelectType(type);
        }
    }

    private void NotifyDrillVisibilityChanged()
    {
        OnPropertyChanged(nameof(ShowTypeTiles));
        OnPropertyChanged(nameof(ShowCategoryTiles));
        OnPropertyChanged(nameof(ShowQuestionList));
        OnPropertyChanged(nameof(ShowBreadcrumb));
        OnPropertyChanged(nameof(BreadcrumbText));
    }

    partial void OnSearchTextChanged(string value) => RefreshCurrentView();

    partial void OnSelectedQuestionChanged(Question? value) => NotifyDrillVisibilityChanged();

    private void RefreshCurrentView()
    {
        IEnumerable<Question> source = CurrentSidebarView switch
        {
            SidebarView.Favorites => _allQuestions.Where(q => q.IsFavorite),
            SidebarView.RecentlyViewed => _allQuestions
                .Where(q => q.LastViewedAt is not null)
                .OrderByDescending(q => q.LastViewedAt)
                .Take(100),
            _ => FilterByDrillDown(_allQuestions)
        };

        var selectedTagIds = TagFacetChoices.Where(t => t.IsSelected).Select(t => t.Id).ToHashSet();
        if (selectedTagIds.Count > 0)
        {
            // AND across selected tags - a question must carry every selected tag, not just any one.
            source = source.Where(q => selectedTagIds.All(q.TagIds.Contains));
        }

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var needle = SearchText.Trim();
            source = source.Where(q =>
                q.Title.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                q.Answer.Contains(needle, StringComparison.OrdinalIgnoreCase) ||
                (q.Notes?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        var categoryLookup = _allCategories.ToDictionary(c => c.Id, c => c.Name);
        var tagLookup = _allTags.ToDictionary(t => t.Id, t => t.Name);

        QuestionList.Clear();
        foreach (var question in source.OrderByDescending(q => q.CreatedAt))
        {
            var categoryNames = question.CategoryIds
                .Where(categoryLookup.ContainsKey)
                .Select(id => categoryLookup[id])
                .ToList();
            var tagNames = question.TagIds
                .Where(tagLookup.ContainsKey)
                .Select(id => tagLookup[id])
                .ToList();
            QuestionList.Add(new QuestionListItemViewModel(question, categoryNames, tagNames));
        }
    }

    private IEnumerable<Question> FilterByDrillDown(IEnumerable<Question> source)
    {
        if (SelectedType is { } type)
        {
            source = source.Where(q => q.Type == type);
        }

        if (SelectedCategoryId is { } categoryId)
        {
            source = source.Where(q => q.CategoryIds.Contains(categoryId));
        }

        return source;
    }

    [RelayCommand]
    private async Task OpenQuestionAsync(QuestionListItemViewModel item) => await OpenQuestionByIdAsync(item.Question.Id);

    public async Task OpenQuestionByIdAsync(Guid questionId)
    {
        await _questionRepository.TouchLastViewedAsync(questionId);
        var refreshed = await _questionRepository.GetByIdAsync(questionId);
        SelectedQuestion = refreshed;
        IsNotesOpen = false;

        var index = _allQuestions.FindIndex(q => q.Id == questionId);
        if (index >= 0 && refreshed is not null)
        {
            _allQuestions[index] = refreshed;
        }
    }

    [RelayCommand]
    private void CloseQuestionDetail()
    {
        SelectedQuestion = null;
        IsNotesOpen = false;
    }

    [RelayCommand]
    private void ToggleNotes()
    {
        IsNotesOpen = !IsNotesOpen;
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Question question)
    {
        question.IsFavorite = !question.IsFavorite;
        await _questionRepository.SaveAsync(question);
        RefreshCurrentView();
    }

    public IReadOnlyList<Question> SearchAllQuestions(string query, bool exactMatch = false, int maxResults = 50)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var needle = query.Trim();
        Func<string?, bool> matches = exactMatch
            ? text => ContainsWholeWord(text, needle)
            : text => text?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false;

        return _allQuestions
            .Where(q => matches(q.Title) || matches(q.Answer) || matches(q.Notes))
            .OrderByDescending(q => matches(q.Title))
            .ThenBy(q => q.Title.Length)
            .Take(maxResults)
            .ToList();
    }

    private static readonly char[] WordBoundaryChars = [' ', '\t', '\n', '\r', '.', ',', '?', '!', ':', ';', '(', ')', '[', ']', '{', '}', '/', '\\', '"', '\'', '<', '>'];

    private static bool ContainsWholeWord(string? text, string word)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        return text
            .Split(WordBoundaryChars, StringSplitOptions.RemoveEmptyEntries)
            .Any(w => string.Equals(w, word, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Finds the most common whole word across all question titles that starts with <paramref name="prefix"/>,
    /// for inline autocomplete (e.g. typing "middle" suggests completing to "middleware").
    /// Returns null if no title contains a word with that prefix.
    /// </summary>
    public string? GetWordCompletion(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return null;
        }

        return _allQuestions
            .SelectMany(q => q.Title.Split(WordBoundaryChars, StringSplitOptions.RemoveEmptyEntries))
            .Where(word => word.Length > prefix.Length && word.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key.Length)
            .Select(g => g.Key)
            .FirstOrDefault();
    }

    public string GetCategoryName(Guid id) => _allCategories.FirstOrDefault(c => c.Id == id)?.Name ?? string.Empty;
    public string GetTagName(Guid id) => _allTags.FirstOrDefault(t => t.Id == id)?.Name ?? string.Empty;
    public IReadOnlyList<Category> AllCategories => _allCategories;
    public IReadOnlyList<Tag> AllTags => _allTags;

    [RelayCommand]
    private void StartAddQuestion()
    {
        EditingQuestion = QuestionEditViewModel.CreateNew(_allCategories, _allTags);
        IsEditFormOpen = true;
    }

    [RelayCommand]
    private void StartEditQuestion(Question question)
    {
        EditingQuestion = QuestionEditViewModel.FromExisting(question, _allCategories, _allTags);
        IsEditFormOpen = true;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditFormOpen = false;
        EditingQuestion = null;
    }

    [RelayCommand]
    private async Task SaveQuestionAsync()
    {
        if (EditingQuestion is null)
        {
            return;
        }

        var categoryIds = new List<Guid>();
        foreach (var name in EditingQuestion.GetSelectedOrNewCategoryNames())
        {
            var category = await _categoryRepository.GetOrCreateAsync(name);
            categoryIds.Add(category.Id);
        }

        var tagIds = new List<Guid>();
        foreach (var name in EditingQuestion.GetSelectedOrNewTagNames())
        {
            var tag = await _tagRepository.GetOrCreateAsync(name);
            tagIds.Add(tag.Id);
        }

        var question = EditingQuestion.OriginalQuestion ?? new Question();
        question.Title = EditingQuestion.Title.Trim();
        question.Answer = EditingQuestion.Answer;
        question.Type = EditingQuestion.SelectedType;
        question.CategoryIds = categoryIds;
        question.TagIds = tagIds;
        question.Notes = string.IsNullOrWhiteSpace(EditingQuestion.Notes) ? null : EditingQuestion.Notes;

        await _questionRepository.SaveAsync(question);

        IsEditFormOpen = false;
        EditingQuestion = null;

        var wasViewingThisQuestion = SelectedQuestion?.Id == question.Id;

        await ReloadAllAsync();

        if (wasViewingThisQuestion)
        {
            // ReloadAllAsync rebuilds _allQuestions with fresh instances, so the detail
            // view's SelectedQuestion needs to be swapped to the refreshed one - otherwise
            // it keeps pointing at the pre-save object and edits (like Notes) never appear
            // in the open detail pane even though they saved correctly.
            SelectedQuestion = _allQuestions.FirstOrDefault(q => q.Id == question.Id);
        }
    }

    [RelayCommand]
    private async Task DeleteQuestionAsync(Question question)
    {
        await _questionRepository.DeleteAsync(question.Id);
        if (SelectedQuestion?.Id == question.Id)
        {
            SelectedQuestion = null;
        }

        await ReloadAllAsync();
    }
}
