using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Clientes;

public sealed record EditarClienteRequest(
    string Nombre,
    CondicionIVA CondicionIVA,
    string? Cuit,
    string? Email,
    DateOnly? FechaNacimiento = null,
    string? Apellido = null,
    string? Telefono = null,
    string? Dni = null);
