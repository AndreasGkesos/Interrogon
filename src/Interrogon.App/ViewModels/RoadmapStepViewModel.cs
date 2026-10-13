using CommunityToolkit.Mvvm.ComponentModel;
using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public partial class RoadmapStepViewModel : ObservableObject
{
    private readonly Action<RoadmapStepViewModel, bool> _onStudiedChanged;

    public RoadmapStepViewModel(
        int position,
        Question question,
        string? stageHeader,
        string categoryChips,
        string levelLabel,
        bool isStudied,
        Action<RoadmapStepViewModel, bool> onStudiedChanged)
    {
        Position = position;
        Question = question;
        StageHeader = stageHeader ?? string.Empty;
        CategoryChips = categoryChips;
        LevelLabel = levelLabel;
        _onStudiedChanged = onStudiedChanged;

        // Set the backing field directly so building the list doesn't write progress back to the DB.
        _isStudied = isStudied;
    }

    public int Position { get; }
    public Question Question { get; }
    public Guid QuestionId => Question.Id;
    public string Title => Question.Title;
    public string TypeLabel => QuestionTypeDisplay.GetLabel(Question.Type);
    public string CategoryChips { get; }
    public string LevelLabel { get; }

    /// <summary>Section heading shown above this row; empty unless the stage changes at this step.</summary>
    public string StageHeader { get; }
    public bool HasStageHeader => StageHeader.Length > 0;

    [ObservableProperty]
    private bool _isStudied;

    partial void OnIsStudiedChanged(bool value) => _onStudiedChanged(this, value);
}
