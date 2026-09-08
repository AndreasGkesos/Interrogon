namespace Interrogon.App.ViewModels;

public class SettingsListItemViewModel(Guid id, string name, int questionCount)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public int QuestionCount { get; } = questionCount;
}
