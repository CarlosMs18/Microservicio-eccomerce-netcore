
using Catalog.Application.Features.Products.Commands;
using Catalog.Application.Features.Products.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Catalog.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductController : ControllerBase
    {
        private readonly IMediator _mediator;

        public ProductController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPut("[action]")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.NoContent)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(typeof(string), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<UpdateProductPriceResponse>> UpdateProductPrice([FromBody] UpdateProductPriceCommand command)
        {
            Console.WriteLine("Controaldor;");
            var result = await _mediator.Send(command);

            if (!result.Success)
            {
                if (result.Message.Contains("no encontrado"))
                    return NotFound(result.Message);

                return BadRequest(result.Message);
            }

            return Ok(result);
        }


        [HttpGet("[action]")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<IEnumerable<GetAllProductsResponse>>> GetAllProducts()
        {
            var query = new GetAllProductsQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }

        [HttpGet("[action]/{id}")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.NotFound)]
        public async Task<ActionResult<GetProductByIdResponse>> GetProductById(Guid id)
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
    }
}
