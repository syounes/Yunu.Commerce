using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Testcontainers.MsSql;
using Xunit;
using Yunu.Commerce.Catalog.Application.CanonicalTaxonomy.EffectiveSegmentDefinitions;
using Yunu.Commerce.Catalog.Domain.CanonicalTaxonomy;
using Yunu.Commerce.Catalog.Domain.Segments;
using Yunu.Commerce.Catalog.Infrastructure.CanonicalTaxonomy.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;
using Yunu.Commerce.Catalog.Infrastructure.SegmentCatalog.SqlServer;

namespace Yunu.Commerce.Catalog.IntegrationTests;

/// <summary>
/// Integration tests proving SqlCanonicalTaxonomySegmentAssociationReader
/// (the raw SQL adapter behind ICanonicalTaxonomySegmentAssociationReader)
/// correctly walks a real SQL Server recursive ancestor chain and maps every
/// field of CanonicalTaxonomySegmentAssociationCandidate, without applying
/// any business filtering (docs task: "Effective Segment Definitions por
/// Canonical Taxonomy Node"). Also proves the composition of the real
/// reader with the deterministic EffectiveSegmentDefinitionResolver.
///
/// Schema is created the same way as SqlCanonicalTaxonomyRepositoryTests and
/// SqlSegmentDefinitionRepositoryTests, by executing
/// deploy/databases/sqlserver/006, 007, 008, 009, 010, 011, 012 and 013
/// directly against a Testcontainers SQL Server instance. Test data (nodes,
/// definitions) is created through the legitimate repository/domain paths;
/// only the CanonicalTaxonomyNodeSegmentDefinitions junction rows are
/// inserted directly via SQL (there is no public write port for this
/// association yet), always respecting FK/CHECK constraints and always
/// stating Source/Status/AppliesToDescendants/IsRequired explicitly.
/// </summary>
public sealed class SqlCanonicalTaxonomySegmentAssociationReaderTests : IAsyncLifetime
{
    private readonly MsSqlContainer _sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
    private string _connectionString = null!;
    private SqlCanonicalTaxonomyRepository _canonicalTaxonomyRepository = null!;
    private SqlSegmentDefinitionRepository _segmentDefinitionRepository = null!;
    private SqlCanonicalTaxonomySegmentAssociationReader _reader = null!;

    public async Task InitializeAsync()
    {
        await _sqlContainer.StartAsync();

        _connectionString = _sqlContainer.GetConnectionString();

        await RunScriptAsync(_connectionString, "001-google-taxonomy-tables.sql");
        await RunScriptAsync(_connectionString, "006-create-canonical-taxonomy-segmentation.sql");
        await RunScriptAsync(_connectionString, "007-drop-legacy-catalog-hierarchy.sql");
        await RunScriptAsync(_connectionString, "008-add-segment-assignment-scope.sql");
        await SeedGoogleTaxonomyCategoriesAsync(_connectionString);
        await RunScriptAsync(_connectionString, "009-reset-canonical-taxonomy-starter.sql");
        await RunScriptAsync(_connectionString, "010-seed-canonical-taxonomy-node-segments.sql");
        await RunScriptAsync(_connectionString, "011-drop-segment-definitions-isrequired.sql");
        await RunScriptAsync(_connectionString, "012-add-canonical-taxonomy-concurrency-guard.sql");
        await RunScriptAsync(_connectionString, "013-remove-canonical-provider-coupling.sql");

        var options = Options.Create(new GoogleTaxonomySqlOptions
        {
            ConnectionString = _connectionString
        });

        _canonicalTaxonomyRepository = new SqlCanonicalTaxonomyRepository(options);
        _segmentDefinitionRepository = new SqlSegmentDefinitionRepository(options);
        _reader = new SqlCanonicalTaxonomySegmentAssociationReader(options);
    }

    public async Task DisposeAsync()
    {
        await _sqlContainer.DisposeAsync();
    }

