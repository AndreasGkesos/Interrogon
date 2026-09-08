using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public class QuestionListItemViewModel(Question question, IReadOnlyList<string> categoryNames, IReadOnlyList<string>? tagNames = null)
{
    public Question Question { get; } = question;
    public string Title => Question.Title;
    public string TypeLabel => QuestionTypeDisplay.GetLabel(Question.Type);
    public string TypeGlyph => QuestionTypeDisplay.GetGlyph(Question.Type);
    public bool IsFavorite => Question.IsFavorite;
    public string FavoriteGlyph => Question.IsFavorite ? "" : "";
    public string CategoryChips => string.Join(" · ", categoryNames);
    public string TagChipsWithSeparator => tagNames is { Count: > 0 } ? $" — {string.Join(", ", tagNames)}" : string.Empty;
}
