using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Ingredientes;

public sealed record IngredienteResponse(
    Guid Id,
    string Nombre,
    UnidadDeMedida UnidadBase,
    bool Activo);
