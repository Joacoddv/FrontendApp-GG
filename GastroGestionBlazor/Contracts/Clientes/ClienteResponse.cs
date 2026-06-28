using GastroGestionBlazor.Contracts.Enums;

namespace GastroGestionBlazor.Contracts.Clientes;

public sealed record ClienteResponse(
    Guid Id,
    string Nombre,
    CondicionIVA CondicionIVA,
    string? Cuit,
    string? Email,
    bool Activo,
    DateOnly? FechaNacimiento = null,
    IReadOnlyList<DireccionResponse>? Direcciones = null,
    string? Apellido = null,
    string? Telefono = null,
    string? Dni = null);

public sealed record DireccionResponse(
    Guid Id,
    string Calle,
    string Numero,
    string Ciudad,
    string Provincia,
    string CodigoPostal,
    string? Piso,
    string? Departamento,
    string? Zona = null);

/// <summary>Body for POST /clientes/{id}/direcciones.</summary>
public sealed record AgregarDireccionRequest(
    string Calle,
    string Numero,
    string Ciudad,
    string Provincia,
    string CodigoPostal,
    string? Piso,
    string? Departamento,
    string? Zona = null);

/// <summary>Row of GET /clientes/cumpleaneros.</summary>
public sealed record CumpleaneroResponse(Guid Id, string Nombre, string? Email, DateOnly FechaNacimiento);

/// <summary>Result of POST /clientes/cumpleaneros/enviar-promo.</summary>
public sealed record EnviarPromoResponse(int Enviados, int SinEmail);
