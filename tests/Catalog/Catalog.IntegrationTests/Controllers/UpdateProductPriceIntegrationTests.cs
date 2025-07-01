using Catalog.IntegrationTests.Common;
using Catalog.IntegrationTests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;
using Catalog.Application.Features.Products.Commands;
using Microsoft.EntityFrameworkCore;
using Catalog.Infrastructure.Persistence;
using Catalog.Domain;
using Xunit.Abstractions;
using RabbitMQ.Client;
using System.Text;

namespace Catalog.IntegrationTests.Controllers;

[Collection("Sequential")]
public class UpdateProductPriceIntegrationTests : BaseIntegrationTest
{
    private readonly ITestOutputHelper _output;
    private const string EXCHANGE_NAME = "catalog.events"; 
    private const string QUEUE_NAME = "catalog.product.updated.test";
    private const string ROUTING_KEY = "catalog.product.updated";

    public UpdateProductPriceIntegrationTests(
        CustomWebApplicationFactory<Program> factory,
        ITestOutputHelper output) : base(factory)
    {
        _output = output;
    }

    [Fact]
    public async Task UpdateProductPrice_WithValidData_ShouldUpdatePriceAndPublishEvent()
    {
        // Arrange - Crear un producto de prueba
        var testProduct = await CreateTestProductAsync();
        var newPrice = 150.99m;

        var command = new UpdateProductPriceCommand
        {
            ProductId = testProduct.Id,
            NewPrice = newPrice
        };

        // 🔧 SETUP: Configurar RabbitMQ antes del test
        await SetupRabbitMQForTest();
        _output.WriteLine("🔧 RabbitMQ configurado para el test");

        // Crear cliente con usuario autenticado
        var authenticatedClient = CreateClientWithTestUser("test-user-123", "test@example.com");

        _output.WriteLine($"📡 Iniciando test para ProductId: {testProduct.Id}");
        _output.WriteLine($"💰 Precio original: {testProduct.Price} -> Nuevo precio: {newPrice}");

        // Act - Llamar al endpoint
        var response = await authenticatedClient.PutAsJsonAsync(
            "/api/Product/UpdateProductPrice",
            command,
            JsonOptions);

        // Assert - Verificar respuesta HTTP
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var responseContent = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<UpdateProductPriceResponse>(responseContent, JsonOptions);

        Assert.NotNull(result);
        Assert.True(result.Success);
        Assert.Equal("Precio actualizado correctamente", result.Message);
        Assert.Equal(99.99m, result.OldPrice);
        Assert.Equal(newPrice, result.NewPrice);

        _output.WriteLine("✅ Respuesta HTTP validada correctamente");

        // Verificar que el precio se actualizó en la base de datos
        await VerifyProductPriceUpdatedInDatabase(testProduct.Id, newPrice);
        _output.WriteLine("✅ Precio actualizado en base de datos verificado");

        // 🎯 VERIFICAR MENSAJE EN RABBITMQ
        _output.WriteLine("🐰 Esperando evento en RabbitMQ...");
        await Task.Delay(3000); // Dar más tiempo para procesamiento asíncrono

        var messages = await GetMessagesFromRabbitMQQueue(QUEUE_NAME);

        Assert.Single(messages); // Debe haber exactamente 1 mensaje
        _output.WriteLine($"🎯 Mensaje encontrado en RabbitMQ: {messages.Count}");

        // Verificar el contenido del evento
        var eventJson = messages.First();
        _output.WriteLine($"📄 JSON del evento: {eventJson}");

        var eventData = JsonSerializer.Deserialize<ProductPriceChangedEvent>(eventJson, JsonOptions);

        Assert.NotNull(eventData);
        Assert.Equal(testProduct.Id, eventData.ProductId);
        Assert.Equal("Test Product", eventData.ProductName);
        Assert.Equal(99.99m, eventData.OldPrice);
        Assert.Equal(newPrice, eventData.NewPrice);
        Assert.Equal("test-user-123", eventData.ChangedBy);

        _output.WriteLine("✅ Contenido del evento RabbitMQ validado correctamente");
        _output.WriteLine($"🎉 Test completado exitosamente!");
        _output.WriteLine($"   📦 ProductId: {eventData.ProductId}");
        _output.WriteLine($"   💰 Precio: {eventData.OldPrice} -> {eventData.NewPrice}");
        _output.WriteLine($"   👤 Usuario: {eventData.ChangedBy}");
        _output.WriteLine($"   🕐 Fecha: {eventData.ChangedAt}");
    }

