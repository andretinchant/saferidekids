using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Providers.Stubs;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Providers;

public class StubProvidersTests
{
    [Theory]
    [InlineData(typeof(AzureFaceFaceVerificationProvider), "azure-face")]
    [InlineData(typeof(FaceTecFaceVerificationProvider), "facetec-zoom")]
    [InlineData(typeof(SerproDatavalidFaceVerificationProvider), "serpro-datavalid")]
    public void Stub_expoe_ProviderId_correto(Type t, string expected)
    {
        var instance = (IFaceVerificationProvider)Activator.CreateInstance(t)!;
        instance.ProviderId.Should().Be(expected);
    }

    [Theory]
    [InlineData(typeof(AzureFaceFaceVerificationProvider))]
    [InlineData(typeof(FaceTecFaceVerificationProvider))]
    [InlineData(typeof(SerproDatavalidFaceVerificationProvider))]
    public async Task Stub_lanca_NotSupportedException_em_qualquer_metodo(Type t)
    {
        var instance = (IFaceVerificationProvider)Activator.CreateInstance(t)!;

        await FluentActions.Awaiting(() => instance.EnrollAsync(
            new EnrollmentRequest("c", "t", new[] { new byte[] { 1 } }, "x"), CancellationToken.None))
            .Should().ThrowAsync<NotSupportedException>();

        await FluentActions.Awaiting(() => instance.StartLivenessSessionAsync(
            new LivenessSessionRequest("ck", "ch", "tn"), CancellationToken.None))
            .Should().ThrowAsync<NotSupportedException>();

        await FluentActions.Awaiting(() => instance.VerifyAsync(
            new VerificationRequest("s", "c", "t", "r"), CancellationToken.None))
            .Should().ThrowAsync<NotSupportedException>();

        await FluentActions.Awaiting(() => instance.DeleteEnrollmentAsync("r", CancellationToken.None))
            .Should().ThrowAsync<NotSupportedException>();
    }
}
