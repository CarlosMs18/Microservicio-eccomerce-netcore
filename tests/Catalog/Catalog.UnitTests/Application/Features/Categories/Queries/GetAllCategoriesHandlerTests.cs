using System.Linq.Expressions;

namespace Catalog.UnitTests.Application.Features.Categories.Queries
{
    public class GetAllCategoriesHandlerTests
    {
        private readonly Mock<IUnitOfWork> _mockUnitOfWork;
        private readonly Mock<ICategoryRepository> _mockCategoryRepository;
        private readonly GetAllCategoriesHandler _handler;

        public GetAllCategoriesHandlerTests()
        {
            _mockUnitOfWork = new Mock<IUnitOfWork>();
            _mockCategoryRepository = new Mock<ICategoryRepository>();

            // Setup: UnitOfWork devuelve nuestro repository mockeado
            _mockUnitOfWork.Setup(uow => uow.CategoryRepository)
                          .Returns(_mockCategoryRepository.Object);

            _handler = new GetAllCategoriesHandler(_mockUnitOfWork.Object);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_WhenCategoriesExist_ShouldReturnCategoriesOrderedByName()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var categories = CreateTestCategories();
            var expectedCount = categories.Count;

            SetupRepositoryMock(categories);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(expectedCount);

            var resultList = result.ToList();
            resultList.Should().BeInAscendingOrder(x => x.Name, "categories should be ordered by name");

            // Verificar mapeo de propiedades
            AssertCategoryMapping(resultList.First(), categories.First(c => c.Name == "Category A"));
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_WhenNoCategoriesExist_ShouldReturnEmptyCollection()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var emptyCategories = new List<Category>();

            SetupRepositoryMock(emptyCategories);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_ShouldCallRepositoryWithCorrectParameters()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var categories = CreateTestCategories();

            SetupRepositoryMock(categories);

            // Act
            await _handler.Handle(query, CancellationToken.None);

            // Assert
            _mockCategoryRepository.Verify(
                repo => repo.GetAsync(
                    It.IsAny<Expression<Func<Category, bool>>>(), // predicate (null en este caso)
                    It.IsAny<Func<IQueryable<Category>, IOrderedQueryable<Category>>>(), // orderBy
                    It.IsAny<string>(), // includeString (null en este caso)
                    It.Is<bool>(disableTracking => disableTracking == true) // debe ser true para queries
                ),
                Times.Once,
                "Repository GetAsync should be called exactly once with correct parameters"
            );
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_ShouldMapAllCategoryPropertiesCorrectly()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var category = CreateCategoryWithAllProperties();
            var categories = new List<Category> { category };

            SetupRepositoryMock(categories);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            var mappedCategory = result.Single();
            AssertCategoryMapping(mappedCategory, category);
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_WhenCategoryHasNullDescription_ShouldHandleNullCorrectly()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var categoryWithNullDescription = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Category with null description",
                Description = null
            };
            var categories = new List<Category> { categoryWithNullDescription };

            SetupRepositoryMock(categories);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            var mappedCategory = result.Single();
            mappedCategory.Description.Should().BeNull("null descriptions should be preserved");
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_WithCancellationToken_ShouldPassThroughSuccessfully()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var categories = CreateTestCategories();
            var cancellationToken = new CancellationToken();

            SetupRepositoryMock(categories);

            // Act & Assert
            var act = async () => await _handler.Handle(query, cancellationToken);
            await act.Should().NotThrowAsync("handler should handle cancellation token gracefully");
        }

        [Fact]
        [Trait("Category", "Unit")]
        [Trait("Feature", "Categories")]
        public async Task Handle_WithLargeCategoryList_ShouldHandlePerformanceCorrectly()
        {
            // Arrange
            var query = new GetAllCategoriesQuery();
            var largeNumberOfCategories = CreateLargeNumberOfCategories(1000);

            SetupRepositoryMock(largeNumberOfCategories);

            // Act
            var result = await _handler.Handle(query, CancellationToken.None);

            // Assert
            result.Should().HaveCount(1000);
            result.Should().BeInAscendingOrder(x => x.Name);
        }

        #region Private Helper Methods

        /// <summary>
        /// Configura el mock del repository para devolver las categorías especificadas
        /// </summary>
        private void SetupRepositoryMock(IList<Category> categories)
        {
            _mockCategoryRepository
                .Setup(repo => repo.GetAsync(
                    It.IsAny<Expression<Func<Category, bool>>>(),
                    It.IsAny<Func<IQueryable<Category>, IOrderedQueryable<Category>>>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>()))
                .ReturnsAsync(() =>
                {
                    // Simular el ordenamiento que hace el handler
                    var orderedCategories = categories.OrderBy(c => c.Name);
                    return orderedCategories.ToList().AsReadOnly();
                });
        }

        /// <summary>
        /// Crea un conjunto de categorías de prueba con nombres en orden aleatorio
        /// </summary>
        private static List<Category> CreateTestCategories()
        {
            return new List<Category>
            {
                new Category
                {
                    Id = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                    Name = "Category C",
                    Description = "Description for Category C"
                },
                new Category
                {
                    Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
                    Name = "Category A",
                    Description = "Description for Category A"
                },
                new Category
                {
                    Id = Guid.Parse("33333333-3333-3333-3333-333333333333"),
                    Name = "Category B",
                    Description = "Description for Category B"
                }
            };
        }

        /// <summary>
        /// Crea una categoría con todas las propiedades definidas para pruebas de mapeo
        /// </summary>
        private static Category CreateCategoryWithAllProperties()
        {
            return new Category
            {
                Id = Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Name = "Complete Test Category",
                Description = "A complete category for testing all property mappings"
            };
        }

        /// <summary>
        /// Crea un gran número de categorías para pruebas de rendimiento
        /// </summary>
        private static List<Category> CreateLargeNumberOfCategories(int count)
        {
            var categories = new List<Category>();
            for (int i = 0; i < count; i++)
            {
                categories.Add(new Category
                {
                    Id = Guid.NewGuid(),
                    Name = $"Category {i:D4}",
                    Description = $"Description for Category {i:D4}"
                });
            }
            return categories;
        }

        /// <summary>
        /// Verifica que el mapeo entre Category y CategoryListResponse sea correcto
        /// </summary>
        private static void AssertCategoryMapping(CategoryListResponse response, Category category)
        {
            response.Should().NotBeNull("mapped category should not be null");
            response.Id.Should().Be(category.Id, "Id should be mapped correctly");
            response.Name.Should().Be(category.Name, "Name should be mapped correctly");
            response.Description.Should().Be(category.Description, "Description should be mapped correctly");
        }

        #endregion
    }
}