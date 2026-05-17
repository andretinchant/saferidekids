using Amazon.CDK;
using Constructs;
using SafeRideKids.Poc.Infra.Constructs;

namespace SafeRideKids.Poc.Infra.Stacks;

// Stack principal POC. Orquestra os constructs na ordem correta de dependencia:
//
//   SecretsConstruct (CMK secrets + tenant-salt + pgp-key + unico-key)
//        |
//        v
//   DatabaseConstruct (VPC + Aurora + db-credentials cifrada pela CMK de secrets)
//   StorageConstruct  (CMK templates + S3)
//   AuthConstruct     (Cognito)
//        |
//        v
//   ApiConstruct      (Lambda + HTTP API + IAM least-privilege)
//        |
//        v
//   ObservabilityConstruct (Alarmas Lambda)
public sealed class BiometriaPocStack : Stack
{
    public BiometriaPocStack(Construct scope, string id, IStackProps props) : base(scope, id, props)
    {
        // 1) Secrets primeiro — Aurora precisa da CMK para cifrar suas credenciais.
        var secrets = new SecretsConstruct(this, "Secrets");

        // 2) Banco (VPC + Aurora).
        var database = new DatabaseConstruct(this, "Database", new DatabaseConstructProps
        {
            SecretsKmsKey = secrets.SecretsKey
        });

        // 3) Storage (CMK templates + bucket S3).
        var storage = new StorageConstruct(this, "Storage");

        // 4) Auth (Cognito).
        var auth = new AuthConstruct(this, "Auth");

        // 5) API (Lambda + HTTP API). Recebe referencias dos itens acima.
        var api = new ApiConstruct(this, "Api", new ApiConstructProps
        {
            Vpc = database.Vpc,
            DatabaseSecurityGroup = database.ClusterSecurityGroup,
            DbClusterEndpoint = database.Cluster.ClusterEndpoint.Hostname,
            DbCredentialsSecret = database.CredentialsSecret,

            TemplatesBucket = storage.TemplatesBucket,
            TemplatesKey = storage.TemplatesKey,

            SecretsKey = secrets.SecretsKey,
            TenantSaltSecret = secrets.TenantSaltSecret,
            PgpKeySecret = secrets.PgpKeySecret,
            UnicoApiKeySecret = secrets.UnicoApiKeySecret,

            CognitoUserPoolId = auth.UserPool.UserPoolId,
            CognitoMobileClientId = auth.MobileClient.UserPoolClientId
        });

        // 6) Observabilidade (alarms sobre o Lambda).
        _ = new ObservabilityConstruct(this, "Observability", new ObservabilityConstructProps
        {
            ApiFunction = api.ApiFunction
        });

        // Outputs consolidados (alguns ja vem dos constructs filhos via CfnOutput).
        _ = new CfnOutput(this, "StackRegion", new CfnOutputProps
        {
            Value = this.Region,
            Description = "Regiao AWS desta stack"
        });
    }
}
