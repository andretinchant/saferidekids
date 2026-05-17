using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeRideKids.Biometria.Api.Contracts;
using SafeRideKids.Biometria.Core.Domain;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;
using SafeRideKids.Biometria.Tests.TestSupport;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Endpoints;

public class CheckInVerifyEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CheckInVerifyEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Dev-Tenant", "tenant-checkin");
        _client.DefaultRequestHeaders.Add("X-Dev-Actor", "motorista-A");
    }

    [Theory]
    [InlineData(VerificationOutcome.Approved, "Approved", "confirm")]
    [InlineData(VerificationOutcome.Inconclusive, "Inconclusive", "fallback_pin")]
    [InlineData(VerificationOutcome.Rejected, "Rejected", "fallback_manual")]
    public async Task Verify_mapeia_outcome_e_grava_notificacao_no_outbox(VerificationOutcome outcome, string expectedOutcomeText, string expectedSuggestion)
    {
        var (checkInId, _, _) = await SeedCheckInAsync();

        // Configura mock provider para devolver o outcome do teste.
        foreach (var p in _factory.MockProviders)
        {
            p.OnVerify = req => new VerificationResult(outcome, outcome == VerificationOutcome.Approved ? 0.95 : 0.5, true, null, 150, 1_000_000);
        }

        var resp = await _client.PostAsJsonAsync("/api/v1/motorista/checkin/verify",
            new CheckInVerifyRequest(checkInId, "mock-session-x"));

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<CheckInVerifyResponse>();
        body.Should().NotBeNull();
        body!.Outcome.Should().Be(expectedOutcomeText);
        body.SuggestNextAction.Should().Be(expectedSuggestion);

        // Verifica que persistiu no DB.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BiometriaDbContext>();
        var ci = await db.CheckIns.FirstAsync(c => c.Id == checkInId);
        ci.Result.Should().NotBeNull();
        ci.LivenessPassed.Should().BeTrue();
        ci.LatencyMs.Should().Be(150);

        // Verifica que notificação foi enfileirada.
        var notif = await db.NotificationOutbox.FirstOrDefaultAsync(n => n.CheckInId == checkInId);
        notif.Should().NotBeNull();
        notif!.Status.Should().Be("pending");
    }

    private async Task<(Guid checkInId, Guid childId, Guid routeStopId)> SeedCheckInAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BiometriaDbContext>();

        var family = new FamilyEntity { Id = Guid.NewGuid(), TenantId = "tenant-checkin", ResponsavelEmailHash = "h", CreatedAt = DateTimeOffset.UtcNow };
        var child = new ChildEntity { Id = Guid.NewGuid(), FamilyId = family.Id, TenantId = "tenant-checkin", DisplayNameEncrypted = new byte[] { 1 }, BirthYear = 2018, SchoolName = "Test", BoardingMode = "facial", CreatedAt = DateTimeOffset.UtcNow };
        var route = new RouteEntity { Id = Guid.NewGuid(), TenantId = "tenant-checkin", MotoristaId = "motorista-A", VehiclePlate = "ABC1234", ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow), CreatedAt = DateTimeOffset.UtcNow };
        var stop = new RouteStopEntity { Id = Guid.NewGuid(), RouteId = route.Id, ChildId = child.Id, StopOrder = 1, ExpectedPickupTime = new TimeOnly(7, 30), AddressLabel = "Rua X" };
        var consent = new ConsentEntity
        {
            Id = Guid.NewGuid(), TenantId = "tenant-checkin", FamilyId = family.Id, ChildId = child.Id,
            GrantedBy = "Mae", ResponsavelCpfHash = "h", GrantedAt = DateTimeOffset.UtcNow,
            Scope = "biometria_checkin", ConsentTextHash = "h", IpAddress = "127.0.0.1", UserAgent = "ua", SignedTextStorageKey = "k"
        };
        var enrollment = new EnrollmentEntity
        {
            Id = Guid.NewGuid(), TenantId = "tenant-checkin", ChildId = child.Id, ProviderId = "aws-rekognition",
            ProviderReferenceId = "face-id-1", ConsentId = consent.Id, Status = EnrollmentStatus.Active,
            EnrolledAt = DateTimeOffset.UtcNow, ExpiresAt = DateTimeOffset.UtcNow.AddMonths(6)
        };
        var checkIn = new CheckInEntity
        {
            Id = Guid.NewGuid(), TenantId = "tenant-checkin", RouteId = route.Id, RouteStopId = stop.Id,
            ChildId = child.Id, MotoristaId = "motorista-A", StartedAt = DateTimeOffset.UtcNow,
            ProviderId = "aws-rekognition", ProviderSessionId = "mock-session-x"
        };

        db.Families.Add(family);
        db.Children.Add(child);
        db.Routes.Add(route);
        db.RouteStops.Add(stop);
        db.Consents.Add(consent);
        db.Enrollments.Add(enrollment);
        db.CheckIns.Add(checkIn);
        await db.SaveChangesAsync();

        return (checkIn.Id, child.Id, stop.Id);
    }
}
