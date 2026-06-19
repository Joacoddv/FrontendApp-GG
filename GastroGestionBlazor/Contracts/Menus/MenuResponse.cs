namespace GastroGestionBlazor.Contracts.Menus;

/// <summary>
/// Read model for a menu, mirroring the backend GET /menus contract.
/// </summary>
public sealed record MenuResponse(
    Guid Id,
    string Nombre,
    DateOnly FechaVigencia,
    bool Activo,
    MenuItemResponse[] Items);

public sealed record MenuItemResponse(
    Guid Id,
    Guid PlatoId,
    decimal? PrecioOverride);
