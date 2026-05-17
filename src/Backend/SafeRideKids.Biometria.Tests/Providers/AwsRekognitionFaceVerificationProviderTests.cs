using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Rekognition;
using Amazon.Rekognition.Model;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SafeRideKids.Biometria.Core.Providers;
using SafeRideKids.Biometria.Providers.Aws;
using SafeRideKids.Biometria.Tests.TestSupport;
using Xunit;

namespace SafeRideKids.Biometria.Tests.Providers;

public class AwsRekognitionFaceVerificationProviderTests
{
    private static AwsRekognitionFaceVerificationProvider Build(IAmazonRekognition rek) =>
        new(rek, Options.Create(new AwsRekognitionOptions()), NullLogger<AwsRekognitionFaceVerificationProvider>.Instance);

    [Fact]
    public async Task EnrollAsync_chama_IndexFaces_e_devolve_primeiro_FaceId()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        rek.DescribeCollectionAsync(Arg.Any<DescribeCollectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DescribeCollectionResponse());

        rek.IndexFacesAsync(Arg.Any<IndexFacesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IndexFacesResponse
            {
                FaceRecords = new List<FaceRecord> { new() { Face = new Face { FaceId = "face-xyz-123" } } }
            });

        var provider = Build(rek);
        var req = new EnrollmentRequest("child-1", "tenant-X", TestImage.JpegSet(3), "consent-1");
        var res = await provider.EnrollAsync(req, CancellationToken.None);

        res.Success.Should().BeTrue();
        res.ProviderReferenceId.Should().Be("face-xyz-123");
        await rek.Received(3).IndexFacesAsync(Arg.Any<IndexFacesRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task EnrollAsync_com_imagens_fora_do_range_devolve_erro()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        var provider = Build(rek);

        var res = await provider.EnrollAsync(new EnrollmentRequest("child-1", "t", TestImage.JpegSet(1), "c"), CancellationToken.None);
        res.Success.Should().BeFalse();
        res.ErrorCode.Should().Be("InvalidImageCount");
    }

    [Fact]
    public async Task EnrollAsync_quando_IndexFaces_nao_detecta_nada_retorna_NoFaceDetected()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        rek.DescribeCollectionAsync(Arg.Any<DescribeCollectionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DescribeCollectionResponse());
        rek.IndexFacesAsync(Arg.Any<IndexFacesRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IndexFacesResponse { FaceRecords = new List<FaceRecord>() });

        var provider = Build(rek);
        var res = await provider.EnrollAsync(new EnrollmentRequest("c", "t", TestImage.JpegSet(3), "c"), CancellationToken.None);
        res.Success.Should().BeFalse();
        res.ErrorCode.Should().Be("NoFaceDetected");
    }

    [Fact]
    public async Task StartLivenessSessionAsync_devolve_sessionId_da_AWS()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        rek.CreateFaceLivenessSessionAsync(Arg.Any<CreateFaceLivenessSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(new CreateFaceLivenessSessionResponse { SessionId = "sess-1" });

        var provider = Build(rek);
        var info = await provider.StartLivenessSessionAsync(new LivenessSessionRequest("ck1", "ch1", "tn1"), CancellationToken.None);

        info.SessionId.Should().Be("sess-1");
        info.SdkConfig.Should().ContainKey("sessionId");
        info.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task VerifyAsync_liveness_failed_devolve_Rejected()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        rek.GetFaceLivenessSessionResultsAsync(Arg.Any<GetFaceLivenessSessionResultsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetFaceLivenessSessionResultsResponse { Confidence = 30f, Status = LivenessSessionStatus.FAILED });

        var provider = Build(rek);
        var res = await provider.VerifyAsync(new VerificationRequest("s", "c", "t", "ref"), CancellationToken.None);
        res.Outcome.Should().Be(VerificationOutcome.Rejected);
        res.LivenessPassed.Should().BeFalse();
        res.ProviderCostMicroCents.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task VerifyAsync_match_acima_threshold_aprovado_devolve_Approved()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        rek.GetFaceLivenessSessionResultsAsync(Arg.Any<GetFaceLivenessSessionResultsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetFaceLivenessSessionResultsResponse
            {
                Confidence = 95f,
                Status = LivenessSessionStatus.SUCCEEDED,
                ReferenceImage = new AuditImage { Bytes = new System.IO.MemoryStream(TestImage.Jpeg()) }
            });
        rek.SearchFacesByImageAsync(Arg.Any<SearchFacesByImageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchFacesByImageResponse
            {
                FaceMatches = new List<FaceMatch>
                {
                    new() { Similarity = 95f, Face = new Face { FaceId = "expected-face" } }
                }
            });

        var provider = Build(rek);
        var res = await provider.VerifyAsync(new VerificationRequest("s", "c", "t", "expected-face"), CancellationToken.None);
        res.Outcome.Should().Be(VerificationOutcome.Approved);
        res.LivenessPassed.Should().BeTrue();
        res.Confidence.Should().BeGreaterThan(0.9);
    }

    [Fact]
    public async Task VerifyAsync_match_FaceId_diferente_devolve_Rejected()
    {
        var rek = Substitute.For<IAmazonRekognition>();
        rek.GetFaceLivenessSessionResultsAsync(Arg.Any<GetFaceLivenessSessionResultsRequest>(), Arg.Any<CancellationToken>())
            .Returns(new GetFaceLivenessSessionResultsResponse
            {
                Confidence = 95f,
                Status = LivenessSessionStatus.SUCCEEDED,
                ReferenceImage = new AuditImage { Bytes = new System.IO.MemoryStream(TestImage.Jpeg()) }
            });
        rek.SearchFacesByImageAsync(Arg.Any<SearchFacesByImageRequest>(), Arg.Any<CancellationToken>())
            .Returns(new SearchFacesByImageResponse
            {
                FaceMatches = new List<FaceMatch>
                {
                    new() { Similarity = 95f, Face = new Face { FaceId = "other-face" } }
                }
            });

        var provider = Build(rek);
        var res = await provider.VerifyAsync(new VerificationRequest("s", "c", "t", "expected-face"), CancellationToken.None);
        res.Outcome.Should().Be(VerificationOutcome.Rejected);
    }
}
