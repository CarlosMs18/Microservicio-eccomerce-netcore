using Cart.Application.Contracts.External;
using Cart.Application.DTos.External;
using Cart.Application.DTos.Requests;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Core.Handlers;

namespace Cart.Application.Features.Carts.Commands
{
    public class AddProductToCartCommand : IRequest<CatalogProductResponse>
    {
        public AddProductToCartRequest Request { get; set; }

        public class AddProductToCartCommandHandler : BaseHandler, IRequestHandler<AddProductToCartCommand, CatalogProductResponse>
        {
            private readonly ICatalogService _catalogService;
            public AddProductToCartCommandHandler(
                IHttpContextAccessor httpContextAccessor,
                ICatalogService catalogService
                ) : base(httpContextAccessor)
            {
                _catalogService = catalogService;
               
            }

            public Task<CatalogProductResponse> Handle(AddProductToCartCommand request, CancellationToken cancellationToken)
            {
                //try
                //{
                //    var productId = request.Request.ProductId;
                //    var requestedQuantity = request.Request.Quantity;

                //    return null;
                //}

                return null;
            }
        }
    }
}
