using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SafeRideKids.Biometria.Core.Fallback;
using SafeRideKids.Biometria.Core.Multitenancy;
using SafeRideKids.Biometria.Core.Security;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;

namespace SafeRideKids.Biometria.Infrastructure.Fallback;

/// <summary>
/// Serviço de PIN de fallback conforme CONTRACTS Seção 7:
/// 6 dígitos · CSPRNG · 24h · 3 tentativas · armazenado apenas hashado.
/// </summary>
public sealed class FallbackPinService : IFallbackPinService
{
    private const int PinLength = 6;
    private const short MaxAttempts = 3;
    private static readonly TimeSpan PinTtl = TimeSpan.FromHours(24);

    private readonly BiometriaDbContext _db;
    private readonly IHmacHasher _hasher;
    private readonly ITenantSecretsProvider _secrets;
    private readonly ILogger<FallbackPinService> _logger;

    public FallbackPinService(
        BiometriaDbContext db,
        IHmacHasher hasher,
        ITenantSecretsProvider secrets,
        ILogger<FallbackPinService> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GeneratePinResult> GeneratePinAsync(string tenantId, Guid childId, CancellationToken cancellationToken)
    {
        // Revoga PINs ativos pré-existentes (idempotência).
        var existing = await _db.FallbackPins
            .Where(p => p.TenantId == tenantId && p.ChildId == childId && p.ConsumedAt == null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        foreach (var p in existing.Where(p => p.ValidUntil > DateTimeOffset.UtcNow))
        {
            p.ConsumedAt = DateTimeOffset.UtcNow;
        }

        var pin = GenerateNumericPin(PinLength);
        var pinHash = await _hasher.HashAsync(tenantId, pin, cancellationToken).ConfigureAwait(false);

        var validFrom = DateTimeOffset.UtcNow;
        var validUntil = validFrom.Add(PinTtl);

        await _db.FallbackPins.AddAsync(new FallbackPinEntity
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ChildId = childId,
            PinHash = pinHash,
            ValidFrom = validFrom,
            ValidUntil = validUntil,
            Attempts = 0
        }, cancellationToken).ConfigureAwait(false);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // NUNCA logar o PIN claro. childId é PII — logar só os 4 primeiros chars do GUID.
        _logger.LogInformation(
            "FallbackPin generated: tenant={Tenant} childIdPrefix={Prefix}",
            tenantId, childId.ToString().Substring(0, 4));

        return new GeneratePinResult(pin, validFrom, validUntil);
    }

    public async Task<PinValidationResult> ValidatePinAsync(string tenantId, Guid childId, string pin, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(pin))
            return new PinValidationResult(PinValidationOutcome.Invalid, 0);

        var entity = await _db.FallbackPins
            .Where(p => p.TenantId == tenantId && p.ChildId == childId && p.ConsumedAt == null)
            .OrderByDescending(p => p.ValidFrom)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (entity is null)
            return new PinValidationResult(PinValidationOutcome.NotFound, 0);

        var now = DateTimeOffset.UtcNow;
        if (entity.ValidUntil < now)
        {
            // Marca expirado consumindo a entrada para não reaparecer em queries.
            entity.ConsumedAt = now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PinValidationResult(PinValidationOutcome.Expired, 0);
        }

        if (entity.Attempts >= MaxAttempts)
        {
            entity.ConsumedAt = now;
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return new PinValidationResult(PinValidationOutcome.Blocked, 0);
        }

        var providedHash = await _hasher.HashAsync(tenantId, pin, cancellationToken).ConfigureAwait(false);
        if (!ConstantTimeEquals(providedHash, entity.PinHash))
        {
            entity.Attempts = (short)(entity.Attempts + 1);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            if (entity.Attempts >= MaxAttempts)
            {
                entity.ConsumedAt = now;
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return new PinValidationResult(PinValidationOutcome.Blocked, 0);
            }
            return new PinValidationResult(PinValidationOutcome.Invalid, MaxAttempts - entity.Attempts);
        }

        // Sucesso.
        entity.ConsumedAt = now;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new PinValidationResult(PinValidationOutcome.Valid, MaxAttempts - entity.Attempts);
    }

    private static string GenerateNumericPin(int length)
    {
        Span<byte> buf = stackalloc byte[length * 2];
        RandomNumberGenerator.Fill(buf);
        var sb = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            // Combina dois bytes para reduzir bias modular.
            var n = (buf[i * 2] << 8) | buf[i * 2 + 1];
            sb.Append((n % 10).ToString());
        }
        return sb.ToString();
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        int diff = 0;
        for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
