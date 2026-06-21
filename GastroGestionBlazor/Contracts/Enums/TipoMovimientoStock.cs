namespace GastroGestionBlazor.Contracts.Enums;

/// <summary>Stock ledger movement type — mirrors the backend Domain.Enums.TipoMovimientoStock.</summary>
public enum TipoMovimientoStock
{
    Compra                = 0,
    Consumo               = 1,
    Ajuste                = 2,
    Reserva               = 3,
    LiberacionReserva     = 4,
    DevolucionCancelacion = 5,
    Merma                 = 6
}
