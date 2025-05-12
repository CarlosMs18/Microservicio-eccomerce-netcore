namespace Catalog.Application.DTOs.Requests
{
    public class CreateCategoryRequest
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }
}
