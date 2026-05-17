using Amazon.CDK;
using Amazon.CDK.AWS.Cognito;
using Constructs;

namespace SafeRideKids.Poc.Infra.Constructs;

// Cognito User Pool isolado para a POC.
//
// Decisoes:
//  - User Pool dedicado da POC (nao reutiliza o pool do produto principal — isolamento total).
//  - Atributo custom 'tenantId' (mutavel false) — alinha com modelo multi-tenant SafeRide.
//  - Atributo custom 'role' — facilita admin pool sem grupos extras.
//  - Grupos: 'family', 'motorista', 'admin' (mapeados em JWT claims via grupo).
//  - 2 app clients:
//      * Mobile (publico, sem secret): SRP_AUTH, REFRESH_TOKEN_AUTH.
//      * Dashboard (confidencial, com secret): AUTHORIZATION_CODE + Hosted UI.
//  - Hosted UI domain Cognito (sem CloudFront/Route53 custom na POC).
//  - MFA: opcional (POC). Producao deve ser MANDATORY para admin.
//  - Self sign-up: desabilitado (cadastro vem via endpoint backend que cria usuario).
public sealed class AuthConstruct : Construct
{
    public UserPool UserPool { get; }
    public UserPoolClient MobileClient { get; }
    public UserPoolClient DashboardClient { get; }
    public UserPoolDomain HostedDomain { get; }

    public AuthConstruct(Construct scope, string id) : base(scope, id)
    {
        UserPool = new UserPool(this, "PocUserPool", new UserPoolProps
        {
            UserPoolName = "saferidekids-poc-users",
            SignInAliases = new SignInAliases
            {
                Email = true,
                Username = false,
                PreferredUsername = false
            },
            AutoVerify = new AutoVerifiedAttrs { Email = true },
            SelfSignUpEnabled = false,
            StandardAttributes = new StandardAttributes
            {
                Email = new StandardAttribute { Required = true, Mutable = true },
                GivenName = new StandardAttribute { Required = false, Mutable = true },
                FamilyName = new StandardAttribute { Required = false, Mutable = true }
            },
            CustomAttributes = new Dictionary<string, ICustomAttribute>
            {
                // tenantId imutavel: garante que o tenant do usuario nunca mude apos criacao.
                ["tenantId"] = new StringAttribute(new StringAttributeProps { MinLen = 1, MaxLen = 64, Mutable = false }),
                // role apenas como hint UX/cliente — autorizacao real vem dos grupos abaixo.
                ["role"] = new StringAttribute(new StringAttributeProps { MinLen = 1, MaxLen = 32, Mutable = true })
            },
            PasswordPolicy = new PasswordPolicy
            {
                MinLength = 12,
                RequireDigits = true,
                RequireLowercase = true,
                RequireUppercase = true,
                RequireSymbols = true,
                TempPasswordValidity = Duration.Days(3)
            },
            AccountRecovery = AccountRecovery.EMAIL_ONLY,
            Mfa = Mfa.OPTIONAL,
            MfaSecondFactor = new MfaSecondFactor { Otp = true, Sms = false },
            AdvancedSecurityMode = AdvancedSecurityMode.AUDIT,
            RemovalPolicy = RemovalPolicy.DESTROY,
            DeletionProtection = false
        });

        // Grupos — permissoes serao decididas na camada de aplicacao (claim 'cognito:groups').
        _ = new CfnUserPoolGroup(this, "GroupFamily", new CfnUserPoolGroupProps
        {
            UserPoolId = UserPool.UserPoolId,
            GroupName = "family",
            Description = "Responsavel legal pela crianca",
            Precedence = 30
        });
        _ = new CfnUserPoolGroup(this, "GroupMotorista", new CfnUserPoolGroupProps
        {
            UserPoolId = UserPool.UserPoolId,
            GroupName = "motorista",
            Description = "Motorista do transporte escolar",
            Precedence = 20
        });
        _ = new CfnUserPoolGroup(this, "GroupAdmin", new CfnUserPoolGroupProps
        {
            UserPoolId = UserPool.UserPoolId,
            GroupName = "admin",
            Description = "Administrador / dashboard / DPO",
            Precedence = 10
        });

        // Client publico (mobile): sem secret, SRP + refresh.
        MobileClient = UserPool.AddClient("MobileClient", new UserPoolClientOptions
        {
            UserPoolClientName = "saferidekids-poc-mobile",
            GenerateSecret = false,
            AuthFlows = new AuthFlow
            {
                UserSrp = true,
                UserPassword = false,
                AdminUserPassword = false,
                Custom = false
            },
            PreventUserExistenceErrors = true,
            EnableTokenRevocation = true,
            AccessTokenValidity = Duration.Minutes(60),
            IdTokenValidity = Duration.Minutes(60),
            RefreshTokenValidity = Duration.Days(30),
            ReadAttributes = new ClientAttributes()
                .WithStandardAttributes(new StandardAttributesMask { Email = true, GivenName = true, FamilyName = true })
                .WithCustomAttributes("tenantId", "role"),
            WriteAttributes = new ClientAttributes()
                .WithStandardAttributes(new StandardAttributesMask { GivenName = true, FamilyName = true })
        });

        // Client confidencial (dashboard Blazor): com secret, AUTHORIZATION_CODE + Hosted UI.
        DashboardClient = UserPool.AddClient("DashboardClient", new UserPoolClientOptions
        {
            UserPoolClientName = "saferidekids-poc-dashboard",
            GenerateSecret = true,
            AuthFlows = new AuthFlow
            {
                UserSrp = true
            },
            OAuth = new OAuthSettings
            {
                Flows = new OAuthFlows { AuthorizationCodeGrant = true },
                Scopes = new[] { OAuthScope.OPENID, OAuthScope.EMAIL, OAuthScope.PROFILE },
                CallbackUrls = new[] { "https://localhost:5001/signin-oidc" },
                LogoutUrls = new[] { "https://localhost:5001/signout-callback-oidc" }
            },
            PreventUserExistenceErrors = true,
            EnableTokenRevocation = true,
            AccessTokenValidity = Duration.Minutes(60),
            IdTokenValidity = Duration.Minutes(60),
            RefreshTokenValidity = Duration.Hours(8)
        });

        // Hosted UI domain Cognito (gratuito ate dominio custom). Prefixo precisa ser globalmente unico.
        HostedDomain = UserPool.AddDomain("HostedDomain", new UserPoolDomainOptions
        {
            CognitoDomain = new CognitoDomainOptions
            {
                DomainPrefix = $"saferidekids-poc-{Aws.ACCOUNT_ID}"
            }
        });

        _ = new CfnOutput(this, "UserPoolId", new CfnOutputProps
        {
            Value = UserPool.UserPoolId,
            Description = "Cognito User Pool POC"
        });
        _ = new CfnOutput(this, "MobileClientId", new CfnOutputProps
        {
            Value = MobileClient.UserPoolClientId,
            Description = "App client mobile (sem secret)"
        });
        _ = new CfnOutput(this, "DashboardClientId", new CfnOutputProps
        {
            Value = DashboardClient.UserPoolClientId,
            Description = "App client dashboard (com secret)"
        });
        _ = new CfnOutput(this, "HostedUiBaseUrl", new CfnOutputProps
        {
            Value = HostedDomain.BaseUrl(),
            Description = "URL do Hosted UI Cognito"
        });
    }
}
