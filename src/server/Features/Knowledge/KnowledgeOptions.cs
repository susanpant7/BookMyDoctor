namespace BookMyDoctor.Features.Knowledge;

public sealed class KnowledgeOptions
{
    public const string SectionName = "Knowledge";

    // How many chunks to retrieve per question, and the lowest cosine similarity that still counts as relevant.
    public int Top { get; init; } = 4;
    public double MinScore { get; init; } = 0.5;

    // Also drop chunks scoring more than this below the best match; scores bunch together, so weak matches
    // otherwise still pass MinScore.
    public double MaxScoreGap { get; init; } = 0.05;
}
