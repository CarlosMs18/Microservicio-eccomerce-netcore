namespace Cart.Application.Contracts.External
{
    public interface ICatalogService
    {
        Task<bool> ProductExistsAsync(int productId);
        Task<int> GetProductStockAsync(int productId);
    }
}
