using Interrogon.Core.Interfaces;
using Interrogon.Core.Models;
using LiteDB;

namespace Interrogon.Services.Persistence;

public class QuestionRepository : IQuestionRepository
{
    private const string CollectionName = "questions";

    private readonly ILiteCollection<Question> _questions;

    public QuestionRepository(LiteDbContext dbContext)
    {
        _questions = dbContext.GetCollection<Question>(CollectionName);
        _questions.EnsureIndex(q => q.ImportKey);
        _questions.EnsureIndex(q => q.LastViewedAt);
        _questions.EnsureIndex(q => q.IsFavorite);
    }

    public Task<IReadOnlyList<Question>> GetAllAsync()
    {
        var questions = _questions.Query().ToList();
        return Task.FromResult<IReadOnlyList<Question>>(questions);
    }

    public Task<Question?> GetByIdAsync(Guid id)
    {
        var question = _questions.FindById(id);
        return Task.FromResult<Question?>(question);
    }

    public Task<Question?> GetByImportKeyAsync(string importKey)
    {
        var question = _questions.FindOne(q => q.ImportKey == importKey);
        return Task.FromResult<Question?>(question);
    }

    public Task SaveAsync(Question question)
    {
        _questions.Upsert(question);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid id)
    {
        _questions.Delete(id);
        return Task.CompletedTask;
    }

    public Task TouchLastViewedAsync(Guid id)
    {
        var question = _questions.FindById(id);
        if (question is not null)
        {
            question.LastViewedAt = DateTimeOffset.UtcNow;
            _questions.Update(question);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Question>> GetRecentlyViewedAsync(int maxCount)
    {
        var questions = _questions.Query()
            .Where(q => q.LastViewedAt != null)
            .OrderByDescending(q => q.LastViewedAt)
            .Limit(maxCount)
            .ToList();

        return Task.FromResult<IReadOnlyList<Question>>(questions);
    }
}
