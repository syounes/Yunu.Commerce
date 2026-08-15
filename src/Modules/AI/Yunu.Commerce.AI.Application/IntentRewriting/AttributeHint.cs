namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// A textual attribute hint extracted from user input (docs task: "Intent/Query
/// Rewriting"). Intentionally free-text only: the Intent Rewriter never emits
/// official attribute/option identifiers. Resolving hints to canonical
/// AttributeDefinition/AttributeOption identifiers is a later retrieval
/// concern (SQL Server / pgvector), not the LLM's responsibility.
/// </summary>
public sealed record AttributeHint(string RawName, string? RawValue);
