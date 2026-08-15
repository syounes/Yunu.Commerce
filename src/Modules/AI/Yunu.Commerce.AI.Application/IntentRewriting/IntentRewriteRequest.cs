namespace Yunu.Commerce.AI.Application.IntentRewriting;

/// <summary>
/// Input to <see cref="IIntentRewriter"/> (docs task: "Intent/Query
/// Rewriting"). <see cref="Input"/> is the raw natural-language text as typed
/// by the user; no retrieval, catalog lookups or persistence happen before
/// this call.
/// </summary>
public sealed record IntentRewriteRequest(string Input, string Locale = "pt-BR");
