using Interrogon.Core.Interfaces;
using Interrogon.Core.Models;
using LiteDB;

namespace Interrogon.Services.Persistence;

public class RoadmapRepository : IRoadmapRepository
{
    private const string RoadmapsCollectionName = "roadmaps";
    private const string ProgressCollectionName = "roadmap_progress";

    private readonly ILiteCollection<Roadmap> _roadmaps;
    private readonly ILiteCollection<RoadmapProgress> _progress;

    public RoadmapRepository(LiteDbContext dbContext)
    {
        _roadmaps = dbContext.GetCollection<Roadmap>(RoadmapsCollectionName);
        _roadmaps.EnsureIndex(r => r.Name);
        _progress = dbContext.GetCollection<RoadmapProgress>(ProgressCollectionName);
        _progress.EnsureIndex(p => p.RoadmapId);
    }

    public Task<IReadOnlyList<Roadmap>> GetAllAsync()
    {
        var roadmaps = _roadmaps.Query().OrderBy(r => r.Name).ToList();
        return Task.FromResult<IReadOnlyList<Roadmap>>(roadmaps);
    }

    public Task<IReadOnlyList<RoadmapProgress>> GetAllProgressAsync()
    {
        var progress = _progress.Query().ToList();
        return Task.FromResult<IReadOnlyList<RoadmapProgress>>(progress);
    }

    public Task SetStudiedAsync(Guid roadmapId, Guid questionId, bool studied)
    {
        var existing = _progress.Find(p => p.RoadmapId == roadmapId && p.QuestionId == questionId).ToList();

        if (studied)
        {
            if (existing.Count == 0)
            {
                _progress.Insert(new RoadmapProgress { RoadmapId = roadmapId, QuestionId = questionId });
            }
        }
        else
        {
            foreach (var row in existing)
            {
                _progress.Delete(row.Id);
            }
        }

        return Task.CompletedTask;
    }
}
