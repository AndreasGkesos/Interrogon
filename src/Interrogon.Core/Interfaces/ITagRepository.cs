using Interrogon.Core.Models;

namespace Interrogon.Core.Interfaces;

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> GetAllAsync();
    Task<Tag> GetOrCreateAsync(string name);
    Task RenameAsync(Guid id, string newName);
    Task DeleteAsync(Guid id);
}
