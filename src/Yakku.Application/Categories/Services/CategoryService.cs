using System.Text;
using FluentValidation;
using Yakku.Application.Categories.DTOs;
using Yakku.Application.Categories.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;

namespace Yakku.Application.Categories.Services
{
    public class CategoryService : ICategoryService
    {
        private const int MaxSlugLength = 100;

        private readonly ICategoryRepository _categories;
        private readonly IValidator<CreateCategoryRequest> _createValidator;

        public CategoryService(
            ICategoryRepository categories,
            IValidator<CreateCategoryRequest> createValidator)
        {
            _categories = categories;
            _createValidator = createValidator;
        }

        public async Task<CategoryResponse> CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

            var name = request.Name.Trim();
            var slug = ToSlug(name);
            if (string.IsNullOrEmpty(slug))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Name must contain letters or numbers.",
                    "name");
            }

            if (await _categories.ExistsByNameAsync(name, cancellationToken)
                || await _categories.ExistsBySlugAsync(slug, cancellationToken))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "A category with this name already exists.",
                    "name");
            }

            var category = new Category(name, slug);
            await _categories.AddAsync(category, cancellationToken);
            await _categories.SaveChangesAsync(cancellationToken);

            return new CategoryResponse
            {
                Id = category.Id,
                Name = category.Name,
                Slug = category.Slug,
                IsActive = category.IsActive,
                CreatedAt = category.CreatedAt
            };
        }

        public async Task<IReadOnlyList<CategoryResponse>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            var categories = await _categories.ListAllAsync(cancellationToken);
            return categories
                .Select(category => new CategoryResponse
                {
                    Id = category.Id,
                    Name = category.Name,
                    Slug = category.Slug,
                    IsActive = category.IsActive,
                    CreatedAt = category.CreatedAt
                })
                .ToList();
        }

        private static string ToSlug(string name)
        {
            var builder = new StringBuilder(name.Length);
            var previousWasHyphen = false;

            foreach (var ch in name)
            {
                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(char.ToUpperInvariant(ch));
                    previousWasHyphen = false;
                    continue;
                }

                if (!previousWasHyphen && builder.Length > 0)
                {
                    builder.Append('-');
                    previousWasHyphen = true;
                }
            }

            while (builder.Length > 0 && builder[^1] == '-')
            {
                builder.Length--;
            }

            if (builder.Length > MaxSlugLength)
            {
                builder.Length = MaxSlugLength;
                while (builder.Length > 0 && builder[^1] == '-')
                {
                    builder.Length--;
                }
            }

            return builder.ToString();
        }
    }
}
