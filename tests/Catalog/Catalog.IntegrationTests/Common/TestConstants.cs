namespace Catalog.IntegrationTests.Common;

/// <summary>
/// Constantes utilizadas en los tests de integración
/// </summary>
public static class TestConstants
{
    /// <summary>
    /// Configuración de RabbitMQ para tests
    /// </summary>
    public static class RabbitMQ
    {
        /// <summary>
        /// Nombre del exchange principal para eventos del catálogo
        /// </summary>
        public const string EXCHANGE_NAME = "catalog.events";

        /// <summary>
        /// Routing keys para diferentes tipos de eventos
        /// </summary>
        public static class RoutingKeys
        {
            public const string PRODUCT_UPDATED = "catalog.product.updated";
            public const string PRODUCT_CREATED = "catalog.product.created";
            public const string PRODUCT_DELETED = "catalog.product.deleted";
            public const string CATEGORY_UPDATED = "catalog.category.updated";
            public const string CATEGORY_CREATED = "catalog.category.created";
            public const string CATEGORY_DELETED = "catalog.category.deleted";
        }

        /// <summary>
        /// Nombres de colas para tests específicos
        /// </summary>
        public static class Queues
        {
            public const string PRODUCT_PRICE_UPDATE_TEST = "catalog.product.updated.test";
            public const string PRODUCT_CREATE_TEST = "catalog.product.created.test";
            public const string PRODUCT_DELETE_TEST = "catalog.product.deleted.test";
        }
    }

    /// <summary>
    /// Usuarios de test predefinidos
    /// </summary>
    public static class TestUsers
    {
        public const string DEFAULT_USER_ID = "test-user-123";
        public const string DEFAULT_USER_EMAIL = "test@example.com";
        public const string ADMIN_USER_ID = "test-admin-456";
        public const string ADMIN_USER_EMAIL = "admin@example.com";
    }

    /// <summary>
    /// Valores de test para productos
    /// </summary>
    public static class TestProducts
    {
        public const string DEFAULT_NAME = "Test Product";
        public const string DEFAULT_DESCRIPTION = "Product for testing";
        public const decimal DEFAULT_PRICE = 99.99m;
        public const string DEFAULT_CREATOR = "test-system";
    }

    /// <summary>
    /// Valores de test para categorías
    /// </summary>
    public static class TestCategories
    {
        public const string DEFAULT_NAME = "Test Category";
        public const string DEFAULT_DESCRIPTION = "Category for testing";
        public const string DEFAULT_CREATOR = "test-system";
    }

    /// <summary>
    /// Timeouts y delays para tests
    /// </summary>
    public static class Timeouts
    {
        /// <summary>
        /// Tiempo de espera para procesamiento asíncrono de eventos
        /// </summary>
        public const int EVENT_PROCESSING_DELAY_MS = 3000;

        /// <summary>
        /// Timeout para conexiones de test
        /// </summary>
        public const int CONNECTION_TIMEOUT_SECONDS = 10;
    }
}