namespace Catalog.IntegrationTests.Builders;

public class CreateCategoryTestDataBuilder
{
    private string _name = "Default Category";
    private string? _description = "Default description";

    // ✅ Método estático para empezar
    public static CreateCategoryTestDataBuilder Create()
        => new CreateCategoryTestDataBuilder();

    // ✅ Datos válidos por defecto
    public CreateCategoryTestDataBuilder WithValidData()
    {
        _name = "Valid Test Category";
        _description = "This is a valid test category";
        return this;
    }

    // ✅ Solo nombre (mínimo requerido)
    public CreateCategoryTestDataBuilder WithMinimalData()
    {
        _name = "Minimal Category";
        _description = null; // Opcional
        return this;
    }

    // ✅ Nombre específico
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