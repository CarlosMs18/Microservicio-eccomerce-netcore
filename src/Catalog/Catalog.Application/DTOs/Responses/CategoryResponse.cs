namespace Catalog.Application.DTOs.Responses
{
    public class CategoryResponse
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string CreatedBy { get; set; }
    }
}
