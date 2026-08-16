namespace Yunu.Commerce.AI.Application.Reranking;

/// <summary>
/// A single candidate offered to the reranker (docs task: "Contextual
/// candidate reranking"). Deliberately generic: it carries no Google
/// Category, AttributeDefinition or AttributeOption concept, only a stable
/// <see cref="Index"/> the caller can map back to its own already-validated
/// candidate object. Never includes official IDs/codes: the reranker must
/// never see or return them (docs restriction: "não retornar IDs").
/// </summary>
public sealed record RerankCandidate(
    int Index,
    string DisplayText,
    string? Metadata);
