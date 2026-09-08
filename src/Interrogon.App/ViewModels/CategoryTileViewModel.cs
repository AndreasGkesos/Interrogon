namespace Interrogon.App.ViewModels;

public class CategoryTileViewModel(Guid categoryId, string name, int count)
{
    public Guid CategoryId { get; } = categoryId;
    public string Name { get; } = name;
    public int Count { get; } = count;
}
