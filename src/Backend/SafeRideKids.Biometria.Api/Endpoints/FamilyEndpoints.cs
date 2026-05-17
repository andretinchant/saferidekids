using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mime;
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
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Core.Security;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Api.Endpoints;

/// <summary>
/// Endpoints da Família (responsável legal). 6.1 da CONTRACTS.md.
/// </summary>
public static class FamilyEndpoints
{
    public static IEndpointRouteBuilder MapFamilyEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/v1/family").WithTags("family");

        grp.MapPost("/register", RegisterFamilyAsync);
        grp.MapPost("/children", CreateChildAsync);
        grp.MapPost("/consent", GrantConsentAsync);
        grp.MapPost("/children/{childId:guid}/enroll", EnrollChildAsync).DisableAntiforgery();
        grp.MapGet("/children/{childId:guid}/enrollment", GetEnrollmentStatusAsync);
        grp.MapDelete("/children/{childId:guid}/enrollment", DeleteEnrollmentAsync);

        return app;
    }

    // ---------------- 6.1.1 register --------------------------------------

    private static async Task<IResult> RegisterFamilyAsync(
        [FromBody] FamilyRegisterRequest body,
        ITenantContext tenant,
        IHmacHasher hasher,
        IAuditLogger audit,
        BiometriaDbContext db,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "email e name são obrigatórios" });

        var tenantId = !string.IsNullOrWhiteSpace(body.TenantId) ? body.TenantId : tenant.TenantId;
        var emailHash = await hasher.HashAsync(tenantId, body.Email, ct).ConfigureAwait(false);

        var existing = await db.Families
            .FirstOrDefaultAsync(f => f.TenantId == tenantId && f.ResponsavelEmailHash == emailHash, ct)
            .ConfigureAwait(false);
        if (existing is not null)
        {
            return Results.Ok(new FamilyRegisterResponse(existing.Id));
        }

        var family = new FamilyEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ResponsavelEmailHash = emailHash,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await db.Families.AddAsync(family, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await audit.LogAsync(tenantId, await hasher.HashAsync(tenantId, body.Email, ct), ActorType.Family,
            "FamilyRegistered", "Family", family.Id.ToString(), "{}", ct).ConfigureAwait(false);

        return Results.Ok(new FamilyRegisterResponse(family.Id));
    }

    // ---------------- 6.1.2 create child ----------------------------------

    private static async Task<IResult> CreateChildAsync(
        [FromBody] FamilyChildCreateRequest body,
        ITenantContext tenant,
        BiometriaDbContext db,
        IAuditLogger audit,
        IHmacHasher hasher,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.DisplayName) || body.BirthYear <= 1900)
            return Results.BadRequest(new { error = "displayName e birthYear válidos são obrigatórios" });

        var family = await db.Families
            .FirstOrDefaultAsync(f => f.Id == body.FamilyId && f.TenantId == tenant.TenantId, ct).ConfigureAwait(false);
        if (family is null) return Results.NotFound(new { error = "Family não encontrada para o tenant" });

        var nameBytes = System.Text.Encoding.UTF8.GetBytes(body.DisplayName);
        // POC: cifrado simbólico (XOR com tenantId hash). Em produção, chamar pgp_sym_encrypt
        // via uma column projection do EF Core ou função SQL inline.
        // TODO[encryption-prod]: substituir por wrapper KmsEncrypted + Npgsql HasValueGenerator.
        var encrypted = SymmetricToyEncrypt(nameBytes, tenant.TenantId);

        var child = new ChildEntity
        {
            Id = Guid.NewGuid(),
            FamilyId = family.Id,
            TenantId = tenant.TenantId,
            DisplayNameEncrypted = encrypted,
            BirthYear = body.BirthYear,
            SchoolName = body.School,
            BoardingMode = string.IsNullOrWhiteSpace(body.BoardingMode) ? BoardingMode.Manual : body.BoardingMode,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await db.Children.AddAsync(child, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await audit.LogAsync(tenant.TenantId,
            await hasher.HashAsync(tenant.TenantId, tenant.ActorId, ct),
            ActorType.Family, "ChildCreated", "Child", child.Id.ToString(), "{}", ct).ConfigureAwait(false);

        return Results.Ok(new FamilyChildCreateResponse(child.Id));
    }

    // ---------------- 6.1.3 consent ---------------------------------------

    private static async Task<IResult> GrantConsentAsync(
        [FromBody] FamilyConsentRequest body,
        HttpContext http,
        ITenantContext tenant,
        IHmacHasher hasher,
        BiometriaDbContext db,
        IAuditLogger audit,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.GrantedBy) || string.IsNullOrWhiteSpace(body.Cpf))
            return Results.BadRequest(new { error = "grantedBy e cpf obrigatórios" });

        var child = await db.Children.FirstOrDefaultAsync(c => c.Id == body.ChildId && c.TenantId == tenant.TenantId, ct)
            .ConfigureAwait(false);
        if (child is null) return Results.NotFound(new { error = "Child não encontrada" });

        var cpfHash = await hasher.HashAsync(tenant.TenantId, body.Cpf, ct).ConfigureAwait(false);
        var textHash = await hasher.HashAsync(tenant.TenantId, body.ConsentTextVersion ?? "v1", ct).ConfigureAwait(false);

        // Revoga consents anteriores ativos para esta criança (índice único parcial garante 1 ativo).
        var existing = await db.Consents.Where(c => c.ChildId == body.ChildId && c.RevokedAt == null).ToListAsync(ct).ConfigureAwait(false);
        foreach (var c in existing) c.RevokedAt = DateTimeOffset.UtcNow;

        var consent = new ConsentEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId,
            FamilyId = child.FamilyId,
            ChildId = child.Id,
            GrantedBy = body.GrantedBy,
            ResponsavelCpfHash = cpfHash,
            GrantedAt = DateTimeOffset.UtcNow,
            Scope = "biometria_checkin",
            ConsentTextHash = textHash,
            IpAddress = http.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0",
            UserAgent = http.Request.Headers.UserAgent.ToString() ?? string.Empty,
            // SignedTextStorageKey: na POC, geramos placeholder; integração com S3 ocorre via ITemplateStorage.
            SignedTextStorageKey = $"consents/{tenant.TenantId}/pending/{Guid.NewGuid()}"
        };
        await db.Consents.AddAsync(consent, ct).ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await audit.LogAsync(tenant.TenantId,
            await hasher.HashAsync(tenant.TenantId, tenant.ActorId, ct),
            ActorType.Family, "ConsentGranted", "Consent", consent.Id.ToString(),
            JsonSerializer.Serialize(new { childId = consent.ChildId, scope = consent.Scope }), ct).ConfigureAwait(false);

        return Results.Ok(new FamilyConsentResponse(consent.Id));
    }

    // ---------------- 6.1.4 enroll ----------------------------------------

    private static async Task<IResult> EnrollChildAsync(
        Guid childId,
        HttpContext http,
        ITenantContext tenant,
        BiometriaDbContext db,
        IFaceVerificationProviderResolver providers,
        IAuditLogger audit,
        IHmacHasher hasher,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("FamilyEndpoints.Enroll");

        if (!http.Request.HasFormContentType)
            return Results.BadRequest(new { error = "Requer multipart/form-data" });

        var form = await http.Request.ReadFormAsync(ct).ConfigureAwait(false);
        var consentIdRaw = form["consentId"].ToString();
        if (!Guid.TryParse(consentIdRaw, out var consentId))
            return Results.BadRequest(new { error = "consentId inválido ou ausente" });

        var consent = await db.Consents.FirstOrDefaultAsync(c => c.Id == consentId && c.ChildId == childId && c.RevokedAt == null, ct)
            .ConfigureAwait(false);
        if (consent is null) return Results.BadRequest(new { error = "Consentimento ativo não encontrado para esta criança" });

        var images = new List<byte[]>();
        foreach (var file in form.Files)
        {
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms, ct).ConfigureAwait(false);
            images.Add(ms.ToArray());
        }

        if (images.Count is < 3 or > 5)
            return Results.BadRequest(new { error = $"Esperado 3 a 5 imagens; recebido {images.Count}" });

        // Enrola em paralelo em todos providers ativos (lista vem do resolver).
        var providersList = providers.All().ToList();
        var enrollTasks = providersList.Select(p => p.EnrollAsync(new EnrollmentRequest(
            ChildId: childId.ToString(),
            TenantId: tenant.TenantId,
            Images: images,
            ConsentReferenceId: consent.Id.ToString()), ct)).ToList();

        var results = await Task.WhenAll(enrollTasks).ConfigureAwait(false);

        // Persiste 1 enrollment por provedor (revogando ativo anterior).
        var summaries = new List<EnrollmentSummary>();
        for (int i = 0; i < providersList.Count; i++)
        {
            var provider = providersList[i];
            var res = results[i];

            if (!res.Success)
            {
                logger.LogWarning("Enroll falhou no provider={Provider}: {Error}", provider.ProviderId, res.ErrorCode);
                summaries.Add(new EnrollmentSummary(provider.ProviderId, "failed", null, res.ErrorCode));
                continue;
            }

            var existing = await db.Enrollments
                .Where(e => e.ChildId == childId && e.ProviderId == provider.ProviderId && e.Status == EnrollmentStatus.Active)
                .ToListAsync(ct).ConfigureAwait(false);
            foreach (var e in existing) e.Status = EnrollmentStatus.Expired;

            var now = DateTimeOffset.UtcNow;
            var entity = new EnrollmentEntity
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.TenantId,
                ChildId = childId,
                ProviderId = provider.ProviderId,
                ProviderReferenceId = res.ProviderReferenceId,
                TemplateStorageKey = null,            // POC: provedor armazena. Quando provider devolver template, persistir via ITemplateStorage.
                ConsentId = consent.Id,
                Status = EnrollmentStatus.Active,
                EnrolledAt = now,
                ExpiresAt = now.AddMonths(6)
            };
            await db.Enrollments.AddAsync(entity, ct).ConfigureAwait(false);
            summaries.Add(new EnrollmentSummary(provider.ProviderId, "active", res.ProviderReferenceId, null));
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await audit.LogAsync(tenant.TenantId,
            await hasher.HashAsync(tenant.TenantId, tenant.ActorId, ct),
            ActorType.Family, "EnrollmentCreated", "Child", childId.ToString(),
            JsonSerializer.Serialize(new { providers = summaries.Select(s => s.ProviderId) }), ct).ConfigureAwait(false);

        return Results.Ok(new FamilyEnrollResponse(summaries));
    }

    // ---------------- 6.1.5 enrollment status -----------------------------

    private static async Task<IResult> GetEnrollmentStatusAsync(
        Guid childId,
        ITenantContext tenant,
        BiometriaDbContext db,
        CancellationToken ct)
    {
        var list = await db.Enrollments
            .Where(e => e.ChildId == childId && e.TenantId == tenant.TenantId)
            .ToListAsync(ct).ConfigureAwait(false);

        var summaries = list.Select(e => new EnrollmentSummary(e.ProviderId, e.Status, e.ProviderReferenceId, null)).ToList();
        var expiry = list.Where(e => e.Status == EnrollmentStatus.Active).Min(e => (DateTimeOffset?)e.ExpiresAt);

        return Results.Ok(new FamilyEnrollmentStatusResponse(childId, summaries, expiry));
    }

    // ---------------- 6.1.6 DSR delete enrollment -------------------------

    private static async Task<IResult> DeleteEnrollmentAsync(
        Guid childId,
        ITenantContext tenant,
        BiometriaDbContext db,
        IFaceVerificationProviderResolver providers,
        IAuditLogger audit,
        IHmacHasher hasher,
        CancellationToken ct)
    {
        var list = await db.Enrollments.Where(e => e.ChildId == childId && e.TenantId == tenant.TenantId && e.Status == EnrollmentStatus.Active)
            .ToListAsync(ct).ConfigureAwait(false);

        var tasks = list.Select(e => providers.Resolve(e.ProviderId).DeleteEnrollmentAsync(e.ProviderReferenceId, ct)).ToList();
        await Task.WhenAll(tasks).ConfigureAwait(false);

        foreach (var e in list)
        {
            e.Status = EnrollmentStatus.Deleted;
            e.DeletedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await audit.LogAsync(tenant.TenantId,
            await hasher.HashAsync(tenant.TenantId, tenant.ActorId, ct),
            ActorType.Family, "DsrDeleteExecuted", "Child", childId.ToString(), "{}", ct).ConfigureAwait(false);

        return Results.Ok(new { deleted = list.Count });
    }

    // ---------------- helpers ---------------------------------------------

    /// <summary>
    /// POC-grade: cifra "símbolica" via XOR com seed do tenant. Em produção, substituir por
    /// pgp_sym_encrypt diretamente no INSERT (raw SQL ou função do EF). Mantém payload
    /// non-cleartext em REPL casual e deixa marker para refatorar.
    /// </summary>
    private static byte[] SymmetricToyEncrypt(byte[] plain, string seed)
    {
        var seedBytes = System.Text.Encoding.UTF8.GetBytes(seed);
        var output = new byte[plain.Length];
        for (int i = 0; i < plain.Length; i++)
        {
            output[i] = (byte)(plain[i] ^ seedBytes[i % seedBytes.Length]);
        }
        return output;
    }
}
