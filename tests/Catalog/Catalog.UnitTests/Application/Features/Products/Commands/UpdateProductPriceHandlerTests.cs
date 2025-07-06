using Catalog.Application.Features.Products.Commands;
using Catalog.Application.Contracts.Persistence;
using Catalog.Application.Contracts.Messaging;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;
using Xunit.Abstractions;
using System.Security.Claims;
using Shared.Core.Handlers;

namespace Catalog.UnitTests.Application.Features.Products.Commands
{
    public class UpdateProductPriceHandlerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<IProductRepository> _mockProductRepository;
        private readonly Mock<ILogger<UpdateProductPriceHandler>> _mockLogger;
        private readonly Mock<IEventPublisher> _mockEventPublisher;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly UpdateProductPriceHandler _handler;
        private readonly string _testUserId = "test-user-123";

        public UpdateProductPriceHandlerTests(ITestOutputHelper output)
        {
            _output = output;
            _output.WriteLine("🚀 === INICIANDO CONFIGURACIÓN DE TEST ===");

            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockProductRepository = new Mock<IProductRepository>();
            _mockLogger = new Mock<ILogger<UpdateProductPriceHandler>>();
            _mockEventPublisher = new Mock<IEventPublisher>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            _output.WriteLine("✅ Mocks creados correctamente");

            // Setup UnitOfWork
            _mockUnitOfWork.Setup(u => u.ProductRepository)
                          .Returns(_mockProductRepository.Object);
            _output.WriteLine("✅ UnitOfWork configurado");

            // 🎯 CONFIGURACIÓN COMPLETA DEL HTTPCONTEXT PARA TESTING
            SetupHttpContextForTesting();

            // Handler normal
            _handler = new UpdateProductPriceHandler(
                _mockUnitOfWork.Object,
                _mockLogger.Object,
                _mockEventPublisher.Object,
                _mockHttpContextAccessor.Object);

            _output.WriteLine("✅ Handler creado exitosamente");
            _output.WriteLine("🎯 === CONFIGURACIÓN COMPLETADA ===\n");
        }

