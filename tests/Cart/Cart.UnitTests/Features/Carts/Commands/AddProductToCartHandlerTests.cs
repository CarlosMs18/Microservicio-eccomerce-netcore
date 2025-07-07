
namespace Cart.UnitTests.Application.Features.Carts.Commands
{
    public class AddProductToCartHandlerTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICartRepository> _mockCartRepository;
        private readonly Mock<ICartItemRepository> _mockCartItemRepository;
        private readonly Mock<ICatalogService> _mockCatalogService;
        private readonly Mock<ILogger<AddProductToCartCommand.AddProductToCartCommandHandler>> _mockLogger;
        private readonly Mock<IHttpContextAccessor> _mockHttpContextAccessor;
        private readonly AddProductToCartCommand.AddProductToCartCommandHandler _handler;
        private readonly string _testUserId = "test-user-123";
        private readonly Guid _testProductId = Guid.NewGuid();
        private readonly Guid _testCartId = Guid.NewGuid();

        public AddProductToCartHandlerTests(ITestOutputHelper output)
        {
            _output = output;
            _output.WriteLine("🚀 === INICIANDO CONFIGURACIÓN DE TEST CART ===");

            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCartRepository = new Mock<ICartRepository>();
            _mockCartItemRepository = new Mock<ICartItemRepository>();
            _mockCatalogService = new Mock<ICatalogService>();
            _mockLogger = new Mock<ILogger<AddProductToCartCommand.AddProductToCartCommandHandler>>();
            _mockHttpContextAccessor = new Mock<IHttpContextAccessor>();

            _output.WriteLine("✅ Mocks creados correctamente");

            // Setup UnitOfWork
            _mockUnitOfWork.Setup(u => u.CartRepository)
                          .Returns(_mockCartRepository.Object);
            _mockUnitOfWork.Setup(u => u.CartItemRepository)
                          .Returns(_mockCartItemRepository.Object);
            _output.WriteLine("✅ UnitOfWork configurado");

            // 🎯 CONFIGURACIÓN COMPLETA DEL HTTPCONTEXT PARA TESTING
            SetupHttpContextForTesting();

            // Handler
            _handler = new AddProductToCartCommand.AddProductToCartCommandHandler(
                _mockHttpContextAccessor.Object,
                _mockCatalogService.Object,
                _mockUnitOfWork.Object,
                _mockLogger.Object);

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
                new Claim(ClaimTypes.Role, "User")
            };

