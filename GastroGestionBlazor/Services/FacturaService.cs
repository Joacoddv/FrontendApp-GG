using GastroGestionBlazor.Contracts.Common;
using GastroGestionBlazor.Contracts.Enums;
using GastroGestionBlazor.Contracts.Facturacion;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Drives invoice management against the API: list, detail, create, record payment,
/// and cancel invoices.
/// </summary>
public sealed class FacturaService
{
    private readonly HttpClient _httpClient;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public FacturaService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    /// <summary>GET /facturas — lists invoices, optionally filtered by estado and/or clienteId.</summary>
    public async Task<List<FacturaResumenResponse>> GetFacturasAsync(
        EstadoFactura? estado = null, Guid? clienteId = null, CancellationToken ct = default)
    {
        var url = "facturas";
        var qs = new List<string>();
        if (estado.HasValue)
            qs.Add($"estado={Uri.EscapeDataString(estado.Value.ToString())}");
        if (clienteId.HasValue)
            qs.Add($"clienteId={clienteId.Value}");
        if (qs.Count > 0)
            url += "?" + string.Join("&", qs);

        var response = await _httpClient.GetAsync(url, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la lista de facturas.", ct);

        var result = await response.Content.ReadFromJsonAsync<List<FacturaResumenResponse>>(JsonOptions, ct);
        return result ?? new List<FacturaResumenResponse>();
    }

    /// <summary>GET /facturas/{id} — full invoice detail, or null if not found.</summary>
    public async Task<FacturaResponse?> GetFacturaByIdAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"facturas/{id}", ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cargar la factura.", ct);

        return await response.Content.ReadFromJsonAsync<FacturaResponse>(JsonOptions, ct);
    }

    /// <summary>POST /facturas — creates an invoice and returns its id.</summary>
    public async Task<Guid> CrearFacturaAsync(CrearFacturaRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("facturas", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo crear la factura.", ct);

        return await ReadIdAsync(response, ct);
    }

    /// <summary>POST /facturas/{id}/pagos — records a payment on an invoice.</summary>
    public async Task RegistrarPagoAsync(Guid id, RegistrarPagoRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"facturas/{id}/pagos", request, JsonOptions, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo registrar el pago.", ct);
    }

    /// <summary>POST /facturas/{id}/cancelar — cancels an invoice.</summary>
    public async Task CancelarFacturaAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"facturas/{id}/cancelar", content: null, ct);
        if (!response.IsSuccessStatusCode)
            await ThrowApiExceptionAsync(response, "No se pudo cancelar la factura.", ct);
    }

    // POST returns the new Guid in the body; fall back to the Location header.
    private static async Task<Guid> ReadIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var id = await response.Content.ReadFromJsonAsync<Guid>(JsonOptions, ct);
            if (id != Guid.Empty)
                return id;
        }
        catch { /* body may not be a bare Guid */ }

        if (response.Headers.Location is { } location)
        {
            var segments = location.AbsolutePath.Split('/');
            if (Guid.TryParse(segments[^1], out var locationId))
                return locationId;
        }

        return Guid.Empty;
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
