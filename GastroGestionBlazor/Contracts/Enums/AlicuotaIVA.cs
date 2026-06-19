namespace GastroGestionBlazor.Contracts.Enums;

/// <summary>
/// VAT rate bracket for a dish. Mirrors the backend Domain enum.
/// </summary>
public enum AlicuotaIVA
{
    Exento = 0,       // 0%
    ReducidoA = 1,    // 10.5%
    General = 2,      // 21%
    Diferencial = 3   // 27%
}
