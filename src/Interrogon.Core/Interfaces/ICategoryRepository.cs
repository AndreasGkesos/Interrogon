using Interrogon.Core.Models;

namespace Interrogon.Core.Interfaces;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> GetAllAsync();
    Task<Category> GetOrCreateAsync(string name);
    Task RenameAsync(Guid id, string newName);
    Task DeleteAsync(Guid id);
}
