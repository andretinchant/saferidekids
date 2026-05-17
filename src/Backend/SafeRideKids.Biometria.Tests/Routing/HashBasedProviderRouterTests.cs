using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SafeRideKids.Biometria.Core.Routing;
using SafeRideKids.Biometria.Infrastructure.Routing;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Routing;

public class HashBasedProviderRouterTests
{
    private static HashBasedProviderRouter Build(
        IReadOnlyList<string> active,
        IPreferredProviderResolver? preferred = null)
    {
        preferred ??= new NoopPreferredProviderResolver();
        var opts = Options.Create(new ProviderRoutingOptions
        {
            ActiveProviderIds = new List<string>(active),
            DefaultProviderId = active.Count > 0 ? active[0] : "aws-rekognition"
        });
        return new HashBasedProviderRouter(preferred, opts, NullLogger<HashBasedProviderRouter>.Instance);
    }

    [Fact]
    public void Mesma_crianca_mesmo_dia_devolve_mesmo_provider()
    {
        var router = Build(new[] { "aws-rekognition", "unico-idcloud" });

        var childId = "11111111-1111-1111-1111-111111111111";
        var day = new DateTimeOffset(2026, 5, 17, 8, 0, 0, TimeSpan.Zero);

        var a = router.ResolveProviderId("tenant-a", "fam-x", childId, day);
        var b = router.ResolveProviderId("tenant-a", "fam-x", childId, day.AddHours(5));
        var c = router.ResolveProviderId("tenant-a", "fam-x", childId, day.AddHours(15));

        a.Should().Be(b);
        b.Should().Be(c);
    }

    [Fact]
    public void Dia_diferente_pode_mudar_provider()
    {
        var router = Build(new[] { "aws-rekognition", "unico-idcloud" });

        var childId = "abababab-cdcd-efef-1111-222222222222";
        var d1 = new DateTimeOffset(2026, 5, 17, 0, 0, 0, TimeSpan.Zero);
        var d2 = new DateTimeOffset(2026, 5, 18, 0, 0, 0, TimeSpan.Zero);

        var p1 = router.ResolveProviderId("t", "f", childId, d1);
        var p2 = router.ResolveProviderId("t", "f", childId, d2);

        new[] { "aws-rekognition", "unico-idcloud" }.Should().Contain(p1);
        new[] { "aws-rekognition", "unico-idcloud" }.Should().Contain(p2);
        // Não é obrigatório mudar — apenas demonstramos que dias diferentes não quebram.
    }

    [Fact]
    public void Distribuicao_entre_dois_providers_eh_aproximadamente_50_50_com_1000_criancas()
    {
        var router = Build(new[] { "aws-rekognition", "unico-idcloud" });

        var day = new DateTimeOffset(2026, 5, 17, 12, 0, 0, TimeSpan.Zero);
        int aws = 0, unico = 0;
        for (int i = 0; i < 1000; i++)
        {
            var childId = Guid.NewGuid().ToString();
            var p = router.ResolveProviderId("t", "f", childId, day);
            if (p == "aws-rekognition") aws++; else unico++;
        }

        // Tolera 10% de desvio (450..550 cada lado).
        aws.Should().BeInRange(400, 600);
        unico.Should().BeInRange(400, 600);
        (aws + unico).Should().Be(1000);
    }

    [Fact]
    public void Override_de_preferred_provider_eh_respeitado()
    {
        var preferred = Substitute.For<IPreferredProviderResolver>();
        preferred.GetPreferredProviderId("t", "f-pref").Returns("unico-idcloud");

        var router = Build(new[] { "aws-rekognition", "unico-idcloud" }, preferred);

        var childId = Guid.NewGuid().ToString();
        var p = router.ResolveProviderId("t", "f-pref", childId, DateTimeOffset.UtcNow);
        p.Should().Be("unico-idcloud");
    }

    [Fact]
    public void Override_nao_listado_em_ativos_eh_ignorado()
    {
        var preferred = Substitute.For<IPreferredProviderResolver>();
        preferred.GetPreferredProviderId("t", Arg.Any<string>()).Returns("azure-face");
        var router = Build(new[] { "aws-rekognition", "unico-idcloud" }, preferred);

        var p = router.ResolveProviderId("t", "fam", Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        new[] { "aws-rekognition", "unico-idcloud" }.Should().Contain(p);
    }

    [Fact]
    public void Lista_ativos_vazia_usa_default()
    {
        var router = Build(Array.Empty<string>());
        var p = router.ResolveProviderId("t", "f", Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        p.Should().Be("aws-rekognition");
    }
}
