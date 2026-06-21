namespace GastroGestionBlazor.Contracts.Proveedores;

public sealed record ProveedorResponse(
    Guid Id,
    string Nombre,
    string? Cuit,
    string? Email,
    string? Telefono,
    bool Activo);

public sealed record CrearProveedorRequest(string Nombre, string? Cuit, string? Email, string? Telefono);

public sealed record EditarProveedorRequest(string Nombre, string? Cuit, string? Email, string? Telefono);
