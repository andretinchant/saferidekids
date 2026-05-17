using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Providers.Unico;
using SafeRideKids.Biometria.Tests.TestSupport;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Providers;

public class UnicoFaceVerificationProviderTests
{
    private static UnicoFaceVerificationProvider Build(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var http = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://api.unico.test")
        };
        return new UnicoFaceVerificationProvider(http,
            Options.Create(new UnicoOptions { BaseUrl = "https://api.unico.test", ApiKey = "k" }),
            NullLogger<UnicoFaceVerificationProvider>.Instance);
    }

    private static HttpResponseMessage Json(object payload, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    [Fact]
    public async Task EnrollAsync_chama_POST_v1_people_e_devolve_personId()
    {
        var provider = Build(req =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/v1/people");
            req.Method.Should().Be(HttpMethod.Post);
            return Json(new { personId = "person-abc" });
        });

        var res = await provider.EnrollAsync(new EnrollmentRequest("child-1", "tn", TestImage.JpegSet(3), "c1"), CancellationToken.None);
        res.Success.Should().BeTrue();
        res.ProviderReferenceId.Should().Be("person-abc");
    }

    [Fact]
    public async Task EnrollAsync_com_imagens_fora_do_range_devolve_erro_local()
    {
        var provider = Build(_ => Json(new { }));
        var res = await provider.EnrollAsync(new EnrollmentRequest("c", "t", TestImage.JpegSet(7), "c"), CancellationToken.None);
        res.Success.Should().BeFalse();
        res.ErrorCode.Should().Be("InvalidImageCount");
    }

    [Fact]
    public async Task EnrollAsync_quando_API_retorna_erro_devolve_ProviderError()
    {
        var provider = Build(_ => new HttpResponseMessage(HttpStatusCode.BadGateway)
        {
            Content = new StringContent("upstream down")
        });

        var res = await provider.EnrollAsync(new EnrollmentRequest("c", "t", TestImage.JpegSet(3), "c"), CancellationToken.None);
        res.Success.Should().BeFalse();
        res.ErrorCode.Should().Be("ProviderError");
    }

    [Fact]
    public async Task StartLiveness_chama_POST_v1_processes_e_retorna_sessionId()
    {
        var provider = Build(req =>
        {
            req.RequestUri!.AbsolutePath.Should().Be("/v1/processes");
            return Json(new { processId = "proc-42", sdkToken = "tk" });
        });

        var info = await provider.StartLivenessSessionAsync(new LivenessSessionRequest("ck", "child", "tn"), CancellationToken.None);
        info.SessionId.Should().Be("proc-42");
        info.SdkConfig["processId"].Should().Be("proc-42");
        info.SdkConfig["sdkToken"].Should().Be("tk");
    }

    [Fact]
    public async Task VerifyAsync_pipeline_completo_com_score_alto_devolve_Approved()
    {
        var step = 0;
        var provider = Build(req =>
        {
            step++;
            if (step == 1)
            {
                req.RequestUri!.AbsolutePath.Should().StartWith("/v1/processes/");
                return Json(new { status = "COMPLETED", livenessPassed = true });
            }
            // step 2 → match
            req.RequestUri!.AbsolutePath.Should().Be("/v1/match");
            return Json(new { score = 96f });
        });

        var res = await provider.VerifyAsync(new VerificationRequest("proc-1", "c", "t", "person-ref"), CancellationToken.None);
        res.Outcome.Should().Be(VerificationOutcome.Approved);
        res.LivenessPassed.Should().BeTrue();
        res.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task VerifyAsync_liveness_falho_devolve_Rejected()
    {
        var provider = Build(req =>
        {
            return Json(new { status = "COMPLETED", livenessPassed = false });
        });

        var res = await provider.VerifyAsync(new VerificationRequest("p", "c", "t", "r"), CancellationToken.None);
        res.Outcome.Should().Be(VerificationOutcome.Rejected);
        res.LivenessPassed.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyAsync_score_intermediario_devolve_Inconclusive()
    {
        var step = 0;
        var provider = Build(req =>
        {
            step++;
            return step == 1
                ? Json(new { status = "COMPLETED", livenessPassed = true })
                : Json(new { score = 80f });
        });

        var res = await provider.VerifyAsync(new VerificationRequest("p", "c", "t", "r"), CancellationToken.None);
        res.Outcome.Should().Be(VerificationOutcome.Inconclusive);
    }

    [Fact]
    public async Task DeleteEnrollmentAsync_aceita_404_idempotente()
    {
        var provider = Build(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        // Não lança.
        await provider.DeleteEnrollmentAsync("nonexistent-person", CancellationToken.None);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }
}
