namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// Input for a single contextual reranking call (docs task: "Contextual
/// candidate reranking"). Generic across catalog concepts: callers (Google
/// Category, AttributeDefinition, AttributeOption resolvers) supply a
/// task-specific instruction, the user's query/context, and only candidates
/// that were already hydrated and validated (typically against SQL Server).
/// The reranker never receives unvalidated pgvector-only candidates.
/// </summary>
public sealed record CandidateRerankRequest(
    string Task,
    string Query,
    string? Context,
    IReadOnlyList<RerankCandidate> Candidates,
    string Locale = "pt-BR");
