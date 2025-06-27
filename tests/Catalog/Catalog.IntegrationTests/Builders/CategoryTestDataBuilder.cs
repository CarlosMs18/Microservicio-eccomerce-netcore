using Catalog.Domain;

using Catalog.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;

namespace Catalog.IntegrationTests.Builders;

/// <summary>
/// 🏗️ Builder para crear datos de prueba de categorías
/// </summary>
public class CategoryTestDataBuilder
{
    private readonly List<Category> _categories = new();

    public CategoryTestDataBuilder WithCategory(string name, string description = "Test description", string createdBy = "test-user")
    {
        _categories.Add(new Category
        {
            Name = name,
            Description = description,
            CreatedBy = createdBy
        });
        return this;
    }

    public CategoryTestDataBuilder WithOrderedCategories()
    {
        return WithCategory("Alpha Category", "First category")
               .WithCategory("Beta Category", "Second category")
               .WithCategory("Gamma Category", "Third category")
               .WithCategory("Zebra Category", "Last category");
    }

    public CategoryTestDataBuilder WithRandomCategories(int count = 5)
    {
        var random = new Random();
        var prefixes = new[] { "Tech", "Home", "Sports", "Fashion", "Books", "Music", "Food", "Travel" };

        for (int i = 0; i < count; i++)
        {
            var prefix = prefixes[random.Next(prefixes.Length)];
            WithCategory($"{prefix} Category {i + 1}", $"Description for {prefix} {i + 1}");
        }
        return this;
    }

    /// <summary>
    /// 🌱 Crea las categorías en la base de datos
    /// </summary>
    public async Task<List<Category>> SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

        context.Categories.AddRange(_categories);
        await context.SaveChangesAsync();

        return _categories.ToList();
    }

    /// <summary>
    /// 📦 Obtiene las categorías sin guardarlas en BD
    /// </summary>
    public List<Category> Build() => _categories.ToList();
}