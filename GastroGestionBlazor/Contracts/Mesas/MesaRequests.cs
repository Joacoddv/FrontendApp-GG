namespace GastroGestionBlazor.Contracts.Mesas;

/// <summary>POST /mesas — create a new table.</summary>
public sealed record CrearMesaRequest(int Numero, int Capacidad);

/// <summary>PUT /mesas/{id} — edit a table's number and capacity.</summary>
public sealed record EditarMesaRequest(int Numero, int Capacidad);

/// <summary>PUT /mesas/{id}/posicion — persist a table's floor position.</summary>
public sealed record UbicarMesaRequest(int X, int Y);
