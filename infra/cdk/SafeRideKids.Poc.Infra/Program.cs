using Amazon.CDK;
using SafeRideKids.Poc.Infra.Stacks;

namespace SafeRideKids.Poc.Infra;

// Ponto de entrada do app CDK.
// Toda a stack POC esta consolidada em BiometriaPocStack para facilitar
// teardown completo apos a Fase A. Em producao quebrariamos em multiplas stacks
// (Network / Data / Compute / Observability) com cross-stack refs.
public static class Program
{
    public static void Main(string[] args)
    {
        var app = new App();

        // Conta e regiao vem de variaveis de ambiente CDK_DEFAULT_ACCOUNT / CDK_DEFAULT_REGION,
        // setadas pelo `aws configure` ou pelo profile usado no CLI.
        // Default da regiao: sa-east-1 (data residency Brasil, requisito LGPD para POC).
        var env = new Amazon.CDK.Environment
        {
            Account = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_ACCOUNT"),
            Region = System.Environment.GetEnvironmentVariable("CDK_DEFAULT_REGION")
                     ?? app.Node.TryGetContext("saferidekids:region")?.ToString()
                     ?? "sa-east-1"
        };

        var stackName = app.Node.TryGetContext("saferidekids:stack-name")?.ToString()
                        ?? "SafeRideKids-Biometria-Poc";

        _ = new BiometriaPocStack(app, stackName, new StackProps
        {
            Env = env,
            Description = "SafeRide Kids - POC ADR-002 (Face Recognition). Provisiona Aurora Serverless v2, S3+KMS, Cognito, API Gateway HTTP + Lambda .NET 8, Secrets, CloudWatch.",
            Tags = new Dictionary<string, string>
            {
                ["Project"] = "SafeRideKids",
                ["Component"] = "BiometriaPoc",
                ["Environment"] = "poc",
                ["Owner"] = "platform",
                // Tag para facilitar identificacao no cost explorer da conta sandbox.
                ["CostCenter"] = "poc-adr-002",
                ["DataClassification"] = "sensitive-biometric",
                ["ManagedBy"] = "cdk"
            }
        });

        app.Synth();
    }
}
