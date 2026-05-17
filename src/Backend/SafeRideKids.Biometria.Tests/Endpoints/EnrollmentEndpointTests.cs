using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SafeRideKids.Biometria.Api.Contracts;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Infrastructure.Persistence;
using SafeRideKids.Biometria.Infrastructure.Persistence.Entities;
using SafeRideKids.Biometria.Tests.TestSupport;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Endpoints;

public class EnrollmentEndpointTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EnrollmentEndpointTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Dev-Tenant", "test-tenant");
        _client.DefaultRequestHeaders.Add("X-Dev-Actor", "actor-1");
    }

    [Fact]
    public async Task Enroll_com_consent_ativo_e_3_a_5_imagens_grava_enrollment_em_ambos_providers()
    {
        var (familyId, childId, consentId) = await SeedAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(consentId.ToString()), "consentId");
        for (int i = 0; i < 4; i++)
        {
            var byteContent = new ByteArrayContent(TestImage.Jpeg());
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(byteContent, "images", $"face-{i}.jpg");
        }

        var resp = await _client.PostAsync($"/api/v1/family/children/{childId}/enroll", content);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<FamilyEnrollResponse>();
        body.Should().NotBeNull();
        body!.Enrollments.Should().HaveCount(2);
        body.Enrollments.Should().OnlyContain(e => e.Status == "active");
        body.Enrollments.Should().Contain(e => e.ProviderId == "aws-rekognition");
        body.Enrollments.Should().Contain(e => e.ProviderId == "unico-idcloud");
    }

    [Fact]
    public async Task Enroll_com_consent_invalido_devolve_400()
    {
        var (_, childId, _) = await SeedAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(Guid.NewGuid().ToString()), "consentId");
        for (int i = 0; i < 3; i++)
        {
            var byteContent = new ByteArrayContent(TestImage.Jpeg());
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
            content.Add(byteContent, "images", $"f-{i}.jpg");
        }

        var resp = await _client.PostAsync($"/api/v1/family/children/{childId}/enroll", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Enroll_com_menos_de_3_imagens_devolve_400()
    {
        var (_, childId, consentId) = await SeedAsync();

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(consentId.ToString()), "consentId");
        var byteContent = new ByteArrayContent(TestImage.Jpeg());
        byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        content.Add(byteContent, "images", "single.jpg");

        var resp = await _client.PostAsync($"/api/v1/family/children/{childId}/enroll", content);
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    private async Task<(Guid familyId, Guid childId, Guid consentId)> SeedAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BiometriaDbContext>();

        var family = new FamilyEntity { Id = Guid.NewGuid(), TenantId = "test-tenant", ResponsavelEmailHash = "h", CreatedAt = DateTimeOffset.UtcNow };
        var child = new ChildEntity { Id = Guid.NewGuid(), FamilyId = family.Id, TenantId = "test-tenant", DisplayNameEncrypted = new byte[] { 1, 2 }, BirthYear = 2018, SchoolName = "Test", BoardingMode = "facial", CreatedAt = DateTimeOffset.UtcNow };
        var consent = new ConsentEntity
        {
            Id = Guid.NewGuid(), TenantId = "test-tenant", FamilyId = family.Id, ChildId = child.Id,
            GrantedBy = "Mae Teste", ResponsavelCpfHash = "cpfhash", GrantedAt = DateTimeOffset.UtcNow,
            Scope = "biometria_checkin", ConsentTextHash = "th", IpAddress = "127.0.0.1", UserAgent = "test", SignedTextStorageKey = "k"
        };

        db.Families.Add(family);
        db.Children.Add(child);
        db.Consents.Add(consent);
        await db.SaveChangesAsync();

        return (family.Id, child.Id, consent.Id);
    }
}
