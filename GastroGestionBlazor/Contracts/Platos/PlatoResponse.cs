namespace GastroGestionBlazor.Contracts.Platos;

/// <summary>
/// Read model for a dish, mirroring the backend GET /platos contract.
/// Enum fields (AlicuotaIVA, Unidad) are kept as strings — the API
/// serializes enums as strings, so no enum mirror is needed for display.
/// </summary>
public sealed record PlatoResponse(
    Guid Id,
    string Nombre,
    decimal PrecioBase,
    string Moneda,
    string AlicuotaIVA,
    bool Activo,
    RecetaLineaResponse[] Receta);

public sealed record RecetaLineaResponse(
    Guid Id,
    Guid IngredienteId,
    decimal Cantidad,
    string Unidad);
