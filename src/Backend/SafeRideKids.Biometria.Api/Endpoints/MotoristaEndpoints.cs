using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SafeRideKids.Biometria.Api.Contracts;
using SafeRideKids.Biometria.Api.Providers;
using SafeRideKids.Biometria.Core.Audit;
using SafeRideKids.Biometria.Core.Domain;
using SafeRideKids.Biometria.Core.Fallback;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Notifications;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Core.Routing;
using SafeRideKids.Biometria.Core.Security;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Api.Endpoints;

/// <summary>Endpoints do Motorista. 6.2 da CONTRACTS.md.</summary>
public static class MotoristaEndpoints
{
    public static IEndpointRouteBuilder MapMotoristaEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/motorista").WithTags("motorista");

        grp.MapGet("/route/today", GetTodayRouteAsync);
        grp.MapPost("/checkin/start", StartCheckInAsync);
        grp.MapPost("/checkin/verify", VerifyCheckInAsync);
        grp.MapPost("/checkin/fallback/pin", FallbackPinAsync);
        grp.MapPost("/checkin/fallback/manual", FallbackManualAsync);
        grp.MapPost("/checkin/confirm", ConfirmCheckInAsync);

        return app;
    }

    // ---------------- 6.2.1 today route -----------------------------------

    private static async Task<IResult> GetTodayRouteAsync(
        ITenantContext tenant,
        BiometriaDbContext db,
        CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var route = await db.Routes
            .Where(r => r.TenantId == tenant.TenantId
                        && r.MotoristaId == tenant.ActorId
                        && r.ScheduledDate == today)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);

        if (route is null) return Results.NotFound(new { error = "Sem rota agendada para hoje." });

        var stops = await (from rs in db.RouteStops
                           join c in db.Children on rs.ChildId equals c.Id
                           where rs.RouteId == route.Id
                           orderby rs.StopOrder
                           select new { rs, c }).ToListAsync(ct).ConfigureAwait(false);

        var dtos = stops.Select(s => new MotoristaStop(
            StopId: s.rs.Id,
            ChildId: s.c.Id,
            DisplayName: DecryptDisplayName(s.c.DisplayNameEncrypted, tenant.TenantId),
            ExpectedPickupTime: s.rs.ExpectedPickupTime.ToString("HH:mm"),
            AddressLabel: s.rs.AddressLabel,
            BoardingMode: s.c.BoardingMode)).ToList();

        return Results.Ok(new MotoristaTodayRouteResponse(route.Id, today, dtos));
    }

    // ---------------- 6.2.2 start check-in --------------------------------

    private static async Task<IResult> StartCheckInAsync(
        [FromBody] CheckInStartRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        IProviderRouter router,
        IFaceVerificationProviderResolver providers,
        IAuditLogger audit,
        IHmacHasher hasher,
        CancellationToken ct)
    {
        var stop = await db.RouteStops.FirstOrDefaultAsync(s => s.Id == body.RouteStopId, ct).ConfigureAwait(false);
        if (stop is null) return Results.NotFound(new { error = "RouteStop não encontrada" });
        if (stop.ChildId != body.ChildId) return Results.BadRequest(new { error = "childId não corresponde ao stop" });

        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == body.ChildId && c.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (child is null) return Results.NotFound(new { error = "Child não encontrada para o tenant" });

        var providerId = router.ResolveProviderId(
            tenantId: tenant.TenantId,
            familyId: child.FamilyId.ToString(),
            childId: child.Id.ToString(),
            utcNow: DateTimeOffset.UtcNow);

        var provider = providers.Resolve(providerId);

        var enrollment = await db.Enrollments
            .Where(e => e.ChildId == child.Id && e.ProviderId == providerId && e.Status == EnrollmentStatus.Active)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (enrollment is null)
        {
            return Results.Conflict(new { error = $"Sem enrollment ativo para provider {providerId}; rodar enroll antes." });
        }

        var checkInId = Guid.NewGuid();
        var session = await provider.StartLivenessSessionAsync(
            new LivenessSessionRequest(checkInId.ToString(), child.Id.ToString(), tenant.TenantId), ct).ConfigureAwait(false);

        var checkIn = new CheckInEntity
        {
            Id = checkInId,
            TenantId = tenant.TenantId,
            RouteId = stop.RouteId,
            RouteStopId = stop.Id,
            ChildId = child.Id,
            MotoristaId = tenant.ActorId,
            StartedAt = DateTimeOffset.UtcNow,
            ProviderId = providerId,
            ProviderSessionId = session.SessionId
        };
        await db.CheckIns.AddAsync(checkIn, ct).ConfigureAwait(false);
        await db.CheckInEvents.AddAsync(new CheckInEventEntity
        {
            Id = Guid.NewGuid(),
            CheckInId = checkInId,
            EventType = "StartLiveness",
            PayloadJson = JsonSerializer.Serialize(new { providerId, session.SessionId, session.ExpiresAt }),
            OccurredAt = DateTimeOffset.UtcNow
        }, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Results.Ok(new CheckInStartResponse(
            CheckInId: checkInId,
            ProviderId: providerId,
            SessionInfo: new LivenessSessionDto(session.SessionId, session.SdkConfig, session.ExpiresAt)));
    }

    // ---------------- 6.2.3 verify ----------------------------------------

    private static async Task<IResult> VerifyCheckInAsync(
        [FromBody] CheckInVerifyRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        IFaceVerificationProviderResolver providers,
        INotificationOutboxRepository outbox,
        IAuditLogger audit,
        IHmacHasher hasher,
        CancellationToken ct)
    {
        var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.Id == body.CheckInId && c.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (checkIn is null) return Results.NotFound(new { error = "CheckIn não encontrado" });
        if (string.IsNullOrWhiteSpace(checkIn.ProviderId)) return Results.Conflict(new { error = "CheckIn sem provider — chamar start primeiro" });

        var enrollment = await db.Enrollments
            .Where(e => e.ChildId == checkIn.ChildId && e.ProviderId == checkIn.ProviderId && e.Status == EnrollmentStatus.Active)
            .FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (enrollment is null) return Results.Conflict(new { error = "Enrollment ativo não encontrado" });

        var provider = providers.Resolve(checkIn.ProviderId);
        var result = await provider.VerifyAsync(new VerificationRequest(
            SessionId: body.SessionId,
            ChildId: checkIn.ChildId.ToString(),
            TenantId: tenant.TenantId,
            ProviderReferenceId: enrollment.ProviderReferenceId), ct).ConfigureAwait(false);

        checkIn.Result = result.Outcome switch
        {
            VerificationOutcome.Approved => CheckInResult.Approved,
            VerificationOutcome.Inconclusive => CheckInResult.Inconclusive,
            _ => CheckInResult.Rejected
        };
        checkIn.Confidence = result.Confidence.HasValue ? (decimal?)Math.Round((decimal)result.Confidence.Value, 4) : null;
        checkIn.LivenessPassed = result.LivenessPassed;
        checkIn.LatencyMs = (int)Math.Min(int.MaxValue, result.LatencyMs);
        checkIn.ProviderCostMicroCents = result.ProviderCostMicroCents;

        await db.CheckInEvents.AddAsync(new CheckInEventEntity
        {
            Id = Guid.NewGuid(),
            CheckInId = checkIn.Id,
            EventType = result.Outcome == VerificationOutcome.Approved ? "VerifyOk" : "VerifyFailed",
            PayloadJson = JsonSerializer.Serialize(new
            {
                outcome = result.Outcome.ToString(),
                confidence = result.Confidence,
                livenessPassed = result.LivenessPassed,
                failureReason = result.FailureReason
            }),
            OccurredAt = DateTimeOffset.UtcNow
        }, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        // Notificação imediata ao responsável (outbox).
        var child = await db.Children.FirstAsync(c => c.Id == checkIn.ChildId, ct).ConfigureAwait(false);
        var template = result.Outcome switch
        {
            VerificationOutcome.Approved => NotificationTemplate.CheckInApproved,
            VerificationOutcome.Rejected => NotificationTemplate.CheckInRejectedAttention,
            _ => NotificationTemplate.CheckInFallbackUsed
        };
        await outbox.EnqueueAsync(new NotificationMessage(
            TenantId: tenant.TenantId,
            FamilyId: child.FamilyId,
            ChildId: child.Id,
            CheckInId: checkIn.Id,
            Template: template,
            PayloadJson: JsonSerializer.Serialize(new
            {
                outcome = result.Outcome.ToString(),
                confidence = result.Confidence,
                providerId = checkIn.ProviderId
            }),
            CreatedAt: DateTimeOffset.UtcNow), ct).ConfigureAwait(false);

        var suggestion = result.Outcome switch
        {
            VerificationOutcome.Approved => NextActionSuggestion.Confirm,
            VerificationOutcome.Inconclusive => NextActionSuggestion.FallbackPin,
            _ => NextActionSuggestion.FallbackManual
        };

        return Results.Ok(new CheckInVerifyResponse(result.Outcome.ToString(), result.Confidence, suggestion));
    }

    // ---------------- 6.2.4 fallback pin ----------------------------------

    private static async Task<IResult> FallbackPinAsync(
        [FromBody] FallbackPinRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        IFallbackPinService pinService,
        CancellationToken ct)
    {
        var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.Id == body.CheckInId && c.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (checkIn is null) return Results.NotFound(new { error = "CheckIn não encontrado" });

        var result = await pinService.ValidatePinAsync(tenant.TenantId, checkIn.ChildId, body.Pin, ct).ConfigureAwait(false);

        if (result.Outcome == PinValidationOutcome.Valid)
        {
            checkIn.UsedFallback = true;
            checkIn.FallbackType = FallbackType.Pin;
            checkIn.Result = CheckInResult.Fallback;
            await db.CheckInEvents.AddAsync(new CheckInEventEntity
            {
                Id = Guid.NewGuid(),
                CheckInId = checkIn.Id,
                EventType = "FallbackTriggered",
                PayloadJson = JsonSerializer.Serialize(new { type = FallbackType.Pin }),
                OccurredAt = DateTimeOffset.UtcNow
            }, ct).ConfigureAwait(false);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
        }

        return Results.Ok(new FallbackPinResponse(result.Outcome.ToString(), result.AttemptsRemaining));
    }

    // ---------------- 6.2.5 fallback manual -------------------------------

    private static async Task<IResult> FallbackManualAsync(
        [FromBody] FallbackManualRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        CancellationToken ct)
    {
        var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.Id == body.CheckInId && c.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (checkIn is null) return Results.NotFound(new { error = "CheckIn não encontrado" });

        if (string.IsNullOrWhiteSpace(body.Justification))
            return Results.BadRequest(new { error = "justification obrigatória para fallback manual" });

        checkIn.UsedFallback = true;
        checkIn.FallbackType = FallbackType.Manual;
        checkIn.Result = CheckInResult.Fallback;
        checkIn.Notes = body.Justification;

        await db.CheckInEvents.AddAsync(new CheckInEventEntity
        {
            Id = Guid.NewGuid(),
            CheckInId = checkIn.Id,
            EventType = "FallbackTriggered",
            PayloadJson = JsonSerializer.Serialize(new { type = FallbackType.Manual, hasPhoto = !string.IsNullOrWhiteSpace(body.PhotoOptionalBase64) }),
            OccurredAt = DateTimeOffset.UtcNow
        }, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        return Results.Ok(new FallbackManualResponse(true));
    }

    // ---------------- 6.2.6 confirm ---------------------------------------

    private static async Task<IResult> ConfirmCheckInAsync(
        [FromBody] CheckInConfirmRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        INotificationOutboxRepository outbox,
        CancellationToken ct)
    {
        var checkIn = await db.CheckIns.FirstOrDefaultAsync(c => c.Id == body.CheckInId && c.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (checkIn is null) return Results.NotFound(new { error = "CheckIn não encontrado" });

        var now = DateTimeOffset.UtcNow;
        checkIn.FinishedAt = now;
        checkIn.GeoLat = body.GeoLat;
        checkIn.GeoLng = body.GeoLng;

        await db.CheckInEvents.AddAsync(new CheckInEventEntity
        {
            Id = Guid.NewGuid(),
            CheckInId = checkIn.Id,
            EventType = "Confirmed",
            PayloadJson = JsonSerializer.Serialize(new { geoLat = body.GeoLat, geoLng = body.GeoLng }),
            OccurredAt = now
        }, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        var child = await db.Children.FirstAsync(c => c.Id == checkIn.ChildId, ct).ConfigureAwait(false);
        await outbox.EnqueueAsync(new NotificationMessage(
            TenantId: tenant.TenantId,
            FamilyId: child.FamilyId,
            ChildId: child.Id,
            CheckInId: checkIn.Id,
            Template: NotificationTemplate.CheckInApproved,
            PayloadJson: JsonSerializer.Serialize(new { confirmed = true, finishedAt = now }),
            CreatedAt: now), ct).ConfigureAwait(false);

        return Results.Ok(new CheckInConfirmResponse(true, now));
    }

    // Inverso simétrico do toy-encrypt no FamilyEndpoints.CreateChildAsync.
    private static string DecryptDisplayName(byte[] cipher, string seed)
    {
        if (cipher.Length == 0) return string.Empty;
        var seedBytes = System.Text.Encoding.UTF8.GetBytes(seed);
        var output = new byte[cipher.Length];
        for (int i = 0; i < cipher.Length; i++)
        {
            output[i] = (byte)(cipher[i] ^ seedBytes[i % seedBytes.Length]);
        }
        return System.Text.Encoding.UTF8.GetString(output);
    }
}
