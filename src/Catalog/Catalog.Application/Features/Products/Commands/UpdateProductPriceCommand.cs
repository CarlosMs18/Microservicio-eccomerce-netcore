using Catalog.Application.Contracts.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using Shared.Core.Handlers;
using Shared.Core.Events;
using Shared.Core.Extensions;
using System.ComponentModel.DataAnnotations;
using Catalog.Application.Contracts.Messaging;

namespace Catalog.Application.Features.Products.Commands
{
    // Command Request
    public class UpdateProductPriceCommand : IRequest<UpdateProductPriceResponse>
    {
        public Guid ProductId { get; set; }
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "El precio debe ser mayor a 0")]
        public decimal NewPrice { get; set; }
        // Ya no necesitas UpdatedBy porque se obtiene automáticamente del contexto
    }

    // Response
    public class UpdateProductPriceResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal OldPrice { get; set; }
        public decimal NewPrice { get; set; }
    }

    // Handler
    public class UpdateProductPriceHandler : BaseHandler, IRequestHandler<UpdateProductPriceCommand, UpdateProductPriceResponse>
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<UpdateProductPriceHandler> _logger;
        private readonly IEventPublisher _eventPublisher;

        public UpdateProductPriceHandler(
            IUnitOfWork unitOfWork,
            ILogger<UpdateProductPriceHandler> logger,
            IEventPublisher eventPublisher,
            IHttpContextAccessor httpContextAccessor) : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
            _eventPublisher = eventPublisher;
        }

        public async Task<UpdateProductPriceResponse> Handle(UpdateProductPriceCommand request, CancellationToken cancellationToken)
        {
            try
            {
                Console.WriteLine("Command Update!;");
                // Validar que tenemos el UserId del contexto
                if (string.IsNullOrEmpty(UserId))
                {
                    return new UpdateProductPriceResponse
                    {
                        Success = false,
                        Message = "Usuario no identificado"
                    };
                }

                // Buscar el producto usando el ProductRepository del UnitOfWork
                var product = await _unitOfWork.ProductRepository.GetByIdAsync(request.ProductId);
                if (product == null)
                {
                    return new UpdateProductPriceResponse
                    {
                        Success = false,
                        Message = "Producto no encontrado"
                    };
                }

                // Guardar precio anterior
                var oldPrice = product.Price;

                // Actualizar precio
                product.Price = request.NewPrice;

                // Aplicar auditoría usando la extensión (isNew = false porque es actualización)
                product.ApplyAudit(UserId, isNew: false);

                // Usar el método Update del repositorio (sin Async)
                _unitOfWork.ProductRepository.Update(product);

                // Guardar cambios usando Complete del UnitOfWork
                await _unitOfWork.Complete();

                var priceChangedEvent = new ProductPriceChangedEvent
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    OldPrice = oldPrice,
                    NewPrice = request.NewPrice,
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = UserId,
                    CategoryId = product.CategoryId
                };

                await _eventPublisher.PublishAsync(priceChangedEvent, cancellationToken);

                _logger.LogInformation($"Precio actualizado para producto {request.ProductId}: {oldPrice} -> {request.NewPrice} por usuario {UserId}");

                return new UpdateProductPriceResponse
                {
                    Success = true,
                    Message = "Precio actualizado correctamente",
                    OldPrice = oldPrice,
                    NewPrice = request.NewPrice
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error actualizando precio del producto {request.ProductId}");
                return new UpdateProductPriceResponse
                {
                    Success = false,
                    Message = "Error interno al actualizar precio"
                };
            }
        }
    }
}