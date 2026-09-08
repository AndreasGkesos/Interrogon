using System.Collections.ObjectModel;
using Interrogon.App.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Interrogon.App.Controls;

public sealed partial class SearchDialogContent : UserControl
{
    private readonly MainViewModel _viewModel;
    private readonly ObservableCollection<QuestionListItemViewModel> _results = [];
    private bool _suppressTextChanged;
    private bool _completionRejected;

    public event Action<Guid>? ResultChosen;

    public SearchDialogContent(MainViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        ResultsItemsControl.ItemsSource = _results;
    }

    public void FocusInput() => QueryTextBox.Focus(FocusState.Programmatic);

    private string _previousText = string.Empty;

    private void QueryTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressTextChanged)
        {
            return;
        }

        var typed = QueryTextBox.Text;

        // Any edit that grows the text (a real keystroke, not a deletion) means the user
        // is typing fresh input again, so completion suggestions should resume.
        if (typed.Length > _previousText.Length)
        {
            _completionRejected = false;
        }
        _previousText = typed;

        RunSearch(typed);
        ApplyCompletion(typed);
    }

    private void RunSearch(string typed)
    {
        _results.Clear();

        if (string.IsNullOrWhiteSpace(typed))
        {
            EmptyStateText.Visibility = Visibility.Visible;
            ResultsScrollViewer.Visibility = Visibility.Collapsed;
            return;
        }

        var matches = _viewModel.SearchAllQuestions(typed, ExactMatchToggle.IsOn);
        var categoryLookup = _viewModel.AllCategories.ToDictionary(c => c.Id, c => c.Name);
        var tagLookup = _viewModel.AllTags.ToDictionary(t => t.Id, t => t.Name);

        foreach (var question in matches)
        {
            var categoryNames = question.CategoryIds
                .Where(categoryLookup.ContainsKey)
                .Select(id => categoryLookup[id])
                .ToList();
            var tagNames = question.TagIds
                .Where(tagLookup.ContainsKey)
                .Select(id => tagLookup[id])
                .ToList();
            _results.Add(new QuestionListItemViewModel(question, categoryNames, tagNames));
        }

        EmptyStateText.Visibility = Visibility.Collapsed;
        ResultsScrollViewer.Visibility = Visibility.Visible;
    }

    private void ApplyCompletion(string typed)
    {
        if (_completionRejected)
        {
            return;
        }

        // Only complete the word currently being typed (no trailing space), and only
        // when the caret is at the end - otherwise ghost-completing mid-sentence edits is confusing.
        if (string.IsNullOrEmpty(typed) || QueryTextBox.SelectionStart != typed.Length || typed.EndsWith(' '))
        {
            return;
        }

        var lastSpace = typed.LastIndexOf(' ');
        var currentWord = lastSpace >= 0 ? typed[(lastSpace + 1)..] : typed;
        if (currentWord.Length == 0)
        {
            return;
        }

        var completion = _viewModel.GetWordCompletion(currentWord);
        if (completion is null)
        {
            return;
        }

        var prefix = typed[..(typed.Length - currentWord.Length)];
        var completedText = prefix + completion;

        _suppressTextChanged = true;
        QueryTextBox.Text = completedText;
        QueryTextBox.Select(typed.Length, completedText.Length - typed.Length);
        _suppressTextChanged = false;
        _previousText = completedText;
    }

    private void QueryTextBox_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
    {
        // If there's a ghosted (selected) completion tail, Right/Tab/End accepts it by
        // collapsing the selection to its end instead of performing their usual action.
        if (QueryTextBox.SelectionLength <= 0)
        {
            return;
        }

        if (e.Key is VirtualKey.Right or VirtualKey.Tab or VirtualKey.End)
        {
            var acceptedLength = QueryTextBox.SelectionStart + QueryTextBox.SelectionLength;
            QueryTextBox.Select(acceptedLength, 0);
            e.Handled = true;
        }
        else if (e.Key is VirtualKey.Back or VirtualKey.Delete)
        {
            // Backspace/Delete with a ghosted tail selected should just drop the
            // suggestion and leave the real typed text untouched, not delete into it.
            // Mark the completion as rejected so it doesn't immediately reappear -
            // otherwise every shorter prefix of the same word re-triggers the same
            // suggestion and Backspace feels like it does nothing.
            var typedLength = QueryTextBox.SelectionStart;
            _completionRejected = true;
            _suppressTextChanged = true;
            QueryTextBox.Text = QueryTextBox.Text[..typedLength];
            QueryTextBox.Select(typedLength, 0);
            _suppressTextChanged = false;
            _previousText = QueryTextBox.Text;
            RunSearch(QueryTextBox.Text);
            e.Handled = true;
        }
        else if (e.Key == VirtualKey.Enter && _results.Count > 0)
        {
            ResultChosen?.Invoke(_results[0].Question.Id);
            e.Handled = true;
        }
    }

    private void ExactMatchToggle_Toggled(object sender, RoutedEventArgs e) => RunSearch(QueryTextBox.Text);

    private void ResultButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: QuestionListItemViewModel item })
        {
            ResultChosen?.Invoke(item.Question.Id);
        }
    }
}
