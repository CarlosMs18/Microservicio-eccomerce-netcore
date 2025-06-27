using Catalog.IntegrationTests.Fixtures;
using Xunit;
using Xunit.Abstractions;

namespace Catalog.IntegrationTests.Common
{
    public class SuperBasicRabbitMQTest : IClassFixture<RabbitMQTestFixture>
    {
        private readonly RabbitMQTestFixture _fixture;
        private readonly ITestOutputHelper _output;

        public SuperBasicRabbitMQTest(RabbitMQTestFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public void RabbitMQ_Container_Should_Be_Running()
        {
            // Act
            var connectionString = _fixture.GetConnectionString();

            // Assert
            Assert.NotNull(_fixture.RabbitMqContainer);
            Assert.NotEmpty(connectionString);

            // Log para que veas que funciona
            _output.WriteLine($"🐰 RabbitMQ está corriendo!");
            _output.WriteLine($"🐰 ConnectionString: {connectionString}");
        }
    }
}