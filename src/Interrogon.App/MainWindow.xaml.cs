using System.ComponentModel;
using Interrogon.App.Controls;
using Interrogon.App.ViewModels;
using Interrogon.Core;
using Interrogon.Core.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;

namespace Interrogon.App;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private SettingsWindow? _settingsWindow;

    private static readonly double[] FontScaleSteps = [1.0, 1.5, 2.0];
    private int _fontScaleIndex;

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<MainViewModel>();
        Title = "Interrogon";

        if (MicaController.IsSupported())
        {
            SystemBackdrop = new MicaBackdrop { Kind = MicaKind.BaseAlt };
        }
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        SetWindowIcon();

        TypeTilesItemsControl.ItemsSource = ViewModel.TypeTiles;
        CategoryTilesItemsControl.ItemsSource = ViewModel.CategoryTiles;
        QuestionListItemsControl.ItemsSource = ViewModel.QuestionList;
        TagFacetItemsControl.ItemsSource = ViewModel.VisibleTagFacetChoices;
        RoadmapListItemsControl.ItemsSource = ViewModel.RoadmapTiles;
        RoadmapStepsListView.ItemsSource = ViewModel.RoadmapSteps;

        EditTypeComboBox.ItemsSource = QuestionTypeDisplay.All.Select(QuestionTypeDisplay.GetLabel).ToList();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;

        _ = ViewModel.InitializeAsync();
        UpdateVisibleState();
    }

    private void SetWindowIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
        if (File.Exists(iconPath))
        {
            AppWindow.SetIcon(iconPath);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateVisibleState();

        if (e.PropertyName == nameof(MainViewModel.SelectedQuestion))
        {
            UpdateDetailPane();
        }

        if (e.PropertyName == nameof(MainViewModel.IsNotesOpen))
        {
            NotesPanel.Visibility = ViewModel.IsNotesOpen ? Visibility.Visible : Visibility.Collapsed;
            NotesColumn.Width = ViewModel.IsNotesOpen ? new GridLength(320) : new GridLength(0);
        }

        if (e.PropertyName == nameof(MainViewModel.EditingQuestion))
        {
            UpdateEditForm();
        }
    }

    private void UpdateVisibleState()
    {
        TypeTilesScrollViewer.Visibility = ViewModel.ShowTypeTiles ? Visibility.Visible : Visibility.Collapsed;
        CategoryTilesScrollViewer.Visibility = ViewModel.ShowCategoryTiles ? Visibility.Visible : Visibility.Collapsed;
        QuestionListScrollViewer.Visibility = ViewModel.ShowQuestionList ? Visibility.Visible : Visibility.Collapsed;

        RoadmapListScrollViewer.Visibility = ViewModel.ShowRoadmapList ? Visibility.Visible : Visibility.Collapsed;
        RoadmapEmptyText.Visibility = ViewModel.RoadmapTiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        RoadmapDetailGrid.Visibility = ViewModel.ShowRoadmapDetail ? Visibility.Visible : Visibility.Collapsed;
        RoadmapTitleText.Text = ViewModel.SelectedRoadmap?.Name ?? string.Empty;
        RoadmapProgressText.Text = ViewModel.SelectedRoadmapProgressText;

        BreadcrumbPanel.Visibility = ViewModel.ShowBreadcrumb ? Visibility.Visible : Visibility.Collapsed;
        if (ViewModel.ShowBreadcrumb)
        {
            BreadcrumbTypeSeparator.Visibility = Visibility.Visible;
            BreadcrumbTypeButton.Visibility = Visibility.Visible;
            BreadcrumbTypeButton.Content = ViewModel.SelectedType is { } type ? QuestionTypeDisplay.GetLabel(type) : string.Empty;

            var showCategory = ViewModel.SelectedCategoryId is not null;
            BreadcrumbCategorySeparator.Visibility = showCategory ? Visibility.Visible : Visibility.Collapsed;
            BreadcrumbCategoryText.Visibility = showCategory ? Visibility.Visible : Visibility.Collapsed;
            BreadcrumbCategoryText.Text = ViewModel.SelectedCategoryName;
        }

        DetailOverlay.Visibility = ViewModel.SelectedQuestion is not null ? Visibility.Visible : Visibility.Collapsed;
        EditOverlay.Visibility = ViewModel.IsEditFormOpen ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDetailPane()
    {
        var question = ViewModel.SelectedQuestion;
        if (question is null)
        {
            return;
        }

        DetailTitleText.Text = question.Title;
        DetailFavoriteIcon.Glyph = question.IsFavorite ? "" : "";

        if (question.TagIds.Count > 0)
        {
            var tagNames = question.TagIds.Select(ViewModel.GetTagName).Where(name => name.Length > 0);
            DetailTagsText.Text = "Tags: " + string.Join(" - ", tagNames);
            DetailTagsText.Visibility = Visibility.Visible;
        }
        else
        {
            DetailTagsText.Visibility = Visibility.Collapsed;
        }

        DetailAnswerMarkdown.Markdown = question.Answer;
        DetailNotesMarkdown.Markdown = question.Notes;
        NotesButton.Content = "Notes";
    }

    private void UpdateEditForm()
    {
        var editing = ViewModel.EditingQuestion;
        EditFormHeaderText.Text = editing?.OriginalQuestion is null ? "Add Question" : "Edit Question";

        if (editing is null)
        {
            return;
        }

        EditTitleBox.Text = editing.Title;
        EditAnswerBox.Text = editing.Answer;
        EditNotesBox.Text = editing.Notes ?? string.Empty;
        EditTypeComboBox.SelectedIndex = QuestionTypeDisplay.All.ToList().IndexOf(editing.SelectedType);

        EditCategoryChoicesItemsControl.ItemsSource = editing.CategoryChoices;
        EditTagChoicesItemsControl.ItemsSource = editing.TagChoices;
    }

    // --- Sidebar ---

    private void AllQuestionsButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowAllQuestionsCommand.Execute(null);
    private void FavoritesButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowFavoritesCommand.Execute(null);
    private void RecentlyViewedButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowRecentlyViewedCommand.Execute(null);

    private void RoadmapsButton_Click(object sender, RoutedEventArgs e) => ViewModel.ShowRoadmapsCommand.Execute(null);

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settingsWindow is null)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
            _settingsWindow.DataChanged += () => _ = ViewModel.InitializeAsync();
        }

        _settingsWindow.Activate();
    }

    // --- Search ---

    private async void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        var searchContent = new SearchDialogContent(ViewModel);

        var dialog = new ContentDialog
        {
            Title = "Search",
            Content = searchContent,
            CloseButtonText = "Close",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot,
            HorizontalAlignment = HorizontalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        dialog.Resources["ContentDialogMaxWidth"] = 720d;

        searchContent.ResultChosen += async questionId =>
        {
            dialog.Hide();
            await ViewModel.OpenQuestionByIdAsync(questionId);
        };

        searchContent.FocusInput();
        await dialog.ShowAsync();
    }

    private void AddQuestionButton_Click(object sender, RoutedEventArgs e) => ViewModel.StartAddQuestionCommand.Execute(null);

    // --- Breadcrumb ---

    private void BreadcrumbRoot_Click(object sender, RoutedEventArgs e) => ViewModel.NavigateToBreadcrumbRootCommand.Execute(null);
    private void BreadcrumbType_Click(object sender, RoutedEventArgs e) => ViewModel.NavigateToBreadcrumbTypeCommand.Execute(null);

    // --- Drill-down tiles ---

    private void TypeTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: TypeTileViewModel tile })
        {
            ViewModel.SelectTypeCommand.Execute(tile.Type);
        }
    }

    private void CategoryTile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CategoryTileViewModel tile })
        {
            ViewModel.SelectCategoryCommand.Execute(tile);
        }
    }

    // --- Roadmaps ---

    private void RoadmapRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: RoadmapTileViewModel tile })
        {
            ViewModel.OpenRoadmapCommand.Execute(tile);
        }
    }

    private void BackToRoadmaps_Click(object sender, RoutedEventArgs e) => ViewModel.BackToRoadmapsCommand.Execute(null);

    // Taps on the studied toggle (or its padding) must not bubble up to the row and open the question.
    private void StudiedToggle_Tapped(object sender, TappedRoutedEventArgs e) => e.Handled = true;

    private void RoadmapStepRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { Tag: RoadmapStepViewModel step })
        {
            ViewModel.OpenRoadmapQuestionCommand.Execute(step);
        }
    }

    // --- Question list ---

    private void QuestionRow_Tapped(object sender, TappedRoutedEventArgs e)
    {
        if (sender is Grid { Tag: QuestionListItemViewModel item })
        {
            ViewModel.OpenQuestionCommand.Execute(item);
        }
    }

    private void FavoriteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Question question })
        {
            ViewModel.ToggleFavoriteCommand.Execute(question);
        }
    }

    private void DetailFavoriteToggle_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuestion is not { } question)
        {
            return;
        }

        ViewModel.ToggleFavoriteCommand.Execute(question);
        DetailFavoriteIcon.Glyph = question.IsFavorite ? "" : "";
    }

    private void EditQuestionRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: Question question })
        {
            ViewModel.StartEditQuestionCommand.Execute(question);
        }
    }

    // --- Detail overlay ---

    private void CloseDetail_Click(object sender, RoutedEventArgs e) => ViewModel.CloseQuestionDetailCommand.Execute(null);
    private void ToggleNotes_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleNotesCommand.Execute(null);

    private void IncreaseFontSize_Click(object sender, RoutedEventArgs e)
    {
        _fontScaleIndex = Math.Min(_fontScaleIndex + 1, FontScaleSteps.Length - 1);
        ApplyFontScale();
    }

    private void DecreaseFontSize_Click(object sender, RoutedEventArgs e)
    {
        _fontScaleIndex = Math.Max(_fontScaleIndex - 1, 0);
        ApplyFontScale();
    }

    private void ApplyFontScale()
    {
        var scale = FontScaleSteps[_fontScaleIndex];
        DetailAnswerMarkdown.FontScale = scale;
        DetailNotesMarkdown.FontScale = scale;
    }

    private void CopyQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuestion is { } question)
        {
            SetClipboardText(MarkdownContent.ToPlainText(question.Title));
        }
    }

    private void CopyAnswer_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuestion is { } question)
        {
            SetClipboardText(MarkdownContent.ToPlainText(question.Answer));
        }
    }

    private static void SetClipboardText(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void EditDetail_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuestion is { } question)
        {
            ViewModel.StartEditQuestionCommand.Execute(question);
        }
    }

    private async void DeleteDetail_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedQuestion is not { } question)
        {
            return;
        }

        var dialog = new ContentDialog
        {
            Title = "Delete this question?",
            Content = "This can't be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            await ViewModel.DeleteQuestionCommand.ExecuteAsync(question);
        }
    }

    // --- Edit form ---

    private void AddNewCategory_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EditingQuestion is { } editing)
        {
            editing.NewCategoryText = EditNewCategoryBox.Text;
            editing.AddNewCategory();
            EditNewCategoryBox.Text = string.Empty;
        }
    }

    private void AddNewTag_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EditingQuestion is { } editing)
        {
            editing.NewTagText = EditNewTagBox.Text;
            editing.AddNewTag();
            EditNewTagBox.Text = string.Empty;
        }
    }

    private async void SaveEdit_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.EditingQuestion is not { } editing)
        {
            return;
        }

        editing.Title = EditTitleBox.Text;
        editing.Answer = EditAnswerBox.Text;
        editing.Notes = EditNotesBox.Text;
        if (EditTypeComboBox.SelectedIndex >= 0)
        {
            editing.SelectedType = QuestionTypeDisplay.All[EditTypeComboBox.SelectedIndex];
        }

        if (!editing.GetSelectedOrNewCategoryNames().Any())
        {
            var dialog = new ContentDialog
            {
                Title = "Category required",
                Content = "A question needs at least one category, or it won't show up anywhere when browsing - pick an existing category or add a new one before saving.",
                CloseButtonText = "OK",
                XamlRoot = Content.XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }

        await ViewModel.SaveQuestionCommand.ExecuteAsync(null);
    }

    private void CancelEdit_Click(object sender, RoutedEventArgs e) => ViewModel.CancelEditCommand.Execute(null);
}
