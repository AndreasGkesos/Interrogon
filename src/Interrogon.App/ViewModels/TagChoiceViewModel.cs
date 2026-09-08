using CommunityToolkit.Mvvm.ComponentModel;

namespace Interrogon.App.ViewModels;

public partial class TagChoiceViewModel(Guid id, string name, int count = 0) : ObservableObject
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public int Count { get; } = count;
    public string DisplayText { get; } = $"{name} ({count})";

    [ObservableProperty]
    private bool _isSelected;
}
