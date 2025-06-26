using Catalog.Application.Contracts.Persistence;
using Catalog.Application.DTOs.Responses;
using Catalog.Application.Exceptions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Shared.Core.Handlers;

namespace Catalog.Application.Features.Categories.Queries
{
    public class GetCategoryQuery : IRequest<CategoryResponse>
    {
        public Guid Id { get; set; }
    }

    public class GetCategoryHandler : BaseHandler, IRequestHandler<GetCategoryQuery, CategoryResponse>
    {
        private readonly IUnitOfWork _unitOfWork;

        public GetCategoryHandler(
            IUnitOfWork unitOfWork,
            IHttpContextAccessor httpContextAccessor
        ) : base(httpContextAccessor)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<CategoryResponse> Handle(GetCategoryQuery request, CancellationToken cancellationToken)
        {
            var category = await _unitOfWork.CategoryRepository.GetByIdAsync(request.Id);

            if (category == null)
                throw new NotFoundException("Category", request.Id);

            return new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Description = category.Description,
                CreatedBy = category.CreatedBy
            };
        }
    }
}