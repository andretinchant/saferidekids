using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SafeRideKids.Biometria.Core.Multitenancy;

namespace SafeRideKids.Biometria.Api.Multitenancy;

/// <summary>
/// Middleware que extrai 'custom:tenantId' do JWT do Cognito e popula o TenantContext.
/// Para requests anônimos (health, etc.), popula com defaults.
/// </summary>
public sealed class TenantResolutionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantResolutionMiddleware> _logger;

    public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContextAbstract)
    {
        if (tenantContextAbstract is not TenantContext tenantContext)
        {
            // Defensivo: registro do DI deve ser TenantContext concreto. Se vier outro tipo, segue sem populating.
            await _next(context).ConfigureAwait(false);
            return;
        }

        var tenantId = context.User?.FindFirstValue("custom:tenantId") ?? string.Empty;
        var actorId = context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? context.User?.FindFirstValue("sub")
                      ?? string.Empty;
        var actorType = context.User?.FindFirstValue("custom:actorType") ?? "family";

        // Fallback para POC sem JWT real (DEV): aceita header X-Dev-Tenant.
        if (string.IsNullOrWhiteSpace(tenantId) && context.Request.Headers.TryGetValue("X-Dev-Tenant", out var devTenant))
        {
            tenantId = devTenant.ToString();
        }
        if (string.IsNullOrWhiteSpace(actorId) && context.Request.Headers.TryGetValue("X-Dev-Actor", out var devActor))
        {
            actorId = devActor.ToString();
        }

        tenantContext.Populate(tenantId, actorId, actorType);
        await _next(context).ConfigureAwait(false);
    }
}
