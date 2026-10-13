namespace Interrogon.Core.Models;

/// <summary>
/// An ordered study path through existing questions. Roadmaps are built and imported by an AI
/// agent (see IMPORT_INSTRUCTIONS.md), never edited in the app. The order of <see cref="Steps"/>
/// is a deliberate decision and is the only thing that determines display order.
/// </summary>
public class Roadmap
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Steps in study order; a step's position is its index + 1. Order is stored by the list
    /// itself (not a number field) so inserting or removing a step shifts the rest by construction.
    /// </summary>
    public List<RoadmapStep> Steps { get; set; } = [];
}

public class RoadmapStep
{
    public Guid QuestionId { get; set; }

    /// <summary>
    /// Optional section label (e.g. "A1" or "Stage 3: Embeddings"). The app draws a divider
    /// wherever this value changes between consecutive steps.
    /// </summary>
    public string? Stage { get; set; }
}
