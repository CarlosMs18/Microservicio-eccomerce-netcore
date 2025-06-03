namespace Cart.Application.Contracts.External
{
    public interface ICatalogService
    {
        Task<bool> ProductExistsAsync(Guid productId);
        Task<int> GetProductStockAsync(Guid productId);
    }
}
