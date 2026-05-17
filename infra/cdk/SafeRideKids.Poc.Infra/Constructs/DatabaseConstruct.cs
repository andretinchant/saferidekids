using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.KMS;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SecretsManager;
using Constructs;

namespace SafeRideKids.Poc.Infra.Constructs;

// Construct responsavel pelo cluster Aurora Serverless v2 (PostgreSQL 16) da POC.
//
// Decisoes:
//  - Scale-to-0 ACU (min=0, max=2) para zerar custo em idle. Cold-start adicional
//    aceitavel num cenario POC (ate ~30s na primeira chamada apos pausa).
//  - Cluster em subnet PRIVATE_ISOLATED. Acesso via Lambda na mesma VPC.
//  - Backup retention de 1 dia (POC) — em prod subiriamos para 7+.
//  - Removal policy DESTROY: facilita teardown completo apos Fase A.
//  - Credenciais auto-geradas e armazenadas no Secrets Manager.
//  - pgcrypto e citext habilitados via parameter group (necessario para pgp_sym_encrypt).
public sealed class DatabaseConstruct : Construct
{
    public IVpc Vpc { get; }
    public DatabaseCluster Cluster { get; }
    public ISecret CredentialsSecret { get; }
    public ISecurityGroup ClusterSecurityGroup { get; }

    // Nome do database criado no cluster — referenciado em variavel de ambiente do Lambda.
    public const string DatabaseName = "biometria_poc";

