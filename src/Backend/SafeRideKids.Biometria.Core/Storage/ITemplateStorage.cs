using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Storage;

/// <summary>
/// Storage de templates biométricos (embeddings) em S3 + KMS.
/// Bucket: BIOMETRIA_S3_TEMPLATE_BUCKET. Chave: templates/{tenantId}/{childId}/{enrollmentId}.
/// </summary>
public interface ITemplateStorage
{
    /// <summary>
    /// Persiste template criptografado e devolve a chave S3.
    /// O caller já deve passar bytes cifrados (envelope encryption).
    /// O bucket tem BucketEncryption.KMS habilitado como segunda camada.
    /// </summary>
    Task<string> StoreTemplateAsync(
        string tenantId,
        Guid childId,
        Guid enrollmentId,
        byte[] encryptedTemplate,
        CancellationToken cancellationToken);

    /// <summary>Recupera template cifrado pela chave S3.</summary>
    Task<byte[]> GetTemplateAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Deleta template (DSR-delete ou expiração natural).</summary>
    Task DeleteTemplateAsync(string storageKey, CancellationToken cancellationToken);

    /// <summary>Persiste documento auxiliar (PDF de consentimento assinado) e devolve a chave S3.</summary>
    Task<string> StoreSignedDocumentAsync(
        string tenantId,
        Guid consentId,
        Stream content,
        string contentType,
        CancellationToken cancellationToken);
}
