using Amazon.CDK;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.S3;
using Constructs;

namespace SafeRideKids.Poc.Infra.Constructs;

// Bucket S3 + CMK dedicada para templates/embeddings biometricos.
//
// Decisoes (alinhadas com CONTRACTS Secao 8):
//  - Block public access total: nao deve haver QUALQUER caminho de acesso publico.
//  - Versionamento ON: permite recuperacao de delete acidental + auditoria de alteracao
//    em DSR-delete (verificamos que o objeto foi marcado para delete).
//  - Encryption SSE-KMS com CMK dedicada (alias 'saferidekids-poc/biometria-templates').
//  - Lifecycle:
//      * transicao para Glacier Instant Retrieval apos 30 dias (template pouco usado em verify)
//      * expiracao definitiva 6 meses + 30 dias de safety (retencao maxima do enrollment)
//      * limpeza de versoes nao-correntes em 7 dias
//  - Object lock OFF: nao e audit log; precisamos honrar DSR-delete imediatamente.
//  - SSL-only via bucket policy.
//  - removalPolicy DESTROY + autoDeleteObjects: teardown limpo apos Fase A.
public sealed class StorageConstruct : Construct
{
    public IKey TemplatesKey { get; }
    public Bucket TemplatesBucket { get; }

    public StorageConstruct(Construct scope, string id) : base(scope, id)
    {
        // CMK dedicada com rotacao anual ativa (LGPD Art. 46 / boas praticas NIST).
        TemplatesKey = new Key(this, "TemplatesKey", new KeyProps
        {
            Alias = "alias/saferidekids-poc/biometria-templates",
            Description = "CMK para criptografar templates biometricos no S3 e tambem campos pgcrypto no Aurora",
            EnableKeyRotation = true,
            PendingWindow = Duration.Days(7),
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Nome do bucket inclui account/region para evitar colisao no namespace global do S3.
        // Tokens do CDK sao resolvidos no synth.
        var bucketName = $"saferidekids-poc-biometria-templates-{Aws.ACCOUNT_ID}-{Aws.REGION}";

        TemplatesBucket = new Bucket(this, "TemplatesBucket", new BucketProps
        {
            BucketName = bucketName,
            BlockPublicAccess = BlockPublicAccess.BLOCK_ALL,
            Encryption = BucketEncryption.KMS,
            EncryptionKey = TemplatesKey,
            BucketKeyEnabled = true,           // reduz custo de KMS em ~99% (recomendacao AWS).
            EnforceSSL = true,                  // gera bucket policy negando aws:SecureTransport=false.
            Versioned = true,
            ObjectOwnership = ObjectOwnership.BUCKET_OWNER_ENFORCED,
            PublicReadAccess = false,
            RemovalPolicy = RemovalPolicy.DESTROY,
            AutoDeleteObjects = true,           // POC; em prod jamais ligar.
            LifecycleRules = new ILifecycleRule[]
            {
                new LifecycleRule
                {
                    Id = "templates-transition-and-expire",
                    Enabled = true,
                    Prefix = "templates/",
                    Transitions = new ITransition[]
                    {
                        new Transition
                        {
                            StorageClass = StorageClass.GLACIER_INSTANT_RETRIEVAL,
                            TransitionAfter = Duration.Days(30)
                        }
                    },
                    Expiration = Duration.Days(210),    // 6 meses (180d) + 30d de safety
                    NoncurrentVersionExpiration = Duration.Days(7)
                },
                new LifecycleRule
                {
                    Id = "abort-incomplete-multipart",
                    Enabled = true,
                    AbortIncompleteMultipartUploadAfter = Duration.Days(1)
                }
            }
        });

        // Reforco: nega QUALQUER acao fora do TLS, mesmo intra-conta.
        TemplatesBucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "DenyInsecureTransport",
            Effect = Effect.DENY,
            Principals = new[] { new AnyPrincipal() },
            Actions = new[] { "s3:*" },
            Resources = new[]
            {
                TemplatesBucket.BucketArn,
                $"{TemplatesBucket.BucketArn}/*"
            },
            Conditions = new Dictionary<string, object>
            {
                ["Bool"] = new Dictionary<string, object>
                {
                    ["aws:SecureTransport"] = "false"
                }
            }
        }));

        // Reforco: nega upload sem KMS (defesa contra mis-config futuro).
        TemplatesBucket.AddToResourcePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "DenyNonKmsUploads",
            Effect = Effect.DENY,
            Principals = new[] { new AnyPrincipal() },
            Actions = new[] { "s3:PutObject" },
            Resources = new[] { $"{TemplatesBucket.BucketArn}/*" },
            Conditions = new Dictionary<string, object>
            {
                ["StringNotEquals"] = new Dictionary<string, object>
                {
                    ["s3:x-amz-server-side-encryption"] = "aws:kms"
                }
            }
        }));

        _ = new CfnOutput(this, "TemplatesBucketName", new CfnOutputProps
        {
            Value = TemplatesBucket.BucketName,
            Description = "Bucket S3 de templates biometricos (KMS, lifecycle 6m)"
        });
        _ = new CfnOutput(this, "TemplatesKmsKeyArn", new CfnOutputProps
        {
            Value = TemplatesKey.KeyArn,
            Description = "CMK dedicada a templates biometricos"
        });
    }
}