            _output.WriteLine($"✅ Claims creados para usuario: {_testUserId}");
            _output.WriteLine($"   - NameIdentifier: {_testUserId}");
            _output.WriteLine($"   - Email: test@example.com");
            _output.WriteLine($"   - Role: User");

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
        public async Task Handle_NewCartAndProduct_ShouldAddProductSuccessfully()
        {
            _output.WriteLine("🧪 === TEST: AGREGAR PRODUCTO A NUEVO CARRITO ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = _testProductId.ToString(),
                Quantity = 2
            };

            var productDetails = new ProductDetailsDto
            {
                Name = "Test Product",
                Description = "Test Description",
                Price = 100.00m,
                Images = new List<ProductImageDto>
                {
                    new ProductImageDto { ImageUrl = "https://example.com/image.jpg" }
                },
                Category = new CategoryDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Category"
                }
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {_testProductId}");
            _output.WriteLine($"   - Cantidad: {command.Quantity}");
            _output.WriteLine($"   - Producto: {productDetails.Name}");
            _output.WriteLine($"   - Precio: ${productDetails.Price}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            // Setup mocks para escenario exitoso
            _mockCatalogService.Setup(s => s.ProductExistsAsync(_testProductId))
                              .ReturnsAsync(true);
            _mockCatalogService.Setup(s => s.GetProductStockAsync(_testProductId))
                              .ReturnsAsync(10); // Stock suficiente
            _mockCatalogService.Setup(s => s.GetProductDetailsAsync(_testProductId))
                              .ReturnsAsync(productDetails);

            // No existe carrito previo
            _mockCartRepository.Setup(r => r.GetCartByUserIdAsync(_testUserId))
                              .ReturnsAsync((Domain.Cart)null);

            // No existe item previo
            _mockCartItemRepository.Setup(r => r.GetByCartAndProductAsync(It.IsAny<Guid>(), _testProductId))
                                  .ReturnsAsync((CartItem)null);

            _mockUnitOfWork.Setup(u => u.Complete())
                          .ReturnsAsync(1);

            _output.WriteLine("✅ Mocks configurados para escenario exitoso");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");
            _output.WriteLine($"   - ProductName: {result.ProductName}");
            _output.WriteLine($"   - Subtotal: ${result.Subtotal}");

            Assert.True(result.Success);
            Assert.Equal("Producto agregado al carrito exitosamente", result.Message);
            Assert.Equal(_testProductId, result.ProductId);
            Assert.Equal(productDetails.Name, result.ProductName);
            Assert.Equal(command.Quantity, result.RequestedQuantity);
            Assert.Equal(productDetails.Price * command.Quantity, result.Subtotal);

            // Verify que se creó nuevo carrito
            _mockCartRepository.Verify(r => r.Add(It.IsAny<Domain.Cart>()), Times.Once);
            _mockCartItemRepository.Verify(r => r.Add(It.IsAny<CartItem>()), Times.Once);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Once);

            _output.WriteLine("✅ Verificaciones exitosas: Nuevo carrito y producto creados");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_ExistingCartAndProduct_ShouldUpdateQuantity()
        {
            _output.WriteLine("🧪 === TEST: ACTUALIZAR CANTIDAD DE PRODUCTO EXISTENTE ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = _testProductId.ToString(),
                Quantity = 3
            };

            var existingCart = new Domain.Cart
            {
                Id = _testCartId,
                Items = new List<CartItem>()
            };

            var existingCartItem = new CartItem
            {
                Id = Guid.NewGuid(),
                CartId = _testCartId,
                ProductId = _testProductId,
                Quantity = 2, // Cantidad actual
                Price = 100.00m
            };

            var productDetails = new ProductDetailsDto
            {
                Name = "Test Product",
                Description = "Test Description",
                Price = 100.00m,
                Images = new List<ProductImageDto>
                {
                    new ProductImageDto { ImageUrl = "https://example.com/image.jpg" }
                },
                Category = new CategoryDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Category"
                }
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {_testProductId}");
            _output.WriteLine($"   - Cantidad a agregar: {command.Quantity}");
            _output.WriteLine($"   - Cantidad actual: {existingCartItem.Quantity}");
            _output.WriteLine($"   - Cantidad final esperada: {existingCartItem.Quantity + command.Quantity}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            // Setup mocks
            _mockCatalogService.Setup(s => s.ProductExistsAsync(_testProductId))
                              .ReturnsAsync(true);
            _mockCatalogService.Setup(s => s.GetProductStockAsync(_testProductId))
                              .ReturnsAsync(10);
            _mockCatalogService.Setup(s => s.GetProductDetailsAsync(_testProductId))
                              .ReturnsAsync(productDetails);

            _mockCartRepository.Setup(r => r.GetCartByUserIdAsync(_testUserId))
                              .ReturnsAsync(existingCart);

            _mockCartItemRepository.Setup(r => r.GetByCartAndProductAsync(_testCartId, _testProductId))
                                  .ReturnsAsync(existingCartItem);

            _mockUnitOfWork.Setup(u => u.Complete())
                          .ReturnsAsync(1);

            _output.WriteLine("✅ Mocks configurados para producto existente");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Cantidad final en item: {existingCartItem.Quantity}");

            Assert.True(result.Success);
            Assert.Equal(5, existingCartItem.Quantity); // 2 + 3 = 5

            // Verify que se actualizó el item existente
            _mockCartItemRepository.Verify(r => r.Update(existingCartItem), Times.Once);
            _mockCartRepository.Verify(r => r.Update(existingCart), Times.Once);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Once);

            // NO se debe crear nuevo item
            _mockCartItemRepository.Verify(r => r.Add(It.IsAny<CartItem>()), Times.Never);

            _output.WriteLine("✅ Verificaciones exitosas: Cantidad actualizada correctamente");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_UserNotAuthenticated_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: USUARIO NO AUTENTICADO ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = _testProductId.ToString(),
                Quantity = 1
            };

            // Setup HttpContext sin usuario autenticado
            var httpContext = new DefaultHttpContext();
            httpContext.User = new ClaimsPrincipal(); // Sin claims

            _mockHttpContextAccessor.Setup(x => x.HttpContext)
                                   .Returns(httpContext);

            _output.WriteLine("✅ HttpContext configurado sin autenticación");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");

            Assert.False(result.Success);
            Assert.Equal("Usuario no autenticado", result.Message);

            // No se debe llamar a ningún servicio
            _mockCatalogService.Verify(s => s.ProductExistsAsync(It.IsAny<Guid>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Never);

            _output.WriteLine("✅ Verificaciones exitosas: Usuario no autenticado manejado correctamente");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_ProductNotExists_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: PRODUCTO NO EXISTE ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = _testProductId.ToString(),
                Quantity = 1
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {_testProductId}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            _mockCatalogService.Setup(s => s.ProductExistsAsync(_testProductId))
                              .ReturnsAsync(false);

            _output.WriteLine("✅ Mock configurado para producto inexistente");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");

            Assert.False(result.Success);
            Assert.Contains("no existe o no está disponible", result.Message);

            // No se debe intentar obtener stock ni detalles
            _mockCatalogService.Verify(s => s.GetProductStockAsync(It.IsAny<Guid>()), Times.Never);
            _mockCatalogService.Verify(s => s.GetProductDetailsAsync(It.IsAny<Guid>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Never);

            _output.WriteLine("✅ Verificaciones exitosas: Producto inexistente manejado correctamente");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_InsufficientStock_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: STOCK INSUFICIENTE ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = _testProductId.ToString(),
                Quantity = 10 // Solicitamos 10
            };

            var availableStock = 5; // Solo hay 5 disponibles

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {_testProductId}");
            _output.WriteLine($"   - Cantidad solicitada: {command.Quantity}");
            _output.WriteLine($"   - Stock disponible: {availableStock}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            _mockCatalogService.Setup(s => s.ProductExistsAsync(_testProductId))
                              .ReturnsAsync(true);
            _mockCatalogService.Setup(s => s.GetProductStockAsync(_testProductId))
                              .ReturnsAsync(availableStock);

            _output.WriteLine("✅ Mocks configurados para stock insuficiente");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");
            _output.WriteLine($"   - AvailableStock: {result.AvailableStock}");
            _output.WriteLine($"   - RequestedQuantity: {result.RequestedQuantity}");

            Assert.False(result.Success);
            Assert.Contains("Stock insuficiente", result.Message);
            Assert.Equal(availableStock, result.AvailableStock);
            Assert.Equal(command.Quantity, result.RequestedQuantity);

            // No se debe obtener detalles del producto ni hacer cambios
            _mockCatalogService.Verify(s => s.GetProductDetailsAsync(It.IsAny<Guid>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Never);

            _output.WriteLine("✅ Verificaciones exitosas: Stock insuficiente manejado correctamente");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_InvalidProductId_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: PRODUCT ID INVÁLIDO ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = "invalid-guid-string",
                Quantity = 1
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId inválido: {command.ProductId}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            // Act
            _output.WriteLine("🎯 === EJECUTANDO HANDLER ===");
            var result = await _handler.Handle(command, CancellationToken.None);
            _output.WriteLine("✅ Handler ejecutado");

            // Assert
            _output.WriteLine("🔍 === VERIFICANDO RESULTADOS ===");
            _output.WriteLine($"   - Success: {result.Success}");
            _output.WriteLine($"   - Message: {result.Message}");

            Assert.False(result.Success);
            Assert.Equal("El identificador del producto no es válido", result.Message);

            // No se debe llamar a ningún servicio
            _mockCatalogService.Verify(s => s.ProductExistsAsync(It.IsAny<Guid>()), Times.Never);
            _mockUnitOfWork.Verify(u => u.Complete(), Times.Never);

            _output.WriteLine("✅ Verificaciones exitosas: GUID inválido manejado correctamente");
            _output.WriteLine("🎉 === TEST COMPLETADO EXITOSAMENTE ===\n");
        }

        [Fact]
        public async Task Handle_DatabaseError_ShouldReturnFailure()
        {
            _output.WriteLine("🧪 === TEST: ERROR DE BASE DE DATOS ===");

            // Arrange
            var command = new AddProductToCartCommand
            {
                ProductId = _testProductId.ToString(),
                Quantity = 1
            };

            var productDetails = new ProductDetailsDto
            {
                Name = "Test Product",
                Description = "Test Description",
                Price = 100.00m,
                Images = new List<ProductImageDto>(),
                Category = new CategoryDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Test Category"
                }
            };

            _output.WriteLine($"📋 DATOS DEL TEST:");
            _output.WriteLine($"   - ProductId: {_testProductId}");
            _output.WriteLine($"   - Usuario: {_testUserId}");

            // Setup mocks exitosos hasta llegar a la BD
            _mockCatalogService.Setup(s => s.ProductExistsAsync(_testProductId))
                              .ReturnsAsync(true);
            _mockCatalogService.Setup(s => s.GetProductStockAsync(_testProductId))
                              .ReturnsAsync(10);
            _mockCatalogService.Setup(s => s.GetProductDetailsAsync(_testProductId))
                              .ReturnsAsync(productDetails);

            _mockCartRepository.Setup(r => r.GetCartByUserIdAsync(_testUserId))
                              .ReturnsAsync((Domain.Cart)null);

            _mockCartItemRepository.Setup(r => r.GetByCartAndProductAsync(It.IsAny<Guid>(), _testProductId))
                                  .ReturnsAsync((CartItem)null);

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
            Assert.Equal("Error interno del servidor al procesar la solicitud", result.Message);

            _output.WriteLine("✅ Resultado correcto: Error interno manejado");
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