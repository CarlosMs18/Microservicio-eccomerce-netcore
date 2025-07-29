using Catalog.Application.DTOs.Responses;
using Catalog.Application.Exceptions;
using Catalog.Application.Features.Catalogs.Commands;
using Catalog.Application.Features.Categories.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shared.Infrastructure.Interfaces;
using System.Diagnostics;
using System.Net;

namespace Catalog.WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoryController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly IMetricsService _metricsService;

        public CategoryController(IMediator mediator, IMetricsService metricsService)
        {
            _mediator = mediator;
            _metricsService = metricsService;
        }

        [HttpPost]
        [Authorize]
        [ProducesResponseType(typeof(CategoryResponse), (int)HttpStatusCode.Created)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.BadRequest)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.InternalServerError)]
        public async Task<ActionResult<CategoryResponse>> CreateCategory([FromBody] CreateCategoryCommand command)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/category";
            var method = "POST";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                // 🎯 ¡Súper limpio! El middleware maneja las excepciones
                var result = await _mediator.Send(command);
                return CreatedAtAction(
                    nameof(GetCategory),
                    new { id = result.Id },
                    result
                );
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpGet("{id:guid}")]
        [Authorize]
        [ProducesResponseType(typeof(CategoryResponse), (int)HttpStatusCode.OK)]
        [ProducesResponseType(typeof(ProblemDetails), (int)HttpStatusCode.NotFound)]
        [ProducesResponseType((int)HttpStatusCode.Unauthorized)]
        public async Task<ActionResult<CategoryResponse>> GetCategory(Guid id)
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/category/{id}";
            var method = "GET";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var query = new GetCategoryQuery { Id = id };
                var result = await _mediator.Send(query);
                return Ok(result);
            }
            catch (NotFoundException ex)
            {
                return NotFound(new ProblemDetails
                {
                    Title = "Categoría no encontrada",
                    Detail = ex.Message,
                    Status = (int)HttpStatusCode.NotFound,
                    Instance = HttpContext.Request.Path
                });
            }
            catch (Exception ex)
            {
                return StatusCode((int)HttpStatusCode.InternalServerError, new ProblemDetails
                {
                    Title = "Error interno del servidor",
                    Detail = "Ha ocurrido un error interno. Por favor, intente más tarde.",
                    Status = (int)HttpStatusCode.InternalServerError,
                    Instance = HttpContext.Request.Path
                });
            }
            finally
            {
                stopwatch.Stop();
                _metricsService.RecordRequestDuration(endpoint, stopwatch.Elapsed.TotalSeconds);
                _metricsService.UpdateActiveConnections(-1);
            }
        }

        [HttpGet("GetAll")]
        [ProducesResponseType(typeof(IEnumerable<CategoryListResponse>), (int)HttpStatusCode.OK)]
        public async Task<ActionResult<IEnumerable<CategoryListResponse>>> GetAll()
        {
            var stopwatch = Stopwatch.StartNew();
            var endpoint = "api/category/getall";
            var method = "GET";

            _metricsService.UpdateActiveConnections(1);
            _metricsService.IncrementRequestCount(endpoint, method);

            try
            {
                var query = new GetAllCategoriesQuery();
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
    }
}