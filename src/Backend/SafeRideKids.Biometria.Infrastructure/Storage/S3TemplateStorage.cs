using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SafeRideKids.Biometria.Core.Storage;

namespace SafeRideKids.Biometria.Infrastructure.Storage;

/// <summary>Opções do storage S3 para templates biométricos.</summary>
public sealed class S3TemplateStorageOptions
{
    /// <summary>Nome do bucket (BIOMETRIA_S3_TEMPLATE_BUCKET).</summary>
    public string BucketName { get; set; } = string.Empty;

    /// <summary>ARN da CMK (BIOMETRIA_KMS_KEY_ID). Aplicado como SSE-KMS no PUT.</summary>
    public string KmsKeyId { get; set; } = string.Empty;

    /// <summary>Bucket separado para PDFs assinados (consent). Pode ser o mesmo do template.</summary>
    public string SignedDocumentsBucketName { get; set; } = string.Empty;
}

/// <summary>
/// Implementação S3 do storage de templates. BucketEncryption.KMS deve estar
/// habilitado no bucket (camada extra de defesa). Ainda assim, enviamos
/// SSE-KMS no PUT explicitamente (defense in depth).
/// </summary>
public sealed class S3TemplateStorage : ITemplateStorage
{
    private readonly IAmazonS3 _s3;
    private readonly S3TemplateStorageOptions _options;
    private readonly ILogger<S3TemplateStorage> _logger;

    public S3TemplateStorage(
        IAmazonS3 s3,
        IOptions<S3TemplateStorageOptions> options,
        ILogger<S3TemplateStorage> logger)
    {
        _s3 = s3 ?? throw new ArgumentNullException(nameof(s3));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<string> StoreTemplateAsync(
        string tenantId, Guid childId, Guid enrollmentId, byte[] encryptedTemplate, CancellationToken cancellationToken)
    {
        var key = $"templates/{tenantId}/{childId}/{enrollmentId}";

        await using var stream = new MemoryStream(encryptedTemplate);
        var request = new PutObjectRequest
        {
            BucketName = _options.BucketName,
            Key = key,
            InputStream = stream,
            ContentType = "application/octet-stream",
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
            ServerSideEncryptionKeyManagementServiceKeyId = _options.KmsKeyId,
            BucketKeyEnabled = true
        };

        await _s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Template stored: bucket={Bucket} key={Key}", _options.BucketName, key);
        return key;
    }

    public async Task<byte[]> GetTemplateAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("storageKey vazio.", nameof(storageKey));

        var response = await _s3.GetObjectAsync(
            new GetObjectRequest { BucketName = _options.BucketName, Key = storageKey },
            cancellationToken).ConfigureAwait(false);

        using var ms = new MemoryStream();
        await response.ResponseStream.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        return ms.ToArray();
    }

    public async Task DeleteTemplateAsync(string storageKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) return;
        await _s3.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = _options.BucketName, Key = storageKey },
            cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Template deleted: bucket={Bucket} key={Key}", _options.BucketName, storageKey);
    }

    public async Task<string> StoreSignedDocumentAsync(
        string tenantId, Guid consentId, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var key = $"consents/{tenantId}/{consentId}.pdf";
        var bucket = string.IsNullOrWhiteSpace(_options.SignedDocumentsBucketName) ? _options.BucketName : _options.SignedDocumentsBucketName;

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            ServerSideEncryptionMethod = ServerSideEncryptionMethod.AWSKMS,
            ServerSideEncryptionKeyManagementServiceKeyId = _options.KmsKeyId
        };

        await _s3.PutObjectAsync(request, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Signed document stored: bucket={Bucket} key={Key}", bucket, key);
        return key;
    }
}
