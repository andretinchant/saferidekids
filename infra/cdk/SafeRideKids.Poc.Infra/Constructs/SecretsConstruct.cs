using Amazon.CDK;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;

namespace SafeRideKids.Poc.Infra.Constructs;

// Segredos da POC (excetuando-se as credenciais do Aurora, que sao criadas
// dentro do DatabaseConstruct para vincular ao cluster).
//
// Decisoes:
//  - CMK dedicada (alias 'alias/saferidekids-poc/secrets') para todos os segredos.
//    Separar do KMS de templates evita "rotear" permissoes de forma cruzada.
//  - tenant-salt e pgp-key sao gerados pelo Secrets Manager (CSPRNG do servico).
//  - unico-api-key fica como placeholder vazio — operador preenche depois do onboarding
//    junto a Unico (provisao manual de credencial, fora do escopo do IaC).
public sealed class SecretsConstruct : Construct
{
    public IKey SecretsKey { get; }

    public ISecret TenantSaltSecret { get; }
    public ISecret PgpKeySecret { get; }
    public ISecret UnicoApiKeySecret { get; }

    public SecretsConstruct(Construct scope, string id) : base(scope, id)
    {
        // CMK exclusiva para Secrets Manager — rotacao anual.
        SecretsKey = new Key(this, "SecretsKey", new KeyProps
        {
            Alias = "alias/saferidekids-poc/secrets",
            Description = "CMK para Secrets Manager da POC biometria",
            EnableKeyRotation = true,
            PendingWindow = Duration.Days(7),
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // tenant-salt: 32 bytes random, usado em HMAC-SHA256 para hashar PII em logs/metricas
        // (vide CONTRACTS Secao 1.7 e Secao 8).
        TenantSaltSecret = new Secret(this, "TenantSaltSecret", new SecretProps
        {
            SecretName = "saferidekids/poc/tenant-salt",
            Description = "Salt HMAC por tenant — 32 bytes random (base64). Rotacao manual + recadastro de hashes.",
            EncryptionKey = SecretsKey,
            GenerateSecretString = new SecretStringGenerator
            {
                SecretStringTemplate = "{\"version\":\"v1\"}",
                GenerateStringKey = "salt",
                PasswordLength = 44,            // ~32 bytes em base64
                ExcludePunctuation = false,
                IncludeSpace = false
            }
        });

        // pgp-key: chave simetrica usada por pgp_sym_encrypt no Aurora para cifrar display_name etc.
        // 32 bytes (256 bits). Aurora le esse secret on-demand via psql funcao ou via app layer.
        PgpKeySecret = new Secret(this, "PgpKeySecret", new SecretProps
        {
            SecretName = "saferidekids/poc/pgp-key",
            Description = "Chave simetrica pgp_sym_encrypt (32 bytes). Recriacao requer re-encryption das colunas.",
            EncryptionKey = SecretsKey,
            GenerateSecretString = new SecretStringGenerator
            {
                SecretStringTemplate = "{\"version\":\"v1\"}",
                GenerateStringKey = "key",
                PasswordLength = 44,            // 32 bytes em base64
                ExcludePunctuation = false,
                IncludeSpace = false
            }
        });

        // unico-api-key: placeholder — operador preenche apos contratar credenciais Unico.
        UnicoApiKeySecret = new Secret(this, "UnicoApiKeySecret", new SecretProps
        {
            SecretName = "saferidekids/poc/unico-api-key",
            Description = "Credenciais Unico IDCloud (placeholder — preencher manualmente via console/AWS CLI).",
            EncryptionKey = SecretsKey,
            // String vazia explicita ao inves de geracao automatica (nao queremos um valor random como chave de API).
            SecretStringValue = SecretValue.UnsafePlainText("{\"apiKey\":\"\",\"apiSecret\":\"\",\"baseUrl\":\"https://api.unico.io\"}")
        });

        _ = new CfnOutput(this, "TenantSaltSecretArn", new CfnOutputProps { Value = TenantSaltSecret.SecretArn });
        _ = new CfnOutput(this, "PgpKeySecretArn", new CfnOutputProps { Value = PgpKeySecret.SecretArn });
        _ = new CfnOutput(this, "UnicoApiKeySecretArn", new CfnOutputProps { Value = UnicoApiKeySecret.SecretArn });
    }
}
