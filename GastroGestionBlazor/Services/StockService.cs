using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Enums;
using GastroGestionBlazor.Contracts.Stock;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Reads stock balances and registers manual stock movements (Compra / Merma / Ajuste) against the
/// API. Reservation/consumption movements are system-driven by the OT lifecycle, not this service.
/// </summary>
public sealed class StockService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public StockService(HttpClient httpClient) => _httpClient = httpClient;

    /// <summary>GET /stock/balances — current balance for every ingredient.</summary>
    public async Task<List<IngredienteBalanceResponse>> GetBalancesAsync(CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync("stock/balances", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudieron cargar los balances de stock.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<IngredienteBalanceResponse>>(JsonOptions, ct);
        return result ?? new List<IngredienteBalanceResponse>();
    }

    /// <summary>GET /stock/movimientos/{ingredienteId} — the ingredient's ledger, newest first.</summary>
    public async Task<List<MovimientoStockResponse>> GetMovimientosAsync(Guid ingredienteId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"stock/movimientos/{ingredienteId}", ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudieron cargar los movimientos de stock.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<MovimientoStockResponse>>(JsonOptions, ct);
        return result ?? new List<MovimientoStockResponse>();
    }

    /// <summary>PUT /ingredientes/{id}/stock-minimo — set the reorder (low-stock alert) threshold.</summary>
    public async Task SetStockMinimoAsync(Guid ingredienteId, decimal stockMinimo, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync(
            $"ingredientes/{ingredienteId}/stock-minimo",
            new ActualizarStockMinimoRequest(stockMinimo), JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo actualizar el umbral de stock.", ct);
    }

    /// <summary>POST /stock/movimientos — register a manual movement (Compra / Merma / Ajuste).</summary>
    public async Task RegistrarMovimientoAsync(
        Guid ingredienteId, TipoMovimientoStock tipo, decimal cantidad,
        Guid? proveedorId = null, CancellationToken ct = default)
    {
        var request = new RegistrarMovimientoStockRequest(ingredienteId, tipo, cantidad, null, null, proveedorId);
        var response = await _httpClient.PostAsJsonAsync("stock/movimientos", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo registrar el movimiento de stock.", ct);
    }

    private static async Task ThrowApiExceptionAsync(
        HttpResponseMessage response, string fallback, CancellationToken ct)
    {
        ProblemDetailsResponse? problem = null;
        try
        {
            problem = await response.Content.ReadFromJsonAsync<ProblemDetailsResponse>(JsonOptions, ct);
        }
        catch { /* ignore deserialization errors */ }

        throw new ApiException(problem?.Detail ?? problem?.Title ?? fallback);
    }
}
