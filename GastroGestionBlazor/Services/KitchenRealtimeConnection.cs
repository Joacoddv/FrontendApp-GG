using GastroGestionBlazor.Contracts.OrdenesTrabajo;
using GastroGestionBlazor.Options;
using GastroGestionBlazor.Services.Auth;
using Microsoft.AspNetCore.SignalR.Client;
using System.Text.Json.Serialization;

public sealed class KitchenRealtimeConnection : IAsyncDisposable
{
    private HubConnection? _connection;

    public HubConnectionState State =>
        _connection?.State ?? HubConnectionState.Disconnected;

    private readonly ApiOptions _apiOptions;
    private readonly IAuthService _authService;

    public KitchenRealtimeConnection(ApiOptions apiOptions, IAuthService authService)
    {
        _apiOptions = apiOptions;
        _authService = authService;
    }

    public async Task StartAsync(
        Action<OrdenTrabajoBoardItem> onOtChanged,
        Func<Task> onReconnected,
        Action onStateChanged,
        CancellationToken ct = default)
    {
        var hubUrl = $"{_apiOptions.ApiBaseUrl.TrimEnd('/')}/hubs/kitchen";

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl, options =>
            {
                options.AccessTokenProvider = async () => await _authService.GetTokenAsync();
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(options =>
            {
                options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            })
            .Build();

        _connection.On<OrdenTrabajoBoardItem>("OtChanged", onOtChanged);

        _connection.Reconnected += async _ =>
        {
            onStateChanged();
            await onReconnected();
        };

        _connection.Reconnecting += _ =>
        {
            onStateChanged();
            return Task.CompletedTask;
        };

        _connection.Closed += _ =>
        {
            onStateChanged();
            return Task.CompletedTask;
        };

        await _connection.StartAsync(ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