    private static async Task SeedGoogleTaxonomyCategoriesAsync(string connectionString)
    {
        const string sql = """
            INSERT INTO Catalog.GoogleTaxonomyCategories
                (GoogleCategoryId, ParentGoogleCategoryId, Name, FullPath, Level, IsLeaf, IsActive, SourceLanguage, CreatedAt, ImportedAt)
            VALUES
                (166, NULL, N'Vestuário e acessórios', N'Vestuário e acessórios', 1, 0, 1, N'pt-BR', SYSUTCDATETIME(), SYSUTCDATETIME()),
                (187, 166, N'Sapatos', N'Vestuário e acessórios > Sapatos', 2, 1, 1, N'pt-BR', SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task RunScriptAsync(string connectionString, string fileName)
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "..",
            "deploy", "databases", "sqlserver", fileName);

        var script = await File.ReadAllTextAsync(Path.GetFullPath(scriptPath));

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        foreach (var batch in script.Split("GO", StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.IsNullOrWhiteSpace(batch))
            {
                continue;
            }

            await using var command = new SqlCommand(batch, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    // ------------------------------------------------------------------
    // Test data helpers: multi-level tree built through legitimate
    // Domain/Repository paths (Root -> Level1 -> Level2 -> Leaf), each
    // test using unique Codes to avoid collisions with 009/010 seed data
    // or with other tests.
    // ------------------------------------------------------------------

    private async Task<CanonicalTaxonomyNodeId> CreateRootAsync(string code)
    {
        var node = CanonicalTaxonomyNode.CreateRoot(
            new CanonicalTaxonomyNodeId(0),
            code,
            $"Name {code}",
            $"name {code}",
            "Description",
            $"Name {code}",
            status: CanonicalTaxonomyNodeStatus.Active);

        return await _canonicalTaxonomyRepository.AddAsync(node, CancellationToken.None);
    }

    private async Task<CanonicalTaxonomyNodeId> CreateChildAsync(string code, CanonicalTaxonomyNodeId parentId, int depth)
    {
        var (_, parentRevision) = (await _canonicalTaxonomyRepository.GetWithRevisionAsync(parentId, CancellationToken.None))!.Value;

        var child = CanonicalTaxonomyNode.CreateChild(
            new CanonicalTaxonomyNodeId(0), parentId, code, $"Name {code}", $"name {code}", null, depth, $"Path {code}",
            status: CanonicalTaxonomyNodeStatus.Active);

        var result = await _canonicalTaxonomyRepository.AddChildAsync(child, parentRevision, CancellationToken.None);
        Assert.Equal(AddCanonicalTaxonomyChildOutcome.Created, result.Outcome);
        return result.AssignedId!.Value;
    }

    /// <summary>
    /// Creates a full deterministic Root -> Level1 -> Level2 -> Leaf tree
    /// with unique node codes for the given test-specific prefix.
    /// </summary>
    private async Task<(CanonicalTaxonomyNodeId Root, CanonicalTaxonomyNodeId Level1, CanonicalTaxonomyNodeId Level2, CanonicalTaxonomyNodeId Leaf)>
        CreateFourLevelTreeAsync(string prefix)
    {
        var root = await CreateRootAsync($"{prefix}-root");
        var level1 = await CreateChildAsync($"{prefix}-level1", root, 1);
        var level2 = await CreateChildAsync($"{prefix}-level2", level1, 2);
        var leaf = await CreateChildAsync($"{prefix}-leaf", level2, 3);

        return (root, level1, level2, leaf);
    }

    private async Task<SegmentDefinitionId> CreateActiveSegmentDefinitionAsync(string code)
    {
        var definition = SegmentDefinition.Create(
            new SegmentDefinitionCode(code),
            new SegmentDefinitionName($"Name {code}"),
            "Description",
            "Semantic text",
            SegmentSelectionMode.Single,
            SegmentAssignmentScope.Product);

        var id = await _segmentDefinitionRepository.AddAsync(definition, CancellationToken.None);

        var persisted = await _segmentDefinitionRepository.GetByIdAsync(id, CancellationToken.None);
        persisted!.Update(
            persisted.Name,
            persisted.Description,
            persisted.SemanticText,
            persisted.SelectionMode,
            persisted.AssignmentScope,
            SegmentDefinitionStatus.Active);
        await _segmentDefinitionRepository.UpdateAsync(persisted, CancellationToken.None);

        return id;
    }

    private async Task<SegmentDefinitionId> CreateInactiveSegmentDefinitionAsync(string code)
    {
        var id = await CreateActiveSegmentDefinitionAsync(code);

        var persisted = await _segmentDefinitionRepository.GetByIdAsync(id, CancellationToken.None);
        persisted!.Update(
            persisted.Name,
            persisted.Description,
            persisted.SemanticText,
            persisted.SelectionMode,
            persisted.AssignmentScope,
            SegmentDefinitionStatus.Inactive);
        await _segmentDefinitionRepository.UpdateAsync(persisted, CancellationToken.None);

        return id;
    }

    /// <summary>
    /// Inserts a Catalog.CanonicalTaxonomyNodeSegmentDefinitions junction
    /// row directly via SQL, respecting FK/CHECK constraints, always stating
    /// AppliesToDescendants/IsRequired/Source/Status explicitly. There is no
    /// public write port for this association; direct SQL setup is
    /// acceptable per this task's scope for test-only association rows.
    /// </summary>
    private async Task InsertAssociationAsync(
        CanonicalTaxonomyNodeId nodeId,
        SegmentDefinitionId segmentDefinitionId,
        bool appliesToDescendants,
        bool isRequired,
        string source,
        string status,
        decimal? confidence = null)
    {
        const string sql = """
            INSERT INTO Catalog.CanonicalTaxonomyNodeSegmentDefinitions
                (CanonicalTaxonomyNodeId, SegmentDefinitionId, AppliesToDescendants, IsRequired, Source, Confidence, Status, CreatedAt, UpdatedAt)
            VALUES
                (@NodeId, @DefinitionId, @AppliesToDescendants, @IsRequired, @Source, @Confidence, @Status, SYSUTCDATETIME(), SYSUTCDATETIME());
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NodeId", nodeId.Value);
        command.Parameters.AddWithValue("@DefinitionId", segmentDefinitionId.Value);
        command.Parameters.AddWithValue("@AppliesToDescendants", appliesToDescendants);
        command.Parameters.AddWithValue("@IsRequired", isRequired);
        command.Parameters.AddWithValue("@Source", source);
        command.Parameters.AddWithValue("@Confidence", (object?)confidence ?? DBNull.Value);
        command.Parameters.AddWithValue("@Status", status);

        await command.ExecuteNonQueryAsync();
    }

