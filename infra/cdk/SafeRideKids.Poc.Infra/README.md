# SafeRideKids.Poc.Infra

AWS CDK em C# (.NET 8) que provisiona toda a infraestrutura da POC de biometria facial (ADR-002). Use apenas em **conta sandbox**, nunca na conta de produção.

> Documento de contratos compartilhado: [`docs/poc/CONTRACTS.md`](../../../docs/poc/CONTRACTS.md). Tudo aqui segue essas decisões.

---

## 1. Visão geral

Stack `SafeRideKids-Biometria-Poc` em `sa-east-1` (data residency Brasil — requisito LGPD).

Provisiona:

| Categoria | Recurso | Detalhes |
|---|---|---|
| Rede | VPC | 2 AZs, sem NAT Gateway; subnets isoladas + VPC endpoints (Secrets, KMS, Rekognition, S3, Logs) |
| Banco | Aurora Serverless v2 | PostgreSQL 16, ACU 0-2, backup 1 dia, em subnet isolada |
| Storage | S3 + CMK | Bucket `saferidekids-poc-biometria-templates-{account}-{region}`, KMS dedicado, lifecycle 30d→Glacier IR + delete em 210d (6m + 30d safety) |
| KMS | 2 CMKs | `alias/saferidekids-poc/biometria-templates` e `alias/saferidekids-poc/secrets`; rotação anual |
| Auth | Cognito | User Pool isolado, custom attr `tenantId`, grupos `family`/`motorista`/`admin`, 2 app clients (mobile público + dashboard confidencial), Hosted UI |
| API | API Gateway HTTP | Catch-all → Lambda; throttling 100rps/200burst; access logs em CloudWatch |
| Compute | Lambda | `saferidekids-poc-biometria-api` (.NET 8 ARM64, 1024MB, 30s, em VPC) |
| Secrets | Secrets Manager | `db-credentials` (auto), `tenant-salt` (32B rand), `pgp-key` (32B rand), `unico-api-key` (placeholder vazio) |
| Logs | CloudWatch Logs | Retenção 30 dias para Lambda + access logs |
| Alarms | CloudWatch | Error rate >1%/5min e p95 Duration >8s/5min |

---

## 2. Pré-requisitos

| Ferramenta | Versão mínima | Instalação Windows / PowerShell |
|---|---|---|
| .NET SDK | 8.0 | `winget install Microsoft.DotNet.SDK.8` |
| Node.js | 20+ | `winget install OpenJS.NodeJS.LTS` (necessário para o CDK CLI) |
| AWS CDK CLI | 2.155+ | `npm install -g aws-cdk@2.155.0` |
| AWS CLI | 2.x | `winget install Amazon.AWSCLI` |
| Credenciais AWS | — | `aws configure --profile saferidekids-poc` |

Defina o perfil ativo antes dos comandos abaixo:

```powershell
$env:AWS_PROFILE = "saferidekids-poc"
$env:CDK_DEFAULT_ACCOUNT = (aws sts get-caller-identity --query Account --output text)
$env:CDK_DEFAULT_REGION  = "sa-east-1"
```

---

## 3. Bootstrap único da conta

Necessário **uma vez por conta+região**, antes do primeiro deploy:

```powershell
cdk bootstrap aws://$env:CDK_DEFAULT_ACCOUNT/$env:CDK_DEFAULT_REGION
```

---

## 4. Workflow padrão

A partir de `infra/cdk/SafeRideKids.Poc.Infra/`:

```powershell
# Restaurar pacotes (.NET)
dotnet restore

# Sintetizar (gera CloudFormation; não chama AWS)
cdk synth

# Diff contra a stack atual (se já existir)
cdk diff

# Deploy (provisiona / atualiza recursos)
cdk deploy --require-approval broadening

# Listar outputs (ARNs, endpoints, IDs)
aws cloudformation describe-stacks --stack-name SafeRideKids-Biometria-Poc --query "Stacks[0].Outputs"

# Teardown completo (apaga TUDO — inclusive segredos, KMS schedulado p/ 7 dias)
cdk destroy
```

> `cdk synth` funciona mesmo sem o asset publicado da Lambda — uma stub Node.js que retorna HTTP 503 é injetada para que a stack seja válida. Veja seção 5.

---

## 5. Publicar o asset da Lambda

O `ApiConstruct` lê o caminho do publish via context `saferidekids:lambda-asset-path` (default: `../../../src/Backend/SafeRideKids.Biometria.Api/bin/Release/net8.0/publish`). Se o diretório existir no momento do `cdk synth`, o runtime da Lambda muda para `dotnet8` ARM64; senão usa o placeholder Node.

Para gerar o asset real:

```powershell
# A partir da raiz do repositório
dotnet publish src/Backend/SafeRideKids.Biometria.Api/SafeRideKids.Biometria.Api.csproj `
    --configuration Release `
    --runtime linux-arm64 `
    --self-contained false `
    --output src/Backend/SafeRideKids.Biometria.Api/bin/Release/net8.0/publish

# Em seguida, do diretório da infra:
cdk deploy
```

Para gerar **e empacotar em ZIP** (caso prefira upload manual ou outro pipeline):

```powershell
$publish = "src/Backend/SafeRideKids.Biometria.Api/bin/Release/net8.0/publish"
Compress-Archive -Path "$publish/*" -DestinationPath "src/Backend/SafeRideKids.Biometria.Api/bin/biometria-api.zip" -Force
```

---

## 6. Preencher o segredo Unico

O segredo `saferidekids/poc/unico-api-key` é criado vazio. Após contratar credenciais Unico:

```powershell
aws secretsmanager put-secret-value `
    --secret-id "saferidekids/poc/unico-api-key" `
    --secret-string '{"apiKey":"<...>","apiSecret":"<...>","baseUrl":"https://api.unico.io"}'
```

---

## 7. Custos estimados (conta sandbox, volume POC)

| Serviço | Custo mensal estimado (USD) | Observações |
|---|---:|---|
| Aurora Serverless v2 (PostgreSQL 16, 0-2 ACU, ~5 GB) | $10-30 | Auto-pause quando ocioso; storage ~$0.10/GB |
| S3 (templates + versions, < 5 GB) | $1-3 | Inclui requests; transição p/ Glacier IR em 30d |
| KMS (2 CMKs) | $2 | $1/CMK/mês; Bucket Key reduz custo de requests para ~zero |
| Cognito User Pool | $0 | Até 50k MAU gratuitos |
| API Gateway HTTP | $1-2 | $1 por milhão de requests |
| Lambda (1024MB ARM64, baixo tráfego) | $0-3 | Free tier cobre POC |
| Secrets Manager (4 segredos) | $1.60 | $0.40/segredo/mês |
| CloudWatch Logs (~5 GB ingest, 30d retention) | $3-5 | $0.50/GB ingest + $0.03/GB stored |
| CloudWatch Alarms (2) | $0.20 | $0.10/alarm/mês |
| VPC Endpoints (5 Interface + 1 Gateway) | $30-40 | $0.01/h × 5 endpoints × 720h ≈ $36; Gateway S3 grátis |
| Rekognition (volume POC <2k chamadas) | $1-3 | $0.001/CompareFaces; Liveness $0.0025/sessão |
| **Subtotal POC** | **~$50-90/mês** | **≈ R$ 250-450/mês** |

> Maior custo fixo é a soma de **VPC Endpoints**. Se quisermos cortar, podemos remover Rekognition/Secrets/Logs endpoints e voltar a usar NAT Gateway (custo similar, ~$33), ou abrir mão da subnet isolada na POC (cluster em subnet privada com egress; teste sem VPC). Não fizemos isso para manter postura compatível com produção.

Valores baseados em pricing público AWS `sa-east-1` (maio/2026). Reaproveita estrutura do README raiz (fase Beta).

---

## 8. Validações pós-deploy

```powershell
# 1) Lambda responde (após dotnet publish + redeploy):
$api = aws cloudformation describe-stacks --stack-name SafeRideKids-Biometria-Poc `
    --query "Stacks[0].Outputs[?OutputKey=='ApiHttpApiEndpoint'].OutputValue" --output text
curl "$api/api/v1/health"

# 2) Aurora acessível só pela Lambda (esperado timeout do laptop):
$dbHost = aws cloudformation describe-stacks --stack-name SafeRideKids-Biometria-Poc `
    --query "Stacks[0].Outputs[?OutputKey=='DatabaseAuroraEndpoint'].OutputValue" --output text
Test-NetConnection $dbHost -Port 5432  # deve falhar — SG só libera Lambda

# 3) Bucket não acessível publicamente:
$bucket = aws cloudformation describe-stacks --stack-name SafeRideKids-Biometria-Poc `
    --query "Stacks[0].Outputs[?OutputKey=='StorageTemplatesBucketName'].OutputValue" --output text
curl -I "https://$bucket.s3.sa-east-1.amazonaws.com/"  # deve retornar 403
```

---

## 9. O que NÃO está na stack

- Pipeline de CI/CD (GitHub Actions, CodePipeline) — fora do escopo POC.
- Domínio custom / certificado ACM para API e Cognito.
- WAF na frente do API Gateway — opcional após beta.
- SNS topics para alarms — adicionar via `Topic` + `AlarmAction` quando definirmos canal (e-mail/Slack/PagerDuty).
- DR cross-region — não aplicável em POC.

---

## 10. Pontos de atenção LGPD

- **CMKs com `RemovalPolicy.DESTROY`**: aceitável em sandbox; em produção mudar para `RETAIN`.
- **Bucket `AutoDeleteObjects = true`**: aceitável em sandbox; em produção remover.
- **Secrets Manager `RemovalPolicy.DESTROY`**: idem.
- **Logs sem PII**: o construto não força — responsabilidade da aplicação (vide CONTRACTS Seção 1.7). Revise CloudWatch Logs ao final da Fase A para confirmar conformidade.
- **Texto de consentimento** e procedimentos de DSR estão em `docs/poc/lgpd-*.md`.
