using Catalog.Application.Contracts.Persistence;
using Catalog.Application.DTOs.Responses;
using MediatR;

namespace Catalog.Application.Features.Categories.Queries
{
    public class GetAllCategoriesQuery : IRequest<IEnumerable<CategoryListResponse>>
    {
        // Puedes añadir parámetros de paginación/filtrado si los necesitas
        // Ejemplo: public int PageNumber { get; set; } = 1;
    }

    public class GetAllCategoriesHandler : IRequestHandler<GetAllCategoriesQuery, IEnumerable<CategoryListResponse>>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetAllCategoriesHandler(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IEnumerable<CategoryListResponse>> Handle(GetAllCategoriesQuery request, CancellationToken cancellationToken)
        {
            var categories = await _unitOfWork.CategoryRepository.GetAsync(
                orderBy: x => x.OrderBy(c => c.Name),
                disableTracking: true
            );

            return categories.Select(c => new CategoryListResponse
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description
            });
        }
    }
}