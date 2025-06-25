using Catalog.IntegrationTests.Fixtures;
using Xunit;

namespace Catalog.IntegrationTests.Common;

// ✅ Collection Definition - Una sola instancia del factory para toda la colección
[CollectionDefinition("Sequential")]
public class SequentialTestCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
{
    // Esta clase existe solo para definir la collection fixture
    // No necesita implementación, solo la definición
}