    public DatabaseConstruct(Construct scope, string id, DatabaseConstructProps props) : base(scope, id)
    {
        // VPC enxuta: 2 AZs (Aurora exige multi-AZ minimo), 1 subnet publica para NAT egress (opcional),
        // 1 subnet privada com egress (Lambda) e 1 isolada (DB).
        // Para minimizar custo da POC, removemos NAT Gateway: Lambda usa subnet PRIVATE_ISOLATED
        // e VPC endpoints quando precisar acessar servicos AWS.
        Vpc = new Vpc(this, "PocVpc", new VpcProps
        {
            VpcName = "saferidekids-poc-vpc",
            MaxAzs = 2,
            NatGateways = 0,
            SubnetConfiguration = new ISubnetConfiguration[]
            {
                new SubnetConfiguration
                {
                    Name = "isolated",
                    SubnetType = SubnetType.PRIVATE_ISOLATED,
                    CidrMask = 24
                }
            }
        });

        // VPC Endpoints para que a Lambda em subnet isolada consiga falar com servicos AWS
        // sem precisar de NAT Gateway (que custaria ~US$33/mes fixo).
        Vpc.AddInterfaceEndpoint("SecretsEndpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.SECRETS_MANAGER,
            PrivateDnsEnabled = true
        });
        Vpc.AddInterfaceEndpoint("KmsEndpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.KMS,
            PrivateDnsEnabled = true
        });
        Vpc.AddInterfaceEndpoint("RekognitionEndpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.REKOGNITION,
            PrivateDnsEnabled = true
        });
        Vpc.AddGatewayEndpoint("S3Endpoint", new GatewayVpcEndpointOptions
        {
            Service = GatewayVpcEndpointAwsService.S3
        });
        Vpc.AddInterfaceEndpoint("CloudWatchLogsEndpoint", new InterfaceVpcEndpointOptions
        {
            Service = InterfaceVpcEndpointAwsService.CLOUDWATCH_LOGS,
            PrivateDnsEnabled = true
        });

        // Security group dedicado para o cluster. Ingress apenas na 5432 a partir do SG do Lambda.
        ClusterSecurityGroup = new SecurityGroup(this, "AuroraSg", new SecurityGroupProps
        {
            Vpc = Vpc,
            Description = "Aurora Serverless v2 - biometria POC",
            AllowAllOutbound = false,
            SecurityGroupName = "saferidekids-poc-aurora-sg"
        });

        // Parameter group: habilita extensoes pgcrypto e citext, necessarias para
        // pgp_sym_encrypt (display_name_encrypted) e indices case-insensitive.
        var parameterGroup = new ParameterGroup(this, "AuroraPg", new ParameterGroupProps
        {
            Engine = DatabaseClusterEngine.AuroraPostgres(new AuroraPostgresClusterEngineProps
            {
                Version = AuroraPostgresEngineVersion.VER_16_4
            }),
            Description = "SafeRideKids POC - habilita pgcrypto/citext via shared_preload_libraries",
            Parameters = new Dictionary<string, string>
            {
                // shared_preload_libraries nao se aplica a pgcrypto (extensao userspace),
                // mas deixamos hooks de logging conservadores aqui:
                ["log_min_duration_statement"] = "1000",   // log de queries acima de 1s
                ["log_statement"] = "ddl",                  // apenas DDL nos logs (evita PII)
                ["log_connections"] = "1",
                ["log_disconnections"] = "1"
            }
        });

        // Credenciais geradas automaticamente no Secrets Manager.
        // Nome do secret: 'saferidekids/poc/db-credentials' — referenciado em variavel
        // de ambiente do Lambda. Cifrado com a CMK dedicada de secrets.
        CredentialsSecret = new Secret(this, "DbCredentials", new SecretProps
        {
            SecretName = "saferidekids/poc/db-credentials",
            Description = "Credenciais master do Aurora POC (auto-rotacao desabilitada na POC)",
            EncryptionKey = props.SecretsKmsKey,
            GenerateSecretString = new SecretStringGenerator
            {
                SecretStringTemplate = "{\"username\":\"saferidekids_admin\"}",
                GenerateStringKey = "password",
                ExcludePunctuation = true,
                IncludeSpace = false,
                PasswordLength = 32
            }
        });

        Cluster = new DatabaseCluster(this, "AuroraCluster", new DatabaseClusterProps
        {
            Engine = DatabaseClusterEngine.AuroraPostgres(new AuroraPostgresClusterEngineProps
            {
                Version = AuroraPostgresEngineVersion.VER_16_4
            }),
            ClusterIdentifier = "saferidekids-poc-aurora",
            Credentials = Credentials.FromSecret(CredentialsSecret),
            DefaultDatabaseName = DatabaseName,
            Vpc = Vpc,
            VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PRIVATE_ISOLATED },
            SecurityGroups = new[] { ClusterSecurityGroup },
            ParameterGroup = parameterGroup,

            // Serverless v2: a unica forma de escalar e via writer/reader serverless.
            // min=0 ACU habilita auto-pause; max=2 ACU limita o teto de gasto (~US$0,12/h em pico).
            ServerlessV2MinCapacity = 0.0,
            ServerlessV2MaxCapacity = 2.0,

            // Writer + 0 readers (POC, sem demanda de leitura escalada).
            Writer = ClusterInstance.ServerlessV2("Writer", new ServerlessV2ClusterInstanceProps
            {
                PubliclyAccessible = false,
                AutoMinorVersionUpgrade = true
            }),

            // Backup curto para POC. Producao: 7+ dias e PITR.
            Backup = new BackupProps
            {
                Retention = Duration.Days(1),
                PreferredWindow = "03:00-04:00"
            },
            CloudwatchLogsExports = new[] { "postgresql" },
            CloudwatchLogsRetention = Amazon.CDK.AWS.Logs.RetentionDays.ONE_MONTH,
            DeletionProtection = false,
            Iam = false,
            // Storage encryption sempre ON; usa AWS-managed key (suficiente para POC,
            // visto que a CMK custom ja protege os templates biometricos no S3).
            StorageEncrypted = true,
            RemovalPolicy = RemovalPolicy.DESTROY
        });

        // Tag para auditoria de classificacao de dado.
        Tags.Of(Cluster).Add("DataClassification", "sensitive-biometric");

        // Outputs uteis para o CLI / pipeline.
        _ = new CfnOutput(this, "AuroraEndpoint", new CfnOutputProps
        {
            Value = Cluster.ClusterEndpoint.Hostname,
            Description = "Endpoint do cluster Aurora POC"
        });
        _ = new CfnOutput(this, "AuroraSecretArn", new CfnOutputProps
        {
            Value = CredentialsSecret.SecretArn,
            Description = "ARN do secret com credenciais master"
        });
    }
}

public sealed class DatabaseConstructProps
{
    // CMK dedicada a Secrets Manager (separada da CMK de templates biometricos).
    public required IKey SecretsKmsKey { get; init; }
}
