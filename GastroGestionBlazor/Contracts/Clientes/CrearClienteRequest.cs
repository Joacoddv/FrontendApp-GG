using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Clientes;

public sealed record CrearClienteRequest(
    string Nombre,
    CondicionIVA CondicionIVA,
    string? Cuit,
    string? Email,
    DateOnly? FechaNacimiento = null);
