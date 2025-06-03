using Cart.Application.Features.Carts.Commands;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Cart.WebAPI.Controllers
{
    [Route("api/[controller]")]
    public class CartController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CartController(IMediator mediator)
        {
            _mediator = mediator;
        }


        [HttpPost("[action]")]
        [Authorize]
        [ProducesResponseType((int)HttpStatusCode.Created)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult> AddProductToCart([FromBody] AddProductToCartCommand command)
        {
            var result = await _mediator.Send(command);
            return Created("", result);
        }


    }
}