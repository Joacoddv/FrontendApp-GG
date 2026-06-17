using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Ingredientes;

public sealed record CrearIngredienteRequest(
    string Nombre,
    UnidadDeMedida UnidadBase);
