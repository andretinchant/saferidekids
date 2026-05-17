using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SafeRideKids.Biometria.Api.Contracts;
using SafeRideKids.Biometria.Api.Providers;
using SafeRideKids.Biometria.Core.Audit;
using SafeRideKids.Biometria.Core.Domain;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Security;
using SafeRideKids.Biometria.Infrastructure.Persistence;

namespace SafeRideKids.Biometria.Api.Endpoints;

/// <summary>Endpoints do Dashboard administrativo. 6.3 da CONTRACTS.md.</summary>
public static class DashboardEndpoints
{
    public static IEndpointRouteBuilder MapDashboardEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/dashboard").WithTags("dashboard");

        grp.MapGet("/checkins", ListCheckInsAsync);
        grp.MapGet("/checkins/{id:guid}", GetCheckInDetailAsync);
        grp.MapGet("/metrics/summary", GetMetricsSummaryAsync);
        grp.MapGet("/families", ListFamiliesAsync);
        grp.MapPost("/dsr/{action}", HandleDsrAsync);

        return app;
    }

    // ---------------- 6.3.1 list checkins ---------------------------------

    private static async Task<IResult> ListCheckInsAsync(
        ITenantContext tenant,
        BiometriaDbContext db,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] string? providerId,
        [FromQuery] string? outcome,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.CheckIns.Where(c => c.TenantId == tenant.TenantId);

        if (from.HasValue) query = query.Where(c => c.StartedAt >= from.Value);
        if (to.HasValue) query = query.Where(c => c.StartedAt <= to.Value);
        if (!string.IsNullOrWhiteSpace(providerId)) query = query.Where(c => c.ProviderId == providerId);
        if (!string.IsNullOrWhiteSpace(outcome)) query = query.Where(c => c.Result == outcome);

        var total = await query.CountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(c => c.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new CheckInListItem(
                c.Id,
                c.ChildId,
                c.Result,
                c.ProviderId,
                c.Confidence.HasValue ? (double?)(double)c.Confidence.Value : null,
                c.UsedFallback,
                c.LatencyMs,
                c.StartedAt,
                c.FinishedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return Results.Ok(new CheckInPagedResponse(items, page, pageSize, total));
    }

    // ---------------- 6.3.2 detail ----------------------------------------

    private static async Task<IResult> GetCheckInDetailAsync(
        Guid id,
        ITenantContext tenant,
        BiometriaDbContext db,
        CancellationToken ct)
    {
        var checkIn = await db.CheckIns
            .Where(c => c.Id == id && c.TenantId == tenant.TenantId)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (checkIn is null) return Results.NotFound();

        var events = await db.CheckInEvents
            .Where(e => e.CheckInId == id)
            .OrderBy(e => e.OccurredAt)
            .Select(e => new CheckInEventDto(e.EventType, e.PayloadJson, e.OccurredAt))
            .ToListAsync(ct).ConfigureAwait(false);

        var dto = new CheckInListItem(
            checkIn.Id, checkIn.ChildId, checkIn.Result, checkIn.ProviderId,
            checkIn.Confidence.HasValue ? (double?)(double)checkIn.Confidence.Value : null,
            checkIn.UsedFallback, checkIn.LatencyMs, checkIn.StartedAt, checkIn.FinishedAt);

        return Results.Ok(new CheckInDetailResponse(dto, events));
    }

    // ---------------- 6.3.3 metrics summary -------------------------------

    private static async Task<IResult> GetMetricsSummaryAsync(
        ITenantContext tenant,
        BiometriaDbContext db,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var query = db.CheckIns.Where(c => c.TenantId == tenant.TenantId);
        if (from.HasValue) query = query.Where(c => c.StartedAt >= from.Value);
        if (to.HasValue) query = query.Where(c => c.StartedAt <= to.Value);

        var rows = await query
            .Select(c => new { c.Result, c.ProviderId, c.UsedFallback, c.LatencyMs, c.ProviderCostMicroCents })
            .ToListAsync(ct).ConfigureAwait(false);

        var total = rows.Count;
        var byOutcome = rows
            .GroupBy(r => r.Result ?? "unknown")
            .Select(g => new OutcomeCount(g.Key, g.Count()))
            .ToList();

        var byProvider = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ProviderId))
            .GroupBy(r => r.ProviderId!)
            .Select(g => new ProviderCount(g.Key, g.Count(),
                g.Sum(x => x.ProviderCostMicroCents ?? 0L)))
            .ToList();

        var latencies = rows.Where(r => r.LatencyMs.HasValue).Select(r => (long)r.LatencyMs!.Value).OrderBy(v => v).ToList();
        long? p50 = latencies.Count > 0 ? latencies[latencies.Count / 2] : null;
        long? p95 = latencies.Count > 0 ? latencies[Math.Min(latencies.Count - 1, (int)Math.Ceiling(latencies.Count * 0.95) - 1)] : null;

        var fallbackRate = total == 0 ? 0d : rows.Count(r => r.UsedFallback) / (double)total;

        var totalCostMicroCents = rows.Sum(r => r.ProviderCostMicroCents ?? 0L);
        // microcent = 1e-8 USD → USD = microcents * 1e-8.
        var totalCostUsd = totalCostMicroCents == 0L ? 0m : Math.Round((decimal)totalCostMicroCents / 100_000_000m, 4);

        return Results.Ok(new MetricsSummaryResponse(
            TotalCheckIns: total,
            ByOutcome: byOutcome,
            ByProvider: byProvider,
            P50LatencyMs: p50,
            P95LatencyMs: p95,
            FallbackRate: fallbackRate,
            EstimatedCostUsd: totalCostUsd));
    }

    // ---------------- 6.3.4 list families ---------------------------------

    private static async Task<IResult> ListFamiliesAsync(
        ITenantContext tenant,
        BiometriaDbContext db,
        CancellationToken ct)
    {
        var items = await db.Families
            .Where(f => f.TenantId == tenant.TenantId)
            .OrderByDescending(f => f.CreatedAt)
            .Take(500)
            .Select(f => new FamilySummary(f.Id, f.TenantId, f.PreferredProviderId, f.CreatedAt))
            .ToListAsync(ct).ConfigureAwait(false);

        return Results.Ok(items);
    }

    // ---------------- 6.3.5 DSR -------------------------------------------

    private static async Task<IResult> HandleDsrAsync(
        string action,
        [FromBody] DsrRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        IFaceVerificationProviderResolver providers,
        IAuditLogger audit,
        IHmacHasher hasher,
        CancellationToken ct)
    {
        action = (action ?? string.Empty).ToLowerInvariant();
        if (action is not ("access" or "delete" or "portability"))
            return Results.BadRequest(new { error = "action deve ser 'access', 'delete' ou 'portability'" });

        if (!string.Equals(body.TargetType, "child", StringComparison.OrdinalIgnoreCase) || !Guid.TryParse(body.TargetId, out var childGuid))
            return Results.BadRequest(new { error = "Apenas targetType='child' suportado nesta POC" });

        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == childGuid && c.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (child is null) return Results.NotFound();

        string outcome;
        string? details = null;

        switch (action)
        {
            case "access":
                {
                    var enrolls = await db.Enrollments.Where(e => e.ChildId == childGuid).ToListAsync(ct).ConfigureAwait(false);
                    details = System.Text.Json.JsonSerializer.Serialize(enrolls.Select(e => new
                    {
                        e.ProviderId,
                        e.Status,
                        e.EnrolledAt,
                        e.ExpiresAt
                    }));
                    outcome = "ok";
                    break;
                }
            case "delete":
                {
                    var activeEnrolls = await db.Enrollments
                        .Where(e => e.ChildId == childGuid && e.Status == EnrollmentStatus.Active)
                        .ToListAsync(ct).ConfigureAwait(false);

                    foreach (var e in activeEnrolls)
                    {
                        try
                        {
                            await providers.Resolve(e.ProviderId).DeleteEnrollmentAsync(e.ProviderReferenceId, ct).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            // Logado no provider; seguimos para que outros provedores também sejam apagados.
                            details = $"Aviso: falha em provider {e.ProviderId}: {ex.GetType().Name}";
                        }
                        e.Status = EnrollmentStatus.Deleted;
                        e.DeletedAt = DateTimeOffset.UtcNow;
                    }

                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    outcome = "deleted";
                    break;
                }
            case "portability":
                {
                    var enrolls = await db.Enrollments.Where(e => e.ChildId == childGuid).ToListAsync(ct).ConfigureAwait(false);
                    var checkins = await db.CheckIns.Where(c => c.ChildId == childGuid).Take(500).ToListAsync(ct).ConfigureAwait(false);
                    details = System.Text.Json.JsonSerializer.Serialize(new { enrolls, checkins });
                    outcome = "ok";
                    break;
                }
            default:
                outcome = "unsupported";
                break;
        }

        await audit.LogAsync(tenant.TenantId,
            await hasher.HashAsync(tenant.TenantId, tenant.ActorId, ct),
            ActorType.Admin, $"Dsr{char.ToUpper(action[0])}{action[1..]}Executed",
            body.TargetType, body.TargetId, "{}", ct).ConfigureAwait(false);

        return Results.Ok(new DsrResponse(action, outcome, details));
    }
}
