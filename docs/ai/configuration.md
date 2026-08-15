# AI Module Configuration

This document describes how to configure AI connections and logical model
registrations for the Yunu.Commerce AI module (Embeddings and Intent/Query
Rewriting), and how to set the required secret locally.

## Configuration shape

```json
"AI": {
  "Connections": {
    "AzureOpenAI": {
      "Endpoint": "https://aif-yunu-commerce-lab.openai.azure.com/openai/v1/"
    }
  },
  "Models": {
    "CategoryEmbedding": {
      "Connection": "AzureOpenAI",
      "DeploymentName": "yunu-embedding-category-v1",
      "ModelType": "Embedding",
      "Dimensions": 1536
    },
    "IntentRewriter": {
      "Connection": "AzureOpenAI",
      "DeploymentName": "yunu-intent-rewriter-v1",
      "ModelType": "Chat"
    }
  }
}
```

`Connections` holds one entry per Azure OpenAI (or other provider) resource:
endpoint plus API key. `Models` holds one entry per logical model, each
pointing at the connection it uses. Multiple models can share the same
connection when they belong to the same underlying resource, so the
endpoint/credential is configured once per resource instead of once per
deployment.

## Logical models

```text
CategoryEmbedding
→ yunu-embedding-category-v1
→ Embedding
→ 1536 dimensions
→ Used by the Google Taxonomy and SKU attribute embedding synchronizers.

IntentRewriter
→ yunu-intent-rewriter-v1
→ Chat / Structured Output
→ Normalizes and classifies natural-language catalog queries (docs task:
  "Intent/Query Rewriting"). Never returns official catalog identifiers.
```

## Setting the API key locally

The API key belongs to the connection, never to a specific deployment, and
must never be committed to `appsettings.json`, `appsettings.Development.json`,
`docker-compose.yml` or source code.

Locally, use .NET User Secrets against the `Yunu.Commerce.Api` project:

```powershell
dotnet user-secrets set "AI:Connections:AzureOpenAI:ApiKey" "<secret>" --project src/Hosts/Yunu.Commerce.Api
```

In other environments, use the equivalent environment variable:

```plaintext
AI__Connections__AzureOpenAI__ApiKey
```

## Adding another connection or model

To add another Azure OpenAI resource (or another provider), add a new entry
under `AI:Connections` and point any new logical model at it under
`AI:Models`. Configuration is validated at startup (`ValidateOnStart`): a
missing connection, invalid endpoint, empty deployment name, invalid
`ModelType`, or a missing `Dimensions` for an Embedding model will fail fast
with a descriptive error instead of failing at first request.
