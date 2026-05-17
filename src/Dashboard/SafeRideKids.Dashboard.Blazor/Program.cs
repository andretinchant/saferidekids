using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using MudBlazor;
using MudBlazor.Services;
using Refit;
using SafeRideKids.Dashboard.Blazor.Services;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------------------------------------
// Blazor Server + MudBlazor
// ----------------------------------------------------------------------------------
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddHttpContextAccessor();

builder.Services.AddMudServices(config =>
{
    config.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.BottomRight;
    config.SnackbarConfiguration.VisibleStateDuration = 4000;
    config.SnackbarConfiguration.ShowCloseIcon = true;
});

// ----------------------------------------------------------------------------------
// Autenticação Cognito (placeholder — middleware Cookie + OIDC).
// Em desenvolvimento, pode-se usar um stub via "Cognito:Authority=local-dev".
// O HttpContext expõe access_token (SaveTokens=true) que o BearerTokenHandler lê.
// ----------------------------------------------------------------------------------
var cognitoAuthority = builder.Configuration["Cognito:Authority"];
var cognitoClientId = builder.Configuration["Cognito:ClientId"];
var cognitoAudience = builder.Configuration["Cognito:Audience"];

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.Cookie.Name = "srk.dashboard.session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.LoginPath = "/login";
        options.AccessDeniedPath = "/login";
    })
    .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
    {
        options.Authority = cognitoAuthority;
        options.ClientId = cognitoClientId;
        options.ResponseType = "code";
        options.UsePkce = true;
        options.SaveTokens = true;                  // crítico: persiste access_token no cookie de auth
        options.GetClaimsFromUserInfoEndpoint = true;
        options.Scope.Add("openid");
        options.Scope.Add("email");
        options.Scope.Add("profile");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = !string.IsNullOrEmpty(cognitoAudience),
            ValidAudience = cognitoAudience,
            ValidateIssuer = true,
            NameClaimType = "email"
        };
        // Em desenvolvimento (authority local-dev sem metadata), desativa
        // exigências de descoberta — o operador faz login mockado.
        if (!string.IsNullOrEmpty(builder.Configuration["Cognito:MetadataAddress"]))
        {
            options.MetadataAddress = builder.Configuration["Cognito:MetadataAddress"];
        }
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    });

builder.Services.AddAuthorization();

// ----------------------------------------------------------------------------------
// Serviços de aplicação
// ----------------------------------------------------------------------------------
builder.Services.AddSingleton<ICsvExportService, CsvExportService>();
builder.Services.AddScoped<IAuthState, AuthState>();
builder.Services.AddTransient<BearerTokenHandler>();

// ----------------------------------------------------------------------------------
// HTTP / Refit — cliente tipado para o backend
// ----------------------------------------------------------------------------------
var refitSettings = new RefitSettings
{
    ContentSerializer = new SystemTextJsonContentSerializer(new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    })
};

var backendBaseUrl = builder.Configuration["Backend:BaseUrl"]
    ?? throw new InvalidOperationException("Backend:BaseUrl não configurado em appsettings.");
var backendTimeoutSeconds = builder.Configuration.GetValue<int?>("Backend:TimeoutSeconds") ?? 60;

builder.Services
    .AddRefitClient<IBackendApi>(refitSettings)
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri(backendBaseUrl);
        c.Timeout = TimeSpan.FromSeconds(backendTimeoutSeconds);
    })
    .AddHttpMessageHandler<BearerTokenHandler>();

// ----------------------------------------------------------------------------------
// Localização: pt-BR como padrão para a UI
// ----------------------------------------------------------------------------------
builder.Services.Configure<Microsoft.AspNetCore.Builder.RequestLocalizationOptions>(options =>
{
    var ptBr = new System.Globalization.CultureInfo("pt-BR");
    options.DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(ptBr);
    options.SupportedCultures = new[] { ptBr };
    options.SupportedUICultures = new[] { ptBr };
});

var app = builder.Build();

// ----------------------------------------------------------------------------------
// Pipeline HTTP
// ----------------------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRequestLocalization();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// ----------------------------------------------------------------------------------
// Endpoints minimal de auth — /signin dispara Challenge OIDC; /signout faz logout
// e limpa cookie. Para POC com Cognito real, configure Hosted UI.
// ----------------------------------------------------------------------------------
app.MapGet("/signin", (HttpContext ctx, string? returnUrl) =>
{
    var redirect = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
    return Results.Challenge(
        new Microsoft.AspNetCore.Authentication.AuthenticationProperties { RedirectUri = redirect },
        new[] { OpenIdConnectDefaults.AuthenticationScheme });
});

app.MapGet("/signout", async (HttpContext ctx) =>
{
    await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    await ctx.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme);
    return Results.Redirect("/login");
});

app.Run();
