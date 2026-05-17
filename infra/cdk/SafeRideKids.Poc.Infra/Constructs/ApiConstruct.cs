using Amazon.CDK;
using Amazon.CDK.AWS.APIGatewayv2;
using Amazon.CDK.AWS.APIGatewayv2.Integrations;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.S3;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;
using System.IO;

namespace SafeRideKids.Poc.Infra.Constructs;

// Lambda principal (BiometriaApiFunction) + API Gateway HTTP API + permissionamento IAM minimo.
//
// Decisoes:
//  - HTTP API (apigatewayv2) ao inves de REST API: ~70% mais barato, latencia menor,
//    suficiente para a POC (nao precisamos de usage plans, request validators avancados etc.).
//  - Throttling 100 rps / 200 burst no stage default.
//  - Lambda em VPC privada (para acessar Aurora). Memory 1024MB, timeout 30s.
//  - Code asset: se o diretorio de publish existir, usamos AssetCode; senao geramos
//    placeholder inline para que `cdk synth` nao quebre antes do `dotnet publish`.
//    Comando para gerar o asset esta no README.
//  - Log group dedicado com retencao 30d.
//  - IAM least-privilege:
//      * Rekognition: CreateFaceLivenessSession / GetFaceLivenessSessionResults / CompareFaces / DetectFaces
//        (sem IndexFaces / SearchFaces — proibimos 1:N por contrato).
//      * S3: apenas o bucket de templates.
//      * KMS: encrypt/decrypt nas duas CMKs (templates + secrets).
//      * Secrets: apenas os ARNs especificos.
//      * CloudWatch Logs (auto via execution role basico).
public sealed class ApiConstruct : Construct
{
    public Function ApiFunction { get; }
    public HttpApi HttpApi { get; }
    public ILogGroup ApiLogGroup { get; }
    public ILogGroup AccessLogGroup { get; }
    public ISecurityGroup LambdaSecurityGroup { get; }

