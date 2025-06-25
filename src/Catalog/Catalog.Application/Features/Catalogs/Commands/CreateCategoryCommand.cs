using Catalog.Application.Contracts.Persistence;
using Catalog.Application.DTOs.Requests;
using Catalog.Application.DTOs.Responses;
using Catalog.Domain;
using Microsoft.AspNetCore.Http;
using MediatR;
using Shared.Core.Handlers;
using Shared.Core.Extensions;
using Catalog.Application.Exceptions;

namespace Catalog.Application.Features.Catalogs.Commands
{
    public class CreateCategoryCommand : IRequest<CategoryResponse>
    {
        public CreateCategoryRequest Request { get; set; }

        public class CreateCategoryHandler :BaseHandler, IRequestHandler<CreateCategoryCommand, CategoryResponse>
        {
            private readonly IUnitOfWork _unitOfWork;

            public CreateCategoryHandler(
                IUnitOfWork unitOfWork,
                IHttpContextAccessor httpContextAccessor 
            ) : base(httpContextAccessor) 
            {
                _unitOfWork = unitOfWork;
            }

            public async Task<CategoryResponse> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
            {
                if (await _unitOfWork.CategoryRepository.ExistsByNameAsync(request.Request.Name))
                    throw new BadRequestException("La categoría ya existe");

                var category = new Category
                {
                    Name = request.Request.Name.Trim(),
                    Description = request.Request.Description?.Trim()
                };

                category.ApplyAudit(UserId!, isNew: true);
                _unitOfWork.CategoryRepository.Add(category); 
                await _unitOfWork.Complete();


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


}
