using System.Net.Http.Headers;
using Refit;
using SafeRideKids.Familia.Maui.Models;

namespace SafeRideKids.Familia.Maui.Services;

/// <summary>
/// Interface tipada Refit para o backend. Cobre os endpoints da família
/// definidos em CONTRACTS.md Seção 6.1. Todas as chamadas (exceto login)
/// requerem Bearer token — injetado via DelegatingHandler em MauiProgram.
/// </summary>
public interface IBackendApi
{
    // ----------------------------------------------------------------------------------
    // Auth (placeholder — backend real pode ser Cognito; aqui só esqueleto)
    // ----------------------------------------------------------------------------------
    [Post("/oauth/token")]
    Task<LoginResponse> LoginAsync(
        [Body] LoginRequest request,
        CancellationToken cancellationToken);

    // ----------------------------------------------------------------------------------
    // Família — registro do responsável e crianças
    // ----------------------------------------------------------------------------------
    [Post("/api/v1/family/register")]
    Task<RegisterFamilyResponse> RegisterFamilyAsync(
        [Body] RegisterFamilyRequest request,
        CancellationToken cancellationToken);

    [Post("/api/v1/family/children")]
    Task<CreateChildResponse> CreateChildAsync(
        [Body] CreateChildRequest request,
        CancellationToken cancellationToken);

    // ----------------------------------------------------------------------------------
    // Consentimento LGPD
    // ----------------------------------------------------------------------------------
    [Post("/api/v1/family/consent")]
    Task<ConsentResponse> SubmitConsentAsync(
        [Body] ConsentRequest request,
        CancellationToken cancellationToken);

    // ----------------------------------------------------------------------------------
    // Enrollment facial — multipart com 3..5 imagens JPEG + consentId
    // O servidor orquestra em todos providers ativos (AWS + Unico) em paralelo.
    // ----------------------------------------------------------------------------------
    [Multipart]
    [Post("/api/v1/family/children/{childId}/enroll")]
    Task<EnrollmentResponse> EnrollChildAsync(
        string childId,
        [AliasAs("consentId")] string consentId,
        [AliasAs("images")] IEnumerable<StreamPart> images,
        CancellationToken cancellationToken);

    [Get("/api/v1/family/children/{childId}/enrollment")]
    Task<ChildEnrollmentStatus> GetChildEnrollmentAsync(
        string childId,
        CancellationToken cancellationToken);

    [Delete("/api/v1/family/children/{childId}/enrollment")]
    Task DeleteChildEnrollmentAsync(
        string childId,
        CancellationToken cancellationToken);
}