    // 🔧 NUEVO: Setup completo de RabbitMQ
    private async Task SetupRabbitMQForTest()
    {
        try
        {
            using var connection = CreateRabbitMQConnection();
            using var channel = connection.CreateModel();

            // 🧹 CLEANUP PREVIO (esto es la clave)
            try
            {
                channel.QueueDelete(QUEUE_NAME, ifUnused: false, ifEmpty: false);
            }
            catch { /* Ignorar si no existe */ }

            // 🏗️ CONFIGURACIÓN FRESCA
            channel.ExchangeDeclare(
                exchange: EXCHANGE_NAME,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false);

            channel.QueueDeclare(
                queue: QUEUE_NAME,
                durable: true,
                exclusive: false,
                autoDelete: false);

            channel.QueueBind(
                queue: QUEUE_NAME,
                exchange: EXCHANGE_NAME,
                routingKey: ROUTING_KEY);

            // 🧹 LIMPIAR MENSAJES (súper importante)
            channel.QueuePurge(QUEUE_NAME);

            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error en setup: {ex.Message}");
            throw;
        }
    }


    // 🐰 HELPER METHODS PARA RABBITMQ - CORREGIDOS
    private async Task<List<string>> GetMessagesFromRabbitMQQueue(string queueName)
    {
        var messages = new List<string>();

        try
        {
            using var connection = CreateRabbitMQConnection();
            using var channel = connection.CreateModel();

            // Consumir todos los mensajes disponibles
            int messageCount = 0;
            while (true)
            {
                var result = channel.BasicGet(queueName, autoAck: true);
                if (result == null) break; // No hay más mensajes

                var message = Encoding.UTF8.GetString(result.Body.ToArray());
                messages.Add(message);
                messageCount++;

                _output.WriteLine($"📨 Mensaje {messageCount} obtenido de la cola");

                // Protección contra bucle infinito
                if (messageCount > 10) break;
            }

            _output.WriteLine($"📊 Total de mensajes obtenidos: {messages.Count}");
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error al obtener mensajes: {ex.Message}");
            throw;
        }

        return messages;
    }

    private IConnection CreateRabbitMQConnection()
    {
        var factory = new ConnectionFactory()
        {
            HostName = "localhost",
            Port = 5672,
            UserName = "guest",
            Password = "guest",
            VirtualHost = "/",
            RequestedConnectionTimeout = TimeSpan.FromSeconds(10),
            RequestedHeartbeat = TimeSpan.FromSeconds(10)
        };

        try
        {
            var connection = factory.CreateConnection();
            _output.WriteLine("🔗 Conexión a RabbitMQ establecida correctamente");
            return connection;
        }
        catch (Exception ex)
        {
            _output.WriteLine($"❌ Error conectando a RabbitMQ: {ex.Message}");
            _output.WriteLine("💡 Asegúrate de que RabbitMQ esté corriendo en Docker:");
            _output.WriteLine("   docker run -d --name rabbitmq-local -p 5672:5672 -p 15672:15672 rabbitmq:3-management");
            throw;
        }
    }

    // HELPER METHODS ORIGINALES
    private async Task<Product> CreateTestProductAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        // Crear una categoría de prueba primero
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Category",
            Description = "Category for testing",
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "test-system"
        };

        context.Categories.Add(category);

        // Crear el producto de prueba
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = "Test Product",
            Description = "Product for testing price update",
            Price = 99.99m,
            CategoryId = category.Id,
            CreatedDate = DateTime.UtcNow,
            CreatedBy = "test-system"
        };

        context.Products.Add(product);
        await context.SaveChangesAsync();

        _output.WriteLine($"🏭 Producto de prueba creado: {product.Id}");
        return product;
    }

    private async Task VerifyProductPriceUpdatedInDatabase(Guid productId, decimal expectedPrice)
    {
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        var updatedProduct = await context.Products
            .FirstOrDefaultAsync(p => p.Id == productId);

        Assert.NotNull(updatedProduct);
        Assert.Equal(expectedPrice, updatedProduct.Price);
    }
}

// 🎯 CLASE PARA DESERIALIZAR EL EVENTO
public class ProductPriceChangedEvent
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public DateTime ChangedAt { get; set; }
    public string ChangedBy { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string EventType { get; set; } = string.Empty;
}