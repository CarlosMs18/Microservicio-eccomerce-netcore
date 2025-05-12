using Catalog.Application.DTOs.Responses;
using Catalog.Application.Features.Catalogs.Commands;
using Catalog.Application.Features.Categories.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace Catalog.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly IMediator _mediator;

        public CategoryController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpPost("[action]")]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.BadRequest)]
        public async Task<ActionResult> CreateCategory([FromBody] CreateCategoryCommand command)
        {
            return Ok(await _mediator.Send(command));
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CategoryListResponse>>> GetAll()
        {
            var query = new GetAllCategoriesQuery();
            var result = await _mediator.Send(query);
            return Ok(result);
        }


    }
}