        private void SetupHttpContextForTesting()
        {
            _output.WriteLine("🔧 === CONFIGURANDO HTTPCONTEXT PARA TESTING ===");

            // 🔧 FORZAR EL AMBIENTE DE TESTING
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");
            _output.WriteLine("✅ Ambiente forzado a: Testing");

            // Crear claims como lo hace tu TestingAuthHandler
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, _testUserId),
                new Claim("user_id", _testUserId),
                new Claim(ClaimTypes.Email, "test@example.com"),
                new Claim(ClaimTypes.Role, "User"),
                new Claim(ClaimTypes.Role, "Admin")
            };

            _output.WriteLine($"✅ Claims creados para usuario: {_testUserId}");
            _output.WriteLine($"   - NameIdentifier: {_testUserId}");
            _output.WriteLine($"   - Email: test@example.com");
            _output.WriteLine($"   - Roles: User, Admin");

            // Crear identidad y principal
            var identity = new ClaimsIdentity(claims, "Testing");
            var principal = new ClaimsPrincipal(identity);

            // Crear HttpContext completo
            var httpContext = new DefaultHttpContext();
            httpContext.User = principal;

            _output.WriteLine("✅ HttpContext creado con usuario autenticado");

            // Configurar el mock
            _mockHttpContextAccessor.Setup(x => x.HttpContext)
                                   .Returns(httpContext);

            _output.WriteLine("✅ HttpContextAccessor configurado");
            _output.WriteLine("🔧 === HTTPCONTEXT CONFIGURADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_ProductExists_ShouldUpdatePriceSuccessfully()
        {
            _output.WriteLine("🧪 === TEST: ACTUALIZACIÓN EXITOSA DE PRECIO ===");

            // Arrange
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var oldPrice = 100.00m;
            var newPrice = 150.00m;

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {productId}");
            _output.WriteLine($"   - CategoryId: {categoryId}");
            _output.WriteLine($"   - Precio Anterior: ${oldPrice}");
            _output.WriteLine($"   - Precio Nuevo: ${newPrice}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            var command = new UpdateProductPriceCommand
            {
                ProductId = productId,
                NewPrice = newPrice
            };

            var existingProduct = new Product
            {
                Id = productId,
                Name = "Test Product",
                Price = oldPrice,
                CategoryId = categoryId,
                CreatedDate = DateTime.UtcNow,
                CreatedBy = "system"
            };

            _output.WriteLine($"✅ Producto existente creado: {existingProduct.Name}");

            // Setup mocks
            _mockProductRepository.Setup(r => r.GetByIdAsync(productId))
                                 .ReturnsAsync(existingProduct);

            _mockUnitOfWork.Setup(u => u.Complete())
                          .ReturnsAsync(1);

            _mockEventPublisher.Setup(e => e.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()))
                             .Returns(Task.CompletedTask);

            _output.WriteLine("✅ Mocks configurados para escenario exitoso");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");
            _output.WriteLine($"   - OldPrice: ${result.OldPrice}");
            _output.WriteLine($"   - NewPrice: ${result.NewPrice}");

            Assert.True(result.Success, $"Expected success but got: {result.Message}");
            Assert.Equal("Precio actualizado correctamente", result.Message);
            Assert.Equal(oldPrice, result.OldPrice);
            Assert.Equal(newPrice, result.NewPrice);

            // Verify que el producto fue actualizado
            Assert.Equal(newPrice, existingProduct.Price);
            _output.WriteLine($"✅ Precio del producto actualizado correctamente: ${existingProduct.Price}");

            // Verify que se llamaron los métodos correctos
            _mockProductRepository.Verify(r => r.GetByIdAsync(productId), Times.Once);
            _mockProductRepository.Verify(r => r.Update(existingProduct), Times.Once);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Once);
            _mockEventPublisher.Verify(e => e.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("✅ Todas las verificaciones de métodos pasaron");

            // Verificar que el evento tiene los datos correctos
            _mockEventPublisher.Verify(e => e.PublishAsync(
                It.Is<object>(evt =>
                    evt.GetType().Name == "ProductPriceChangedEvent"
                ),
                It.IsAny<CancellationToken>()), Times.Once);

            _output.WriteLine("✅ Evento ProductPriceChangedEvent publicado correctamente");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_ProductNotFound_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: PRODUCTO NO ENCONTRADO ===");

            // Arrange
            var productId = Guid.NewGuid();
            var command = new UpdateProductPriceCommand
            {
                ProductId = productId,
                NewPrice = 150.00m
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {productId}");
            _output.WriteLine($"   - Precio Nuevo: ${command.NewPrice}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            _mockProductRepository.Setup(r => r.GetByIdAsync(productId))
                                 .ReturnsAsync((Product)null);

            _output.WriteLine("✅ Mock configurado para retornar producto null");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");

            Assert.False(result.Success);
            Assert.Equal("Producto no encontrado", result.Message);

            _output.WriteLine("✅ Resultado correcto: Producto no encontrado");

            // Verify que NO se intentó actualizar nada
            _mockProductRepository.Verify(r => r.Update(It.IsAny<Product>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Never);
            _mockEventPublisher.Verify(e => e.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Never);

            _output.WriteLine("✅ Verificado: No se ejecutaron operaciones de actualización");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_DatabaseError_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: ERROR DE BASE DE DATOS ===");

            // Arrange
            var productId = Guid.NewGuid();
            var command = new UpdateProductPriceCommand
            {
                ProductId = productId,
                NewPrice = 150.00m
            };

            var existingProduct = new Product
            {
                Id = productId,
                Name = "Test Product",
                Price = 100.00m,
                CategoryId = Guid.NewGuid()
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {productId}");
            _output.WriteLine($"   - Producto: {existingProduct.Name}");
            _output.WriteLine($"   - Precio Actual: ${existingProduct.Price}");
            _output.WriteLine($"   - Precio Nuevo: ${command.NewPrice}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            _mockProductRepository.Setup(r => r.GetByIdAsync(productId))
                                 .ReturnsAsync(existingProduct);

            // 🔥 Simular error en la base de datos
            _mockUnitOfWork.Setup(u => u.Complete())
                          .ThrowsAsync(new Exception("Database connection failed"));

            _output.WriteLine("✅ Mocks configurados para simular error de BD");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER (CON ERROR SIMULADO) ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado (manejó la excepción)");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");

            Assert.False(result.Success);
            Assert.Equal("Error interno al actualizar precio", result.Message);

            _output.WriteLine("✅ Resultado correcto: Error interno manejado");

            // Verify que se intentó hacer update pero falló
            _mockProductRepository.Verify(r => r.Update(existingProduct), Times.Once);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Once);

            _output.WriteLine("✅ Verificado: Se intentó actualizar pero falló");

            // No se debe publicar evento si falla el guardado
            _mockEventPublisher.Verify(e => e.PublishAsync(
                It.IsAny<object>(),
                It.IsAny<CancellationToken>()), Times.Never);

            _output.WriteLine("✅ Verificado: No se publicó evento debido al error");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        // 🧹 CLEANUP: Limpiar variables de entorno después de cada test
        public void Dispose()
        {
            _output.WriteLine("🧹 === LIMPIANDO RECURSOS DE TEST ===");
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", null);
            _output.WriteLine("✅ Variable de entorno limpiada");
            _output.WriteLine("🧹 === CLEANUP COMPLETADO ===\n");
        }
    }
}