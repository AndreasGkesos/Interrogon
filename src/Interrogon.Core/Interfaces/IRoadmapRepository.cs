using Interrogon.Core.Models;

namespace Interrogon.Core.Interfaces;

/// <summary>
/// Read-only access to roadmaps (they are imported by an agent, not edited in the app) plus
/// read/write access to per-roadmap "studied" progress.
/// </summary>
public interface IRoadmapRepository
{
    Task<IReadOnlyList<Roadmap>> GetAllAsync();
    Task<IReadOnlyList<RoadmapProgress>> GetAllProgressAsync();
    Task SetStudiedAsync(Guid roadmapId, Guid questionId, bool studied);
}
