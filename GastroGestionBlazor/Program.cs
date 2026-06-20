using Blazored.LocalStorage;
using GastroGestionBlazor;
using GastroGestionBlazor.Options;
using GastroGestionBlazor.Services.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// 1. Typed config — fails at startup if ApiBaseUrl is missing (no silent fallback). BAF-01.
var apiOptions = builder.Configuration.Get<ApiOptions>()
    ?? throw new InvalidOperationException("ApiOptions section is missing from appsettings.json. Ensure ApiBaseUrl is set.");
builder.Services.AddSingleton(apiOptions);

// 2. Local storage + auth core services.
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
    sp.GetRequiredService<CustomAuthenticationStateProvider>());
builder.Services.AddScoped<IAuthService, AuthService>();

// 3. BearerTokenHandler must be registered before the named client that uses it. ADR-4.
builder.Services.AddTransient<BearerTokenHandler>();

// 4a. Unauthenticated client — login calls only. No BearerTokenHandler attached.
builder.Services.AddHttpClient("AuthApi", client =>
{
    client.BaseAddress = new Uri(apiOptions.ApiBaseUrl);
});

// 4b. Authenticated client — all other API calls. Bearer injected by handler.
builder.Services.AddHttpClient("AuthorizedApi", client =>
{
    client.BaseAddress = new Uri(apiOptions.ApiBaseUrl);
})
.AddHttpMessageHandler<BearerTokenHandler>();

// 5. Inject the authenticated client as the default HttpClient for the data services
//    (ClienteService, IngredienteService). They use relative paths against ApiBaseUrl
//    with the Bearer token attached by BearerTokenHandler.
builder.Services.AddScoped(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    return factory.CreateClient("AuthorizedApi");
});

builder.Services.AddScoped<ClienteService>();
builder.Services.AddScoped<IngredienteService>();
builder.Services.AddScoped<KitchenBoardService>();
builder.Services.AddScoped<PlatoService>();
builder.Services.AddScoped<MenuService>();
builder.Services.AddScoped<PedidoService>();

await builder.Build().RunAsync();
