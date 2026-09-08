using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public partial class QuestionEditViewModel : ObservableObject
{
    public Question? OriginalQuestion { get; private init; }

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _answer = string.Empty;

    [ObservableProperty]
    private string? _notes;

    [ObservableProperty]
    private QuestionType _selectedType = QuestionType.TheoryQA;

    [ObservableProperty]
    private string _newCategoryText = string.Empty;

    [ObservableProperty]
    private string _newTagText = string.Empty;

    public ObservableCollection<TagChoiceViewModel> CategoryChoices { get; } = [];
    public ObservableCollection<TagChoiceViewModel> TagChoices { get; } = [];

    public IReadOnlyList<QuestionType> AllTypes => QuestionTypeDisplay.All;

    public static QuestionEditViewModel CreateNew(IReadOnlyList<Category> categories, IReadOnlyList<Tag> tags)
    {
        var vm = new QuestionEditViewModel();
        vm.PopulateChoices(categories, tags, selectedCategoryIds: [], selectedTagIds: []);
        return vm;
    }

    public static QuestionEditViewModel FromExisting(Question question, IReadOnlyList<Category> categories, IReadOnlyList<Tag> tags)
    {
        var vm = new QuestionEditViewModel
        {
            OriginalQuestion = question,
            Title = question.Title,
            Answer = question.Answer,
            Notes = question.Notes,
            SelectedType = question.Type
        };
        vm.PopulateChoices(categories, tags, question.CategoryIds, question.TagIds);
        return vm;
    }

    private void PopulateChoices(IReadOnlyList<Category> categories, IReadOnlyList<Tag> tags, IReadOnlyList<Guid> selectedCategoryIds, IReadOnlyList<Guid> selectedTagIds)
    {
        foreach (var category in categories.OrderBy(c => c.Name))
        {
            CategoryChoices.Add(new TagChoiceViewModel(category.Id, category.Name) { IsSelected = selectedCategoryIds.Contains(category.Id) });
        }

        foreach (var tag in tags.OrderBy(t => t.Name))
        {
            TagChoices.Add(new TagChoiceViewModel(tag.Id, tag.Name) { IsSelected = selectedTagIds.Contains(tag.Id) });
        }
    }

    public void AddNewCategory()
    {
        var name = NewCategoryText.Trim();
        if (string.IsNullOrEmpty(name) || CategoryChoices.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewCategoryText = string.Empty;
            return;
        }

        CategoryChoices.Add(new TagChoiceViewModel(Guid.NewGuid(), name) { IsSelected = true });
        NewCategoryText = string.Empty;
    }

    public void AddNewTag()
    {
        var name = NewTagText.Trim();
        if (string.IsNullOrEmpty(name) || TagChoices.Any(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            NewTagText = string.Empty;
            return;
        }

        TagChoices.Add(new TagChoiceViewModel(Guid.NewGuid(), name) { IsSelected = true });
        NewTagText = string.Empty;
    }

    public IEnumerable<string> GetSelectedOrNewCategoryNames() =>
        CategoryChoices.Where(c => c.IsSelected).Select(c => c.Name);

    public IEnumerable<string> GetSelectedOrNewTagNames() =>
        TagChoices.Where(t => t.IsSelected).Select(t => t.Name);
}
