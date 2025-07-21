using Catalog.Application.Features.Products.Commands;
using Catalog.Application.Features.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Shared.Infrastructure.Interfaces;
using System.Diagnostics;
using System.Net;

namespace Catalog.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IMetricsService _metricsService;

        public ProductController(IMediator mediator, IMetricsService metricsService)
        {
            _mediator = mediator;
            _metricsService = metricsService;
        }

        [HttpPut("[action]")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UpdateProductPriceResponse>> UpdateProductPrice([FromBody] UpdateProductPriceCommand command)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/product/updateproductprice";
            var method = "PUT";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                Console.WriteLine("Controlador UpdateProductPrice");
                var result = await _mediator.Send(command);

                if (!result.Success)
                {
                    if (result.Message.Contains("no encontrado"))
                        return NotFound(result.Message);
                    return BadRequest(result.Message);
                }

                return Ok(result);
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpGet("[action]")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<IEnumerable<GetAllProductsResponse>>> GetAllProducts()
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/product/getallproducts";
            var method = "GET";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var query = new GetAllProductsQuery();
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpGet("[action]/{id}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<GetProductByIdResponse>> GetProductById(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/product/getproductbyid/{id}";
            var method = "GET";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var query = new GetProductByIdQuery { ProductId = id };
                var result = await _mediator.Send(query);

                if (!result.Success)
                {
                    if (result.Message.Contains("no encontrado"))
                        return NotFound(result.Message);
                    return BadRequest(result.Message);
                }

                return Ok(result);
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