    public ApiConstruct(Construct scope, string id, ApiConstructProps props) : base(scope, id)
    {
        // SG da Lambda — permitido sair para qualquer destino dentro da VPC e para os
        // VPC endpoints (Secrets, KMS, Rekognition, S3, Logs).
        LambdaSecurityGroup = new SecurityGroup(this, "LambdaSg", new SecurityGroupProps
        {
            Vpc = props.Vpc,
            Description = "Lambda biometria POC",
            AllowAllOutbound = true,
            SecurityGroupName = "saferidekids-poc-lambda-sg"
        });

        // Libera ingress 5432 do Aurora SG vindo deste SG.
        props.DatabaseSecurityGroup.AddIngressRule(
            LambdaSecurityGroup,
            Port.Tcp(5432),
            "Lambda -> Aurora POC");

        // Log group explicito (em vez de deixar Lambda criar implicito) — retencao 30d e KMS aplicavel.
        ApiLogGroup = new LogGroup(this, "ApiLogs", new LogGroupProps
        {
            LogGroupName = "/aws/lambda/saferidekids-poc-biometria-api",
            Retention = RetentionDays.ONE_MONTH,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        AccessLogGroup = new LogGroup(this, "ApiAccessLogs", new LogGroupProps
        {
            LogGroupName = "/aws/apigw/saferidekids-poc-biometria-api",
            Retention = RetentionDays.ONE_MONTH,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Execution role explicito — facilita auditar policies inline.
        var executionRole = new Role(this, "ApiFunctionRole", new RoleProps
        {
            AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            Description = "Execution role do Lambda BiometriaApi - least privilege",
            ManagedPolicies = new IManagedPolicy[]
            {
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaBasicExecutionRole"),
                ManagedPolicy.FromAwsManagedPolicyName("service-role/AWSLambdaVPCAccessExecutionRole")
            }
        });

        // Codigo da Lambda.
        // Caminho de publish vem do context cdk.json -> 'saferidekids:lambda-asset-path'.
        // Se o diretorio nao existir (cenario typical de primeira sintese antes do dotnet publish),
        // usamos um placeholder inline para nao quebrar o synth. README documenta como gerar o asset.
        var assetPath = this.Node.TryGetContext("saferidekids:lambda-asset-path")?.ToString()
                        ?? "../../../src/Backend/SafeRideKids.Biometria.Api/bin/Release/net8.0/publish";

        var resolvedAssetPath = Path.IsPathRooted(assetPath)
            ? assetPath
            : Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        Code lambdaCode;
        if (Directory.Exists(resolvedAssetPath))
        {
            lambdaCode = Code.FromAsset(resolvedAssetPath);
        }
        else
        {
            // Placeholder valido — handler retorna 503 ate o backend ser publicado.
            // Apenas para nao bloquear `cdk synth`/`cdk deploy --no-execute`.
            lambdaCode = Code.FromInline(@"
exports.handler = async () => ({
  statusCode: 503,
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    error: 'lambda-asset-not-published',
    message: 'Rode `dotnet publish` no projeto SafeRideKids.Biometria.Api e re-execute cdk deploy.'
  })
});");
        }

        // Variaveis de ambiente alinhadas com CONTRACTS Secao 13.
        var environment = new Dictionary<string, string>
        {
            ["AWS_REGION_HINT"] = Stack.Of(this).Region,
            ["BIOMETRIA_PROVIDERS_ATIVOS"] = "aws-rekognition,unico-idcloud",
            ["BIOMETRIA_DEFAULT_PROVIDER"] = "aws-rekognition",
            ["BIOMETRIA_S3_TEMPLATE_BUCKET"] = props.TemplatesBucket.BucketName,
            ["BIOMETRIA_KMS_KEY_ID"] = props.TemplatesKey.KeyArn,
            ["BIOMETRIA_PG_CONNSTRING_SECRET"] = props.DbCredentialsSecret.SecretArn,
            ["BIOMETRIA_PG_DATABASE"] = DatabaseConstruct.DatabaseName,
            ["BIOMETRIA_PG_HOST"] = props.DbClusterEndpoint,
            ["BIOMETRIA_PG_ENCRYPTION_KEY_SECRET"] = props.PgpKeySecret.SecretArn,
            ["UNICO_API_BASE_URL"] = "https://api.unico.io",
            ["UNICO_API_KEY_SECRET"] = props.UnicoApiKeySecret.SecretArn,
            ["COGNITO_USER_POOL_ID"] = props.CognitoUserPoolId,
            ["COGNITO_CLIENT_ID"] = props.CognitoMobileClientId,
            ["LOG_LEVEL"] = "Information",
            ["TENANT_SALT_SECRET"] = props.TenantSaltSecret.SecretArn,
            // ASP.NET Lambda hosting bootstrap.
            ["ASPNETCORE_ENVIRONMENT"] = "Poc"
        };

        // O runtime sera trocado pelo `dotnet` quando o asset real for publicado.
        // Como placeholder usa Node, mantemos um runtime "neutro" e o handler textual abaixo:
        var runtime = Directory.Exists(resolvedAssetPath) ? Runtime.DOTNET_8 : Runtime.NODEJS_20_X;
        var handler = Directory.Exists(resolvedAssetPath)
            ? "SafeRideKids.Biometria.Api::SafeRideKids.Biometria.Api.LambdaEntryPoint::FunctionHandlerAsync"
            : "index.handler";

        ApiFunction = new Function(this, "BiometriaApiFunction", new FunctionProps
        {
            FunctionName = "saferidekids-poc-biometria-api",
            Description = "API biometria POC (Rekognition + Unico). Hospedagem ASP.NET minimal via AspNetCoreServer.Hosting.",
            Runtime = runtime,
            Handler = handler,
            Code = lambdaCode,
            MemorySize = 1024,
            Timeout = Duration.Seconds(30),
            Architecture = Architecture.ARM_64,
            Role = executionRole,
            Environment = environment,
            LogGroup = ApiLogGroup,
            Vpc = props.Vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = new[] { LambdaSecurityGroup },
            Tracing = Tracing.ACTIVE
        });

        // --- IAM least-privilege para o Lambda ---

        // Rekognition: explicitamente PROIBIMOS IndexFaces / SearchFaces (operacoes 1:N).
        // Permitimos apenas as APIs de liveness e comparacao 1:1.
        ApiFunction.AddToRolePolicy(new PolicyStatement(new PolicyStatementProps
        {
            Sid = "RekognitionFaceVerification",
            Effect = Effect.ALLOW,
            Actions = new[]
            {
                "rekognition:CreateFaceLivenessSession",
                "rekognition:StartFaceLivenessSession",
                "rekognition:GetFaceLivenessSessionResults",
                "rekognition:CompareFaces",
                "rekognition:DetectFaces"
            },
            Resources = new[] { "*" }   // APIs Rekognition de sessao nao suportam ARN granular
        }));

        // S3: somente o bucket de templates.
        props.TemplatesBucket.GrantReadWrite(ApiFunction);
        props.TemplatesBucket.GrantDelete(ApiFunction);

        // KMS: encrypt/decrypt nas duas CMKs.
        props.TemplatesKey.GrantEncryptDecrypt(ApiFunction);
        props.SecretsKey.GrantEncryptDecrypt(ApiFunction);

        // Secrets: apenas os ARNs relevantes.
        props.DbCredentialsSecret.GrantRead(ApiFunction);
        props.TenantSaltSecret.GrantRead(ApiFunction);
        props.PgpKeySecret.GrantRead(ApiFunction);
        props.UnicoApiKeySecret.GrantRead(ApiFunction);

        // --- API Gateway HTTP API ---

        var integration = new HttpLambdaIntegration("LambdaIntegration", ApiFunction);

        HttpApi = new HttpApi(this, "BiometriaHttpApi", new HttpApiProps
        {
            ApiName = "saferidekids-poc-biometria-api",
            Description = "HTTP API para a POC de biometria (Familia, Motorista, Dashboard)",
            DefaultIntegration = integration,
            CorsPreflight = new CorsPreflightOptions
            {
                AllowMethods = new[] { CorsHttpMethod.ANY },
                AllowHeaders = new[] { "Authorization", "Content-Type" },
                AllowOrigins = new[] { "https://localhost:5001" },  // dashboard local — ajustar em PR
                MaxAge = Duration.Hours(1)
            },
            // Criamos o stage manualmente para conseguir setar throttling e access logs.
            CreateDefaultStage = false
        });

        // Catch-all idiomatico: HttpApi.AddRoutes cria CfnRoute + reusa a integration.
        // ASP.NET (via Amazon.Lambda.AspNetCoreServer.Hosting) faz o roteamento interno.
        HttpApi.AddRoutes(new AddRoutesOptions
        {
            Path = "/{proxy+}",
            Methods = new[] { HttpMethod.ANY },
            Integration = integration
        });

        // Stage $default explicito — atalho para conseguir setar throttling e access logs
        // sem precisar manipular o CfnStage gerado implicitamente.
        var defaultStage = new HttpStage(this, "DefaultStage", new HttpStageProps
        {
            HttpApi = HttpApi,
            StageName = "$default",
            AutoDeploy = true,
            Throttle = new ThrottleSettings
            {
                RateLimit = 100,
                BurstLimit = 200
            }
        });
        // Access logs precisam do CfnStage subjacente porque o L2 nao expoe a propriedade.
        var cfnStage = (CfnStage)defaultStage.Node.DefaultChild!;
        cfnStage.AccessLogSettings = new CfnStage.AccessLogSettingsProperty
        {
            DestinationArn = AccessLogGroup.LogGroupArn,
            Format = "{\"requestId\":\"$context.requestId\",\"sourceIp\":\"$context.identity.sourceIp\",\"method\":\"$context.httpMethod\",\"path\":\"$context.path\",\"status\":\"$context.status\",\"latencyMs\":\"$context.responseLatency\",\"userAgent\":\"$context.identity.userAgent\"}"
        };

        _ = new CfnOutput(this, "HttpApiEndpoint", new CfnOutputProps
        {
            Value = HttpApi.ApiEndpoint,
            Description = "Endpoint base da HTTP API biometria POC"
        });
        _ = new CfnOutput(this, "BiometriaApiFunctionArn", new CfnOutputProps
        {
            Value = ApiFunction.FunctionArn,
            Description = "ARN do Lambda principal"
        });
    }
}

public sealed class ApiConstructProps
{
    public required IVpc Vpc { get; init; }
    public required ISecurityGroup DatabaseSecurityGroup { get; init; }
    public required string DbClusterEndpoint { get; init; }
    public required ISecret DbCredentialsSecret { get; init; }

    public required Bucket TemplatesBucket { get; init; }
    public required IKey TemplatesKey { get; init; }

    public required IKey SecretsKey { get; init; }
    public required ISecret TenantSaltSecret { get; init; }
    public required ISecret PgpKeySecret { get; init; }
    public required ISecret UnicoApiKeySecret { get; init; }

    public required string CognitoUserPoolId { get; init; }
    public required string CognitoMobileClientId { get; init; }
}
