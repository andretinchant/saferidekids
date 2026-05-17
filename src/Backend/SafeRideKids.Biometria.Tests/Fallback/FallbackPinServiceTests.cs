using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SafeRideKids.Biometria.Core.Fallback;
using SafeRideKids.Biometria.Infrastructure.Fallback;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Security;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Fallback;

public class FallbackPinServiceTests : IAsyncLifetime
{
    private BiometriaDbContext _db = null!;
    private FallbackPinService _svc = null!;
    private const string Tenant = "tenant-A";

    public Task InitializeAsync()
    {
        var opts = new DbContextOptionsBuilder<BiometriaDbContext>()
            .UseInMemoryDatabase($"fp_{Guid.NewGuid()}")
            .Options;
        _db = new BiometriaDbContext(opts);

        var secrets = new InMemoryTenantSecretsProvider();
        var hasher = new HmacHasher(secrets);
        _svc = new FallbackPinService(_db, hasher, secrets, NullLogger<FallbackPinService>.Instance);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task GeneratePin_retorna_pin_de_6_digitos_e_persiste_apenas_hash()
    {
        var childId = Guid.NewGuid();
        var result = await _svc.GeneratePinAsync(Tenant, childId, CancellationToken.None);

        result.PlaintextPin.Should().HaveLength(6);
        result.PlaintextPin.Should().MatchRegex("^[0-9]{6}$");

        var persisted = await _db.FallbackPins.SingleAsync(p => p.ChildId == childId);
        persisted.PinHash.Should().NotBe(result.PlaintextPin);
        persisted.ValidUntil.Should().BeAfter(persisted.ValidFrom);
        (persisted.ValidUntil - persisted.ValidFrom).Should().BeCloseTo(TimeSpan.FromHours(24), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task ValidatePin_correto_marca_consumido_e_retorna_valid()
    {
        var childId = Guid.NewGuid();
        var gen = await _svc.GeneratePinAsync(Tenant, childId, CancellationToken.None);

        var res = await _svc.ValidatePinAsync(Tenant, childId, gen.PlaintextPin, CancellationToken.None);
        res.Outcome.Should().Be(PinValidationOutcome.Valid);

        var persisted = await _db.FallbackPins.SingleAsync(p => p.ChildId == childId);
        persisted.ConsumedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidatePin_incorreto_3_vezes_bloqueia()
    {
        var childId = Guid.NewGuid();
        await _svc.GeneratePinAsync(Tenant, childId, CancellationToken.None);

        var r1 = await _svc.ValidatePinAsync(Tenant, childId, "000000", CancellationToken.None);
        var r2 = await _svc.ValidatePinAsync(Tenant, childId, "111111", CancellationToken.None);
        var r3 = await _svc.ValidatePinAsync(Tenant, childId, "222222", CancellationToken.None);

        r1.Outcome.Should().Be(PinValidationOutcome.Invalid);
        r2.Outcome.Should().Be(PinValidationOutcome.Invalid);
        // r3 atinge limite → Blocked (após terceira falha).
        r3.Outcome.Should().Be(PinValidationOutcome.Blocked);
    }

    [Fact]
    public async Task ValidatePin_expirado_retorna_expired()
    {
        var childId = Guid.NewGuid();
        var gen = await _svc.GeneratePinAsync(Tenant, childId, CancellationToken.None);

        // Adianta validity para que esteja vencido.
        var pin = await _db.FallbackPins.SingleAsync(p => p.ChildId == childId);
        pin.ValidUntil = DateTimeOffset.UtcNow.AddMinutes(-1);
        await _db.SaveChangesAsync();

        var res = await _svc.ValidatePinAsync(Tenant, childId, gen.PlaintextPin, CancellationToken.None);
        res.Outcome.Should().Be(PinValidationOutcome.Expired);
    }

    [Fact]
    public async Task ValidatePin_sem_pin_gerado_retorna_NotFound()
    {
        var childId = Guid.NewGuid();
        var res = await _svc.ValidatePinAsync(Tenant, childId, "123456", CancellationToken.None);
        res.Outcome.Should().Be(PinValidationOutcome.NotFound);
    }
}
