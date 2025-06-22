using Catalog.Application.DTOs.Requests;
using Catalog.Application.Features.Catalogs.Commands;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http;
using System.Text;
using Xunit;

namespace Catalog.Tests.Integration
{
    public class CategoryControllerTests : IClassFixture<CustomApplicationFactory<Program>>
    {
        private readonly HttpClient _client;
        private readonly CustomApplicationFactory<Program> _factory;

        public CategoryControllerTests(CustomApplicationFactory<Program> factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task CreateCategory_ValidRequest_ReturnsCreated()
        {
            // Debug: Verificar que estamos en entorno Testing
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            System.Console.WriteLine($"🧪 Environment: {environment}");

            // Arrange
            var createCategoryCommand = new CreateCategoryCommand
            {
                Request = new CreateCategoryRequest
                {
                    Name = "Test Category " + Guid.NewGuid().ToString()[..8], // Nombre único
                    Description = "Test Description"
                }
            };

            var json = JsonConvert.SerializeObject(createCategoryCommand);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            System.Console.WriteLine($"🔍 Sending request: {json}");

            // Act
            var response = await _client.PostAsync("/api/Category/CreateCategory", content);

            // Debug info
            System.Console.WriteLine($"📊 Response Status: {response.StatusCode}");
            var responseContent = await response.Content.ReadAsStringAsync();
            System.Console.WriteLine($"📄 Response Content: {responseContent}");

            if (response.StatusCode != HttpStatusCode.Created)
            {
                throw new Exception($"Test failed. Status: {response.StatusCode}, Content: {responseContent}");
            }

            // Assert
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotEmpty(responseContent);
        }
    }
}