using Interrogon.Core.Interfaces;
using Interrogon.Core.Models;
using LiteDB;

namespace Interrogon.Services.Persistence;

public class TagRepository : ITagRepository
{
    private const string CollectionName = "tags";

    private readonly ILiteCollection<Tag> _tags;
    private readonly ILiteCollection<Question> _questions;

    public TagRepository(LiteDbContext dbContext)
    {
        _tags = dbContext.GetCollection<Tag>(CollectionName);
        _tags.EnsureIndex(t => t.Name);
        _questions = dbContext.GetCollection<Question>("questions");
    }

    public Task<IReadOnlyList<Tag>> GetAllAsync()
    {
        var tags = _tags.Query().OrderBy(t => t.Name).ToList();
        return Task.FromResult<IReadOnlyList<Tag>>(tags);
    }

    public Task<Tag> GetOrCreateAsync(string name)
    {
        var trimmed = name.Trim();
        var existing = _tags.FindOne(t => t.Name == trimmed);
        if (existing is not null)
        {
            return Task.FromResult(existing);
        }

        var tag = new Tag { Name = trimmed };
        _tags.Insert(tag);
        return Task.FromResult(tag);
    }

    public Task RenameAsync(Guid id, string newName)
    {
        var tag = _tags.FindById(id);
        if (tag is not null)
        {
            tag.Name = newName.Trim();
            _tags.Update(tag);
        }

        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _tags.Delete(id);

        var affected = _questions.Find(q => q.TagIds.Contains(id)).ToList();
        foreach (var question in affected)
        {
            question.TagIds.Remove(id);
            _questions.Update(question);
        }

        return Task.CompletedTask;
    }
}
