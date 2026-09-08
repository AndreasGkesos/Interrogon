using Interrogon.Core.Interfaces;
using Interrogon.Core.Models;
using LiteDB;

namespace Interrogon.Services.Persistence;

public class CategoryRepository : ICategoryRepository
{
    private const string CollectionName = "categories";

    private readonly ILiteCollection<Category> _categories;
    private readonly ILiteCollection<Question> _questions;

    public CategoryRepository(LiteDbContext dbContext)
    {
        _categories = dbContext.GetCollection<Category>(CollectionName);
        _categories.EnsureIndex(c => c.Name);
        _questions = dbContext.GetCollection<Question>("questions");
    }

    public Task<IReadOnlyList<Category>> GetAllAsync()
    {
        var categories = _categories.Query().OrderBy(c => c.Name).ToList();
        return Task.FromResult<IReadOnlyList<Category>>(categories);
    }

    public Task<Category> GetOrCreateAsync(string name)
    {
        var trimmed = name.Trim();
        var existing = _categories.FindOne(c => c.Name == trimmed);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        var category = new Category { Name = trimmed };
        _categories.Insert(category);
        return Task.FromResult(category);
    }

    public Task RenameAsync(Guid id, string newName)
    {
        var category = _categories.FindById(id);
        if (category is not null)
        {
            category.Name = newName.Trim();
            _categories.Update(category);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _categories.Delete(id);

        var affected = _questions.Find(q => q.CategoryIds.Contains(id)).ToList();
        foreach (var question in affected)
        {
            question.CategoryIds.Remove(id);
            _questions.Update(question);
        }

        return Task.CompletedTask;
    }
}
