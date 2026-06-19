using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Platos;

/// <summary>
/// Create payload for a dish, mirroring the backend POST /platos contract.
/// Currency is not sent — the domain defaults to ARS. Lineas may be empty.
/// </summary>
public sealed record CrearPlatoRequest(
    string Nombre,
    decimal PrecioBase,
    AlicuotaIVA AlicuotaIVA,
    RecetaLineaRequest[] Lineas);

public sealed record RecetaLineaRequest(
    Guid IngredienteId,
    decimal Cantidad,
    UnidadDeMedida Unidad);
