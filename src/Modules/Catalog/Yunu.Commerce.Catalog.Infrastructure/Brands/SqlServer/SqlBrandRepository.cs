using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using Yunu.Commerce.Catalog.Domain.Brands;
using Yunu.Commerce.Catalog.Infrastructure.GoogleTaxonomy.Persistence.SqlServer;

namespace Yunu.Commerce.Catalog.Infrastructure.Brands.SqlServer;

public sealed class SqlBrandRepository : IBrandRepository
{
    private readonly string _connectionString;

    public SqlBrandRepository(IOptions<GoogleTaxonomySqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task UpdateAsync(Brand brand, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE Catalog.Brands
            SET Name = @Name,
                NormalizedName = @NormalizedName,
                Status = @Status,
                UpdatedAt = @UpdatedAt
            WHERE BrandId = @BrandId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", brand.Name.Value);
        command.Parameters.AddWithValue("@NormalizedName", brand.NormalizedName);
        command.Parameters.AddWithValue("@Status", brand.Status.ToString());
        command.Parameters.AddWithValue("@UpdatedAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("@BrandId", brand.Id.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task AddAsync(Brand brand, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO Catalog.Brands (BrandId, Code, Name, NormalizedName, Status, CreatedAt)
            VALUES (@BrandId, @Code, @Name, @NormalizedName, @Status, @CreatedAt)
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BrandId", brand.Id.Value);
        command.Parameters.AddWithValue("@Code", brand.Code.Value);
        command.Parameters.AddWithValue("@Name", brand.Name.Value);
        command.Parameters.AddWithValue("@NormalizedName", brand.NormalizedName);
        command.Parameters.AddWithValue("@Status", brand.Status.ToString());
        command.Parameters.AddWithValue("@CreatedAt", brand.CreatedAtUtc);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Brand?> GetByIdAsync(BrandId id, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT BrandId, Code, Name, NormalizedName, Status, CreatedAt
            FROM Catalog.Brands
            WHERE BrandId = @BrandId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BrandId", id.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadBrand(reader);
    }

    public async Task DeleteAsync(BrandId id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE FROM Catalog.Brands
            WHERE BrandId = @BrandId
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BrandId", id.Value);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<Brand?> GetByCodeAsync(BrandCode code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT BrandId, Code, Name, NormalizedName, Status, CreatedAt
            FROM Catalog.Brands
            WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadBrand(reader);
    }

    public async Task<Brand?> FindByNormalizedNameAsync(string normalizedName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT BrandId, Code, Name, NormalizedName, Status, CreatedAt
            FROM Catalog.Brands
            WHERE NormalizedName = @NormalizedName
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NormalizedName", normalizedName);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return ReadBrand(reader);
    }

    public async Task<bool> ExistsCodeAsync(BrandCode code, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1 FROM Catalog.Brands WHERE Code = @Code
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Code", code.Value);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result != null;
    }

    private static Brand ReadBrand(SqlDataReader reader)
    {
        var brandId = reader.GetGuid(0);
        var code = reader.GetString(1);
        var name = reader.GetString(2);
        var normalized = reader.GetString(3);
        var status = reader.GetString(4);
        var createdAt = ReadDateTimeOffset(reader, 5);

        var brand = Brand.Reconstitute(
            new BrandId(brandId),
            new BrandCode(code),
            new BrandName(name),
            normalized,
            Enum.Parse<BrandStatus>(status),
            createdAt);

        return brand;
    }

    /// <summary>
    /// Reads a timestamp column as <see cref="DateTimeOffset"/> regardless of whether the
    /// underlying SQL Server column type is DATETIMEOFFSET or a plain DATETIME/DATETIME2.
    /// <see cref="SqlDataReader.GetDateTimeOffset"/> throws <see cref="InvalidCastException"/>
    /// when the column value is a plain <see cref="DateTime"/>; values read that way are
    /// treated as UTC, matching the CreatedAt/UpdatedAt convention (physical column names;
    /// values still represent UTC instants).
    /// </summary>
    private static DateTimeOffset ReadDateTimeOffset(SqlDataReader reader, int ordinal)
    {
        var fieldType = reader.GetFieldType(ordinal);
        if (fieldType == typeof(DateTimeOffset))
        {
            return reader.GetDateTimeOffset(ordinal);
        }

        var dateTime = reader.GetDateTime(ordinal);
        return new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
    }
}
