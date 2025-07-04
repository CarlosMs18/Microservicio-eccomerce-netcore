namespace Catalog.IntegrationTests.Common;

/// <summary>
/// Evento que representa el cambio de precio de un producto
/// Usado para deserializar mensajes de RabbitMQ en tests de integración
/// </summary>
public class ProductPriceChangedEvent
{
    /// <summary>
    /// ID único del producto
    /// </summary>
    public Guid ProductId { get; set; }

    /// <summary>
    /// Nombre del producto
    /// </summary>
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Precio anterior del producto
    /// </summary>
    public decimal OldPrice { get; set; }

    /// <summary>
    /// Nuevo precio del producto
    /// </summary>
    public decimal NewPrice { get; set; }

    /// <summary>
    /// Fecha y hora cuando se realizó el cambio
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Usuario que realizó el cambio
    /// </summary>
    public string ChangedBy { get; set; } = string.Empty;

    /// <summary>
    /// ID de la categoría del producto
    /// </summary>
    public Guid CategoryId { get; set; }

    /// <summary>
    /// ID único del evento
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Fecha y hora cuando ocurrió el evento
    /// </summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// Tipo del evento
    /// </summary>
    public string EventType { get; set; } = string.Empty;
}