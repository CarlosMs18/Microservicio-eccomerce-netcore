namespace Catalog.IntegrationTests.Builders;

public class CreateCategoryTestDataBuilder
{
    private string _name = $"Default Category {Guid.NewGuid().ToString()[..8]}";
    private string? _description = "Default description";

    // ✅ Método estático para empezar
    public static CreateCategoryTestDataBuilder Create()
        => new CreateCategoryTestDataBuilder();

    // ✅ Datos válidos por defecto CON DATOS DINÁMICOS
    public CreateCategoryTestDataBuilder WithValidData()
    {
        _name = $"Valid Test Category {DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid().ToString()[..4]}";
        _description = "This is a valid test category";
        return this;
    }

    // ✅ Solo nombre (mínimo requerido) CON DATOS DINÁMICOS
    public CreateCategoryTestDataBuilder WithMinimalData()
    {
        _name = $"Minimal Category {Guid.NewGuid().ToString()[..8]}";
        _description = null; // Opcional
        return this;
    }

    // ✅ Nombre específico (para casos donde SÍ quieres un nombre fijo)
    public CreateCategoryTestDataBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    // ✅ Descripción específica
    public CreateCategoryTestDataBuilder WithDescription(string? description)
    {
        _description = description;
        return this;
    }

    // ✅ Para casos de error - nombre vacío
    public CreateCategoryTestDataBuilder WithEmptyName()
    {
        _name = "";
        return this;
    }

    // ✅ Para casos de error - nombre muy largo
    public CreateCategoryTestDataBuilder WithTooLongName()
    {
        _name = new string('A', 101); // Tu validación es max 100
        return this;
    }

    // ✅ Para casos de error - descripción muy larga
    public CreateCategoryTestDataBuilder WithTooLongDescription()
    {
        _description = new string('B', 501); // Tu validación es max 500
        return this;
    }

    // ✅ Para casos específicos donde necesitas nombre específico (ej: test de duplicados)
    public CreateCategoryTestDataBuilder WithSpecificName(string name)
    {
        _name = name;
        return this;
    }

    // ✅ Construir el objeto final
    public object Build()
    {
        return new
        {
            Name = _name,
            Description = _description
        };
    }
}
