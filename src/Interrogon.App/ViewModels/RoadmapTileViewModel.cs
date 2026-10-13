using CommunityToolkit.Mvvm.ComponentModel;
using Interrogon.Core.Models;

namespace Interrogon.App.ViewModels;

public partial class RoadmapTileViewModel : ObservableObject
{
    public RoadmapTileViewModel(Roadmap roadmap, int total, int studied)
    {
        Roadmap = roadmap;
        Total = total;
        _progressText = FormatProgress(studied, total);
    }

    public Roadmap Roadmap { get; }
    public Guid Id => Roadmap.Id;
    public string Name => Roadmap.Name;
    public string Description => Roadmap.Description;
    public int Total { get; }

    [ObservableProperty]
    private string _progressText;

    public void UpdateStudied(int studied) => ProgressText = FormatProgress(studied, Total);

    private static string FormatProgress(int studied, int total) => $"{studied} / {total} studied";
}
