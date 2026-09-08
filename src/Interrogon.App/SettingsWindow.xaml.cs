using Interrogon.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Interrogon.App;

public sealed partial class SettingsWindow : Window
{
    public SettingsViewModel ViewModel { get; }

    public event Action? DataChanged;

    public SettingsWindow()
    {
        InitializeComponent();
        ViewModel = App.Services.GetRequiredService<SettingsViewModel>();
        ViewModel.DataChanged += () => DataChanged?.Invoke();
        Title = "Interrogon Settings";

        CategoriesListView.ItemsSource = ViewModel.Categories;
        TagsListView.ItemsSource = ViewModel.Tags;
        LogFolderPathText.Text = ViewModel.LogFolderPath;

        _ = ViewModel.LoadAsync();
    }

    private void OpenLogFolderButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenLogFolderCommand.Execute(null);
    }

    private void AddCategory_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddCategoryCommand.Execute(NewCategoryBox.Text);
        NewCategoryBox.Text = string.Empty;
    }

    private void AddTag_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.AddTagCommand.Execute(NewTagBox.Text);
        NewTagBox.Text = string.Empty;
    }

    private async void RenameItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SettingsListItemViewModel item })
        {
            return;
        }

        var isCategory = ViewModel.Categories.Contains(item);

        var textBox = new TextBox { Text = item.Name, SelectionStart = item.Name.Length };
        var dialog = new ContentDialog
        {
            Title = $"Rename \"{item.Name}\"",
            Content = textBox,
            PrimaryButtonText = "Rename",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (isCategory)
        {
            await ViewModel.RenameCategoryCommand.ExecuteAsync((item.Id, textBox.Text));
        }
        else
        {
            await ViewModel.RenameTagCommand.ExecuteAsync((item.Id, textBox.Text));
        }
    }

    private async void DeleteItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: SettingsListItemViewModel item })
        {
            return;
        }

        var isCategory = ViewModel.Categories.Contains(item);

        if (isCategory)
        {
            var orphanCount = await ViewModel.CountQuestionsThatWouldBeOrphanedByCategoryDeleteAsync(item.Id);
            if (orphanCount > 0)
            {
                var blockedDialog = new ContentDialog
                {
                    Title = "Can't delete this category",
                    Content = $"{orphanCount} question(s) have \"{item.Name}\" as their only category. Deleting it would leave them with none, making them unreachable when browsing. Give those questions another category first, then delete \"{item.Name}\".",
                    CloseButtonText = "OK",
                    XamlRoot = Content.XamlRoot
                };
                await blockedDialog.ShowAsync();
                return;
            }
        }

        var message = item.QuestionCount > 0
            ? $"\"{item.Name}\" is attached to {item.QuestionCount} question(s). Deleting it will remove it from those questions."
            : $"Delete \"{item.Name}\"?";

        var dialog = new ContentDialog
        {
            Title = "Delete this item?",
            Content = message,
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = Content.XamlRoot
        };

        var result = await dialog.ShowAsync();
        if (result != ContentDialogResult.Primary)
        {
            return;
        }

        if (isCategory)
        {
            await ViewModel.DeleteCategoryCommand.ExecuteAsync(item.Id);
        }
        else
        {
            await ViewModel.DeleteTagCommand.ExecuteAsync(item.Id);
        }
    }
}
