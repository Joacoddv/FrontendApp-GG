namespace GastroGestionBlazor.Contracts.Mesas;

/// <summary>
/// Read model for a table, mirroring the backend GET /mesas contract.
/// Estado is kept as a string — the API serializes enums as strings.
/// </summary>
public sealed record MesaResponse(
    Guid Id,
    int Numero,
    int Capacidad,
    string Estado,
    bool Activa,
    Guid? PedidoActivoId,
    int? PosicionX,
    int? PosicionY);
