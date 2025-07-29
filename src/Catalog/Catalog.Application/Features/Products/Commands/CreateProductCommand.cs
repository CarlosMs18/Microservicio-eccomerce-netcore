using Catalog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Shared.Core.Handlers;
using Shared.Core.Extensions;
using System.ComponentModel.DataAnnotations;
using Catalog.Domain;

namespace Catalog.Application.Features.Products.Commands
{
    public class CreateProductCommand : IRequest<CreateProductResponse>
    {
        [Required(ErrorMessage = "El nombre del producto es requerido")]
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "La descripción del producto es requerida")]
        [StringLength(1000, ErrorMessage = "La descripción no puede exceder 1000 caracteres")]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "La categoría es requerida")]
        public Guid CategoryId { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El stock no puede ser negativo")]
        public int Stock { get; set; } = 0;

        public bool IsActive { get; set; } = true;
    }

    // Response
    public class CreateProductResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public Guid? ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
    }

    // Handler
    public class CreateProductHandler : BaseHandler, IRequestHandler<CreateProductCommand, CreateProductResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CreateProductHandler> _logger;

        public CreateProductHandler(
            IUnitOfWork unitOfWork,
            ILogger<CreateProductHandler> logger,
            IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<CreateProductResponse> Handle(CreateProductCommand request, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("Command CreateProduct!");

                // Validar que tenemos el UserId del contexto
                if (string.IsNullOrEmpty(UserId))
                {
                    return new CreateProductResponse
                    {
                        Success = false,
                        Message = "Usuario no identificado"
                    };
                }

              
                var category = await _unitOfWork.CategoryRepository.GetByIdAsync(request.CategoryId);
                if (category == null)
                {
                    return new CreateProductResponse
                    {
                        Success = false,
                        Message = "La categoría especificada no existe"
                    };
                }

                // Verificar que no existe un producto con el mismo nombre
                var existingProduct = await _unitOfWork.ProductRepository.GetByNameAsync(request.Name);
                if (existingProduct != null)
                {
                    return new CreateProductResponse
                    {
                        Success = false,
                        Message = "Ya existe un producto con ese nombre"
                    };
                }

                // Crear el nuevo producto
                var product = new Product
                {
                    Id = Guid.NewGuid(),
                    Name = request.Name,
                    Description = request.Description,
                    Price = request.Price,
                    CategoryId = request.CategoryId,
                    Stock = request.Stock,
                    IsActive = request.IsActive
                };

                // Aplicar auditoría usando la extensión (isNew = true porque es creación)
                product.ApplyAudit(UserId, isNew: true);

                // Agregar el producto usando el método Add (sin guardar aún)
                _unitOfWork.ProductRepository.Add(product);

                // Guardar cambios usando Complete del UnitOfWork
                await _unitOfWork.Complete();

                _logger.LogInformation($"Producto creado exitosamente: {product.Id} - {product.Name} por usuario {UserId}");

                return new CreateProductResponse
                {
                    Success = true,
                    Message = "Producto creado correctamente",
                    ProductId = product.Id,
                    ProductName = product.Name
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creando producto: {request.Name}");
                return new CreateProductResponse
                {
                    Success = false,
                    Message = "Error interno al crear el producto"
                };
            }
        }
    }
}