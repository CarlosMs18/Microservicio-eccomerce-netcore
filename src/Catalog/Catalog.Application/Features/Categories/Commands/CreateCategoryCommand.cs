using Catalog.Application.Contracts.Persistence;
using Catalog.Application.DTOs.Responses;
using Catalog.Application.Exceptions;
using Catalog.Domain;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Http;
using Shared.Core.Extensions;
using Shared.Core.Handlers;

namespace Catalog.Application.Features.Catalogs.Commands
{
    public class CreateCategoryCommand : IRequest<CategoryResponse>
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
    }

    public class CreateCategoryCommandValidator : AbstractValidator<CreateCategoryCommand>
    {
        public CreateCategoryCommandValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty()
                .WithMessage("El nombre es requerido")
                .MaximumLength(100)
                .WithMessage("El nombre no puede exceder 100 caracteres");

            // Description es opcional, solo validar longitud si tiene valor
            RuleFor(x => x.Description)
                .MaximumLength(500)
                .WithMessage("La descripción no puede exceder 500 caracteres")
                .When(x => !string.IsNullOrEmpty(x.Description));
        }
    }

    public class CreateCategoryHandler : BaseHandler, IRequestHandler<CreateCategoryCommand, CategoryResponse>
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
            // Validación de negocio
            if (await _unitOfWork.CategoryRepository.ExistsByNameAsync(request.Name))
                throw new BadRequestException("La categoría ya existe");

            // Crear entidad
            var category = new Category
            {
                Name = request.Name.Trim(),
                Description = request.Description?.Trim()
            };

            // Aplicar auditoría
            category.ApplyAudit(UserId!, isNew: true);

            // Persistir
            _unitOfWork.CategoryRepository.Add(category);
            await _unitOfWork.Complete();

            // Mapear respuesta
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

