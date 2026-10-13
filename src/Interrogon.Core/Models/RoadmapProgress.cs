namespace Interrogon.Core.Models;

/// <summary>
/// "This question was studied in the context of this roadmap." Stored separately from both
/// <see cref="Question"/> (the same question can be studied in one roadmap and not another) and
/// <see cref="Roadmap"/> (so editing a roadmap never loses progress).
/// </summary>
public class RoadmapProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RoadmapId { get; set; }
    public Guid QuestionId { get; set; }
    public DateTimeOffset StudiedAt { get; set; } = DateTimeOffset.UtcNow;
}
