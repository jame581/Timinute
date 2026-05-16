using Blazored.SessionStorage;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Radzen;
using Timinute.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddHttpClient("Timinute.ServerAPI", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<BaseAddressAuthorizationMessageHandler>();

builder.Services.AddBlazoredSessionStorage();
builder.Services.AddRadzenComponents();

// Supply HttpClient instances that include access tokens when making requests to the server project
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("Timinute.ServerAPI"));

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<Timinute.Client.Services.UndoNotificationService>();
builder.Services.AddScoped<Timinute.Client.Services.ProjectColorService>();
builder.Services.AddScoped<Timinute.Client.Services.ViewportService>();
builder.Services.AddScoped<Timinute.Client.Services.MobileSheetService>();
builder.Services.AddScoped<Timinute.Client.Services.UserProfileService>();
builder.Services.AddScoped<Timinute.Client.Services.ThemeService>();

builder.Services.AddOidcAuthentication(options =>
{
    options.ProviderOptions.Authority = builder.HostEnvironment.BaseAddress.TrimEnd('/');
    options.ProviderOptions.ClientId = "Timinute.Client";
    options.ProviderOptions.ResponseType = "code";
    options.ProviderOptions.DefaultScopes.Add("Timinute.ServerAPI");
});

await builder.Build().RunAsync();
