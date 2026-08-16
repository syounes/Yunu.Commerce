using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Yunu.Commerce.Catalog.Domain.ProductProposals;

namespace Yunu.Commerce.Catalog.Infrastructure.Persistence.Mongo;

/// <summary>
/// MongoDB adapter implementing the Catalog.Domain IProductProposalRepository
/// port (docs task: "Catalog intent resolution orchestration" - proposal
/// persistence). Mirrors <see cref="MongoProductRepository"/>; implements
/// exactly the existing contract (create, read by id). Index creation is not
/// performed here: this project has no existing pattern for asynchronous
/// index initialization at repository construction time.
/// </summary>
public sealed class MongoProductProposalRepository : IProductProposalRepository
{
    private readonly IMongoCollection<ProductProposalMongoModel> _collection;

    public MongoProductProposalRepository(IMongoClient mongoClient, IOptions<CatalogMongoOptions> options)
    {
        var database = mongoClient.GetDatabase(options.Value.DatabaseName);
        _collection = database.GetCollection<ProductProposalMongoModel>(options.Value.ProductProposalsCollectionName);
    }

    public async Task AddAsync(ProductProposal proposal, CancellationToken cancellationToken)
    {
        var model = ProductProposalMapper.ToMongoModel(proposal);

        await _collection.InsertOneAsync(model, options: null, cancellationToken);
    }

    public async Task<ProductProposal?> GetByIdAsync(ProductProposalId id, CancellationToken cancellationToken)
    {
        var model = await _collection
            .Find(m => m.Id == id.Value)
            .FirstOrDefaultAsync(cancellationToken);

        return model is null ? null : ProductProposalMapper.ToDomain(model);
    }
}
