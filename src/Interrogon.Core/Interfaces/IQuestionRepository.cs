using Interrogon.Core.Models;

namespace Interrogon.Core.Interfaces;

public interface IQuestionRepository
{
    Task<IReadOnlyList<Question>> GetAllAsync();
    Task<Question?> GetByIdAsync(Guid id);
    Task<Question?> GetByImportKeyAsync(string importKey);
    Task SaveAsync(Question question);
    Task DeleteAsync(Guid id);
    Task TouchLastViewedAsync(Guid id);
    Task<IReadOnlyList<Question>> GetRecentlyViewedAsync(int maxCount);
}
