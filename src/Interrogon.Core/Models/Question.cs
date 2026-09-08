namespace Interrogon.Core.Models;

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public List<Guid> CategoryIds { get; set; } = [];
    public List<Guid> TagIds { get; set; } = [];
    public string? Notes { get; set; }
    public bool IsFavorite { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastViewedAt { get; set; }

    /// <summary>
    /// Stable external identity for a question sourced from ImportData (e.g. a hash of the
    /// source document path + extracted title), used to detect duplicates on re-import without
    /// relying on the LiteDB Id, which is regenerated per insert.
    /// </summary>
    public string? ImportKey { get; set; }
}
