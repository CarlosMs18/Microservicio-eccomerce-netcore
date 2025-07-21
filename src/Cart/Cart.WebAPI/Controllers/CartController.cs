using Cart.Application.Features.Carts.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Infrastructure.Interfaces;
using System.Diagnostics;
using System.Net;

namespace Cart.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CartController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IMetricsService _metricsService;

        public CartController(IMediator mediator, IMetricsService metricsService)
        {
            _mediator = mediator;
            _metricsService = metricsService;
        }

        [HttpPost("[action]")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult> AddProductToCart([FromBody] AddProductToCartCommand command)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/cart/addproducttocart";
            var method = "POST";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var result = await _mediator.Send(command);
                return Created("", result);
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }
    }
}