    // ==================================================================
    // A. Direct association
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_returns_direct_association_regardless_of_AppliesToDescendants()
    {
        var (_, _, _, leaf) = await CreateFourLevelTreeAsync("direct");
        var definitionId = await CreateActiveSegmentDefinitionAsync("direct_def");

        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: true, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        var candidate = Assert.Single(candidates, c => c.SegmentDefinitionId == definitionId.Value);
        Assert.Equal(leaf.Value, candidate.OriginCanonicalTaxonomyNodeId);
        Assert.Equal(3, candidate.OriginNodeDepth);
        Assert.True(candidate.IsSelf);
        Assert.Equal("direct_def", candidate.Code);
        Assert.Equal("Name direct_def", candidate.Name);
        Assert.Equal("Product", candidate.AssignmentScope);
        Assert.Equal("Approved", candidate.AssociationStatus);
        Assert.Equal("Yunu", candidate.AssociationSource);
        Assert.True(candidate.AssociationIsRequired);
        Assert.False(candidate.AppliesToDescendants);
        Assert.Equal("Active", candidate.DefinitionStatus);
    }

    // ==================================================================
    // B. Single inherited association
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_returns_single_inherited_association_from_ancestor()
    {
        var (root, _, _, leaf) = await CreateFourLevelTreeAsync("inherited");
        var definitionId = await CreateActiveSegmentDefinitionAsync("inherited_def");

        await InsertAssociationAsync(root, definitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        var candidate = Assert.Single(candidates, c => c.SegmentDefinitionId == definitionId.Value);
        Assert.False(candidate.IsSelf);
        Assert.Equal(root.Value, candidate.OriginCanonicalTaxonomyNodeId);
        Assert.Equal(0, candidate.OriginNodeDepth);
        Assert.True(candidate.AppliesToDescendants);
    }

    // ==================================================================
    // C. Multiple ancestry levels
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_walks_multiple_ancestry_levels()
    {
        var (root, level1, level2, leaf) = await CreateFourLevelTreeAsync("multilevel");

        var rootDefinitionId = await CreateActiveSegmentDefinitionAsync("multilevel_root_def");
        var level1DefinitionId = await CreateActiveSegmentDefinitionAsync("multilevel_level1_def");
        var level2DefinitionId = await CreateActiveSegmentDefinitionAsync("multilevel_level2_def");

        await InsertAssociationAsync(root, rootDefinitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");
        await InsertAssociationAsync(level1, level1DefinitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");
        await InsertAssociationAsync(level2, level2DefinitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        Assert.Contains(candidates, c => c.SegmentDefinitionId == rootDefinitionId.Value && c.OriginCanonicalTaxonomyNodeId == root.Value);
        Assert.Contains(candidates, c => c.SegmentDefinitionId == level1DefinitionId.Value && c.OriginCanonicalTaxonomyNodeId == level1.Value);
        Assert.Contains(candidates, c => c.SegmentDefinitionId == level2DefinitionId.Value && c.OriginCanonicalTaxonomyNodeId == level2.Value);
    }

    // ==================================================================
    // D. AppliesToDescendants = false is still returned raw
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_returns_ancestor_association_with_AppliesToDescendants_false_raw()
    {
        var (root, _, _, leaf) = await CreateFourLevelTreeAsync("nopropagate");
        var definitionId = await CreateActiveSegmentDefinitionAsync("nopropagate_def");

        await InsertAssociationAsync(root, definitionId, appliesToDescendants: false, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        var candidate = Assert.Single(candidates, c => c.SegmentDefinitionId == definitionId.Value);
        Assert.False(candidate.AppliesToDescendants);
        Assert.False(candidate.IsSelf);
    }

    // ==================================================================
    // E. Association statuses are returned raw
    // ==================================================================

    [Theory]
    [InlineData("Approved")]
    [InlineData("Suggested")]
    [InlineData("Rejected")]
    [InlineData("Inactive")]
    public async Task GetAssociationCandidatesAsync_returns_association_with_any_status_raw(string status)
    {
        var leaf = await CreateRootAsync($"status-{status.ToLowerInvariant()}");
        var definitionId = await CreateActiveSegmentDefinitionAsync($"status_{status.ToLowerInvariant()}_def");

        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: false, source: "Yunu", status: status);

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        var candidate = Assert.Single(candidates, c => c.SegmentDefinitionId == definitionId.Value);
        Assert.Equal(status, candidate.AssociationStatus);
    }

    // ==================================================================
    // F. Inactive Definition is returned raw
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_returns_association_for_inactive_definition_raw()
    {
        var leaf = await CreateRootAsync("inactive-def-node");
        var definitionId = await CreateInactiveSegmentDefinitionAsync("inactive_def");

        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        var candidate = Assert.Single(candidates, c => c.SegmentDefinitionId == definitionId.Value);
        Assert.Equal("Inactive", candidate.DefinitionStatus);
    }

    // ==================================================================
    // G. Same SegmentDefinition at different levels
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_returns_both_candidates_for_same_definition_at_different_levels()
    {
        var (root, _, level2, leaf) = await CreateFourLevelTreeAsync("duplicatedef");
        var definitionId = await CreateActiveSegmentDefinitionAsync("duplicatedef_def");

        await InsertAssociationAsync(root, definitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");
        await InsertAssociationAsync(level2, definitionId, appliesToDescendants: true, isRequired: true, source: "AI", status: "Approved", confidence: 0.9m);

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        var matches = candidates.Where(c => c.SegmentDefinitionId == definitionId.Value).ToArray();
        Assert.Equal(2, matches.Length);
        Assert.Contains(matches, c => c.OriginCanonicalTaxonomyNodeId == root.Value && !c.AssociationIsRequired && c.AssociationSource == "Yunu");
        Assert.Contains(matches, c => c.OriginCanonicalTaxonomyNodeId == level2.Value && c.AssociationIsRequired && c.AssociationSource == "AI");
    }

    // ==================================================================
    // H. No associations
    // ==================================================================

    [Fact]
    public async Task GetAssociationCandidatesAsync_returns_empty_collection_when_no_associations_exist()
    {
        var node = await CreateRootAsync("no-associations-node");

        var candidates = await _reader.GetAssociationCandidatesAsync(node.Value, CancellationToken.None);

        Assert.Empty(candidates);
    }

    // ==================================================================
    // Real SQL + EffectiveSegmentDefinitionResolver composition
    // ==================================================================

    [Fact]
    public async Task Resolve_makes_direct_association_effective()
    {
        var leaf = await CreateRootAsync("resolve-direct-node");
        var definitionId = await CreateActiveSegmentDefinitionAsync("resolve_direct_def");

        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: true, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);
        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        var response = Assert.Single(effective, r => r.SegmentDefinitionId == definitionId.Value);
        Assert.True(response.IsDirect);
        Assert.Equal(leaf.Value, response.OriginCanonicalTaxonomyNodeId);
        Assert.True(response.IsRequired);
    }

    [Fact]
    public async Task Resolve_makes_inherited_Approved_Active_AppliesToDescendants_true_association_effective()
    {
        var (root, _, _, leaf) = await CreateFourLevelTreeAsync("resolve-inherited");
        var definitionId = await CreateActiveSegmentDefinitionAsync("resolve_inherited_def");

        await InsertAssociationAsync(root, definitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);
        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        var response = Assert.Single(effective, r => r.SegmentDefinitionId == definitionId.Value);
        Assert.False(response.IsDirect);
        Assert.Equal(root.Value, response.OriginCanonicalTaxonomyNodeId);
    }

    [Fact]
    public async Task Resolve_excludes_inherited_association_with_AppliesToDescendants_false()
    {
        var (root, _, _, leaf) = await CreateFourLevelTreeAsync("resolve-nopropagate");
        var definitionId = await CreateActiveSegmentDefinitionAsync("resolve_nopropagate_def");

        await InsertAssociationAsync(root, definitionId, appliesToDescendants: false, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);
        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.DoesNotContain(effective, r => r.SegmentDefinitionId == definitionId.Value);
    }

    [Theory]
    [InlineData("Suggested")]
    [InlineData("Rejected")]
    [InlineData("Inactive")]
    public async Task Resolve_excludes_non_Approved_association_statuses(string status)
    {
        var leaf = await CreateRootAsync($"resolve-status-{status.ToLowerInvariant()}");
        var definitionId = await CreateActiveSegmentDefinitionAsync($"resolve_status_{status.ToLowerInvariant()}_def");

        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: false, source: "Yunu", status: status);

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);
        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.DoesNotContain(effective, r => r.SegmentDefinitionId == definitionId.Value);
    }

    [Fact]
    public async Task Resolve_excludes_inactive_segment_definition()
    {
        var leaf = await CreateRootAsync("resolve-inactive-def-node");
        var definitionId = await CreateInactiveSegmentDefinitionAsync("resolve_inactive_def");

        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: false, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);
        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.DoesNotContain(effective, r => r.SegmentDefinitionId == definitionId.Value);
    }

    [Fact]
    public async Task Resolve_selects_deepest_origin_when_same_definition_at_multiple_levels()
    {
        var (root, _, level2, leaf) = await CreateFourLevelTreeAsync("resolve-precedence");
        var definitionId = await CreateActiveSegmentDefinitionAsync("resolve_precedence_def");

        // Root: optional, Yunu-sourced.
        await InsertAssociationAsync(root, definitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");
        // Level2: required, AI-sourced - more specific, must win.
        await InsertAssociationAsync(level2, definitionId, appliesToDescendants: true, isRequired: true, source: "AI", status: "Approved", confidence: 0.95m);

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        // Raw reader must still return both candidates (proves recursive CTE + no dedup in SQL).
        Assert.Equal(2, candidates.Count(c => c.SegmentDefinitionId == definitionId.Value));

        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);
        var response = Assert.Single(effective, r => r.SegmentDefinitionId == definitionId.Value);

        Assert.Equal(level2.Value, response.OriginCanonicalTaxonomyNodeId);
        Assert.False(response.IsDirect);
        Assert.True(response.IsRequired);
        Assert.Equal("AI", response.AssociationSource);
    }

    [Fact]
    public async Task Resolve_direct_association_wins_over_inherited_association_for_same_definition()
    {
        var (root, _, _, leaf) = await CreateFourLevelTreeAsync("resolve-direct-wins");
        var definitionId = await CreateActiveSegmentDefinitionAsync("resolve_direct_wins_def");

        // Ancestor: optional.
        await InsertAssociationAsync(root, definitionId, appliesToDescendants: true, isRequired: false, source: "Yunu", status: "Approved");
        // Direct on the leaf itself: required - must win regardless of AppliesToDescendants.
        await InsertAssociationAsync(leaf, definitionId, appliesToDescendants: false, isRequired: true, source: "Yunu", status: "Approved");

        var candidates = await _reader.GetAssociationCandidatesAsync(leaf.Value, CancellationToken.None);

        Assert.Equal(2, candidates.Count(c => c.SegmentDefinitionId == definitionId.Value));

        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);
        var response = Assert.Single(effective, r => r.SegmentDefinitionId == definitionId.Value);

        Assert.True(response.IsDirect);
        Assert.Equal(leaf.Value, response.OriginCanonicalTaxonomyNodeId);
        Assert.True(response.IsRequired);
    }

    [Fact]
    public async Task Resolve_returns_empty_collection_when_node_has_no_applicable_segments()
    {
        var node = await CreateRootAsync("resolve-empty-node");

        var candidates = await _reader.GetAssociationCandidatesAsync(node.Value, CancellationToken.None);
        var effective = EffectiveSegmentDefinitionResolver.Resolve(candidates);

        Assert.Empty(effective);
    }
}
