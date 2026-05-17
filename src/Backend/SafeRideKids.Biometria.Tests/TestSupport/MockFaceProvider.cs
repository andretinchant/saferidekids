using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SafeRideKids.Biometria.Core.Providers;

namespace SafeRideKids.Biometria.Tests.TestSupport;

/// <summary>
/// Provider in-memory para testes de endpoint. Permite injetar outcomes determinísticos.
/// </summary>
public sealed class MockFaceProvider : IFaceVerificationProvider
{
    public string ProviderId { get; }

    public Func<EnrollmentRequest, EnrollmentResult> OnEnroll { get; set; } =
        req => new EnrollmentResult(true, $"mock-ref-{req.ChildId}", null, null, null);

    public Func<LivenessSessionRequest, LivenessSessionInfo> OnStartLiveness { get; set; } =
        req => new LivenessSessionInfo($"mock-session-{req.CheckInId}", new Dictionary<string, string>(), DateTimeOffset.UtcNow.AddMinutes(3));

    public Func<VerificationRequest, VerificationResult> OnVerify { get; set; } =
        req => new VerificationResult(VerificationOutcome.Approved, 0.95, true, null, 120, 1_000_000);

    public Func<string, Task> OnDelete { get; set; } = _ => Task.CompletedTask;

    public MockFaceProvider(string providerId)
    {
        ProviderId = providerId;
    }

    public Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(OnEnroll(request));

    public Task<LivenessSessionInfo> StartLivenessSessionAsync(LivenessSessionRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(OnStartLiveness(request));

    public Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(OnVerify(request));

    public Task DeleteEnrollmentAsync(string providerReferenceId, CancellationToken cancellationToken) =>
        OnDelete(providerReferenceId);
}
