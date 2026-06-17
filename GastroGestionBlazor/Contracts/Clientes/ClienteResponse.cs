using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Clientes;

public sealed record ClienteResponse(
    Guid Id,
    string Nombre,
    CondicionIVA CondicionIVA,
    string? Cuit,
    string? Email,
    bool Activo);
