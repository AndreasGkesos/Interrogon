using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public class TypeTileViewModel(QuestionType type, int count)
{
    public QuestionType Type { get; } = type;
    public string Label { get; } = QuestionTypeDisplay.GetLabel(type);
    public string Glyph { get; } = QuestionTypeDisplay.GetGlyph(type);
    public int Count { get; } = count;
}
