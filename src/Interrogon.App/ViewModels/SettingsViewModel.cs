using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Interrogon.App.Logging;
using Interrogon.Core.Interfaces;

namespace Interrogon.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ICategoryRepository _categoryRepository;
    private readonly ITagRepository _tagRepository;
    private readonly IQuestionRepository _questionRepository;

    public ObservableCollection<SettingsListItemViewModel> Categories { get; } = [];
    public ObservableCollection<SettingsListItemViewModel> Tags { get; } = [];
    public string LogFolderPath => LogPaths.LogFolder;

    public event Action? DataChanged;

    public SettingsViewModel(ICategoryRepository categoryRepository, ITagRepository tagRepository, IQuestionRepository questionRepository)
    {
        _categoryRepository = categoryRepository;
        _tagRepository = tagRepository;
        _questionRepository = questionRepository;
    }

    public async Task LoadAsync()
    {
        var questions = await _questionRepository.GetAllAsync();
        var categories = await _categoryRepository.GetAllAsync();
        var tags = await _tagRepository.GetAllAsync();

        Categories.Clear();
        foreach (var category in categories.OrderBy(c => c.Name))
        {
            var count = questions.Count(q => q.CategoryIds.Contains(category.Id));
            Categories.Add(new SettingsListItemViewModel(category.Id, category.Name, count));
        }

        Tags.Clear();
        foreach (var tag in tags.OrderBy(t => t.Name))
        {
            var count = questions.Count(q => q.TagIds.Contains(tag.Id));
            Tags.Add(new SettingsListItemViewModel(tag.Id, tag.Name, count));
        }
    }

    [RelayCommand]
    private async Task AddCategoryAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await _categoryRepository.GetOrCreateAsync(name);
        await LoadAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task RenameCategoryAsync((Guid Id, string NewName) args)
    {
        if (string.IsNullOrWhiteSpace(args.NewName))
        {
            return;
        }

        await _categoryRepository.RenameAsync(args.Id, args.NewName);
        await LoadAsync();
        DataChanged?.Invoke();
    }

    /// <summary>
    /// Number of questions that have this category as their ONLY category - deleting it
    /// would leave them with zero categories, making them unreachable when browsing.
    /// </summary>
    public async Task<int> CountQuestionsThatWouldBeOrphanedByCategoryDeleteAsync(Guid categoryId)
    {
        var questions = await _questionRepository.GetAllAsync();
        return questions.Count(q => q.CategoryIds.Count == 1 && q.CategoryIds[0] == categoryId);
    }

    [RelayCommand]
    private async Task DeleteCategoryAsync(Guid id)
    {
        await _categoryRepository.DeleteAsync(id);
        await LoadAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task AddTagAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await _tagRepository.GetOrCreateAsync(name);
        await LoadAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task RenameTagAsync((Guid Id, string NewName) args)
    {
        if (string.IsNullOrWhiteSpace(args.NewName))
        {
            return;
        }

        await _tagRepository.RenameAsync(args.Id, args.NewName);
        await LoadAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private async Task DeleteTagAsync(Guid id)
    {
        await _tagRepository.DeleteAsync(id);
        await LoadAsync();
        DataChanged?.Invoke();
    }

    [RelayCommand]
    private void OpenLogFolder()
    {
        Directory.CreateDirectory(LogPaths.LogFolder);
        Process.Start("explorer.exe", LogPaths.LogFolder);
    }
}
