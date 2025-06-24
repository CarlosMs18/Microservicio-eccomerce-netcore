namespace Catalog.IntegrationTests.Builders;

/// <summary>
/// Builder para crear datos de prueba de categorías
/// </summary>
public class CategoryTestDataBuilder
{
    private string _name = "Test Category";
    private string _description = "Test Description";

    public static CategoryTestDataBuilder Create() => new();

    public CategoryTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public CategoryTestDataBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public CategoryTestDataBuilder WithValidData()
    {
        _name = "Electronics";
        _description = "Electronic devices and accessories";
        return this;
    }

    public CategoryTestDataBuilder WithInvalidData()
    {
        _name = ""; // Nombre vacío para provocar error de validación
        _description = "Invalid category";
        return this;
    }

    public CategoryTestDataBuilder WithLongName()
    {
        _name = new string('A', 256); // Nombre muy largo
        _description = "Category with very long name";
        return this;
    }

    public object Build()
    {
        return new
        {
            Name = _name,
            Description = _description
        };
    }

    // Método para crear múltiples categorías
    public static IEnumerable<object> CreateMultiple(int count)
    {
        for (int i = 1; i <= count; i++)
        {
            yield return Create()
                .WithName($"Category {i}")
                .WithDescription($"Description for category {i}")
                .Build();
        }
    }
}