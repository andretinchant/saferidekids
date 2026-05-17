using Amazon.CDK;
using Amazon.CDK.AWS.CloudWatch;
using Amazon.CDK.AWS.Lambda;
using Constructs;

namespace SafeRideKids.Poc.Infra.Constructs;

// Alarms basicos para a Lambda biometria. CONTRACTS exige minimo:
//   - errors > 1% / 5min
//   - p95 latency > 8s / 5min
//
// Decisoes:
//  - Sem SNS topic na POC; alarms ficam visiveis no console e via CloudWatch.
//    Em prod, plugar SNS -> e-mail/Slack/PagerDuty.
//  - Comparacao "1% de erros" usa MathExpression sobre Invocations e Errors.
//  - Os log groups da Lambda / API ja sao criados em ApiConstruct (retencao 30d).
public sealed class ObservabilityConstruct : Construct
{
    public Alarm LambdaErrorRateAlarm { get; }
    public Alarm LambdaP95LatencyAlarm { get; }

    public ObservabilityConstruct(Construct scope, string id, ObservabilityConstructProps props) : base(scope, id)
    {
        // Math expression: 100 * (errors / invocations). Fill NaN evita alarme falso quando ha 0 invocacoes.
        var errorRate = new MathExpression(new MathExpressionProps
        {
            Label = "ErrorRate (%)",
            Expression = "100 * (m_err / m_inv)",
            UsingMetrics = new Dictionary<string, IMetric>
            {
                ["m_err"] = props.ApiFunction.MetricErrors(new MetricOptions
                {
                    Period = Duration.Minutes(5),
                    Statistic = "sum"
                }),
                ["m_inv"] = props.ApiFunction.MetricInvocations(new MetricOptions
                {
                    Period = Duration.Minutes(5),
                    Statistic = "sum"
                })
            },
            Period = Duration.Minutes(5)
        });

        LambdaErrorRateAlarm = new Alarm(this, "LambdaErrorRateAlarm", new AlarmProps
        {
            AlarmName = "saferidekids-poc-lambda-error-rate-gt-1pct",
            AlarmDescription = "Taxa de erro da Lambda biometria > 1% em 5 minutos",
            Metric = errorRate,
            Threshold = 1.0,
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        // Latencia p95 — Lambda emite Duration; usamos pctstat 'p95'.
        LambdaP95LatencyAlarm = new Alarm(this, "LambdaP95LatencyAlarm", new AlarmProps
        {
            AlarmName = "saferidekids-poc-lambda-p95-gt-8s",
            AlarmDescription = "p95 de duracao da Lambda biometria > 8s em 5 minutos",
            Metric = props.ApiFunction.MetricDuration(new MetricOptions
            {
                Period = Duration.Minutes(5),
                Statistic = "p95"
            }),
            Threshold = 8000.0,            // ms
            EvaluationPeriods = 1,
            ComparisonOperator = ComparisonOperator.GREATER_THAN_THRESHOLD,
            TreatMissingData = TreatMissingData.NOT_BREACHING
        });

        _ = new CfnOutput(this, "LambdaErrorAlarmArn", new CfnOutputProps { Value = LambdaErrorRateAlarm.AlarmArn });
        _ = new CfnOutput(this, "LambdaP95LatencyAlarmArn", new CfnOutputProps { Value = LambdaP95LatencyAlarm.AlarmArn });
    }
}

public sealed class ObservabilityConstructProps
{
    public required Function ApiFunction { get; init; }
}
