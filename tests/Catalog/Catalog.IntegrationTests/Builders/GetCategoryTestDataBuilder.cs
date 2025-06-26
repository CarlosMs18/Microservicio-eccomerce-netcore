namespace Catalog.IntegrationTests.Builders;

public class GetCategoryTestDataBuilder
{
    private Guid _id = Guid.NewGuid();

    public static GetCategoryTestDataBuilder Create()
        => new GetCategoryTestDataBuilder();

    public GetCategoryTestDataBuilder WithValidId(Guid id)
    {
        _id = id;
        return this;
    }

    public GetCategoryTestDataBuilder WithInvalidId()
    {
        _id = Guid.NewGuid(); // ID que no existe
        return this;
    }

    public GetCategoryTestDataBuilder WithEmptyId()
    {
        _id = Guid.Empty;
        return this;
    }

    public Guid Build()
    {
        return _id;
    }
}