$folders = @(
    "docs/architecture",
    "docs/domains",
    "docs/data",
    "docs/ai",
    "docs/integration",
    "docs/adr"
)

$files = @(
    "docs/architecture/01-system-overview.md",
    "docs/architecture/02-bounded-contexts.md",
    "docs/architecture/03-clean-architecture.md",
    "docs/architecture/04-hexagonal-architecture.md",
    "docs/architecture/05-event-driven-architecture.md",
    "docs/architecture/06-solution-structure.md",
    "docs/domains/catalog.md",
    "docs/domains/sellers.md",
    "docs/domains/offers.md",
    "docs/domains/pricing.md",
    "docs/domains/availability.md",
    "docs/domains/fulfillment.md",
    "docs/domains/freight.md",
    "docs/data/data-architecture.md",
    "docs/ai/ai-architecture.md",
    "docs/integration/integration-architecture.md"
)

foreach ($folder in $folders) {
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
}

foreach ($file in $files) {
    New-Item -ItemType File -Force -Path $file | Out-Null
}

Write-Host "Documentation structure created successfully."