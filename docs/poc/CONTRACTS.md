# POC Biometria Facial — Contratos Compartilhados

> **Documento de coordenação para agentes paralelos.** Toda decisão de arquitetura, naming, modelo de dados, interface, endpoints e baseline de LGPD está aqui. Agentes devem seguir este documento literalmente. Quando algo não estiver coberto, escolher a opção mais conservadora (LGPD, segurança, simplicidade) e documentar a decisão em comentário no código.

- **Projeto:** SafeRide Kids — POC para ADR-002 (Face Recognition Provider)
- **Stack:** .NET 8 LTS · .NET MAUI · AWS Lambda · Aurora Serverless v2 (PostgreSQL 16) · S3 + KMS · Cognito · Blazor Server · AWS CDK em C#
- **Provedores funcionais:** AWS Rekognition Face Liveness + CompareFaces · Unico IDCloud
- **Provedores documentais (paper-only):** Azure AI Face · FaceTec ZoOm · Serpro Datavalid · Google Cloud Vision (rejeitado, explicar por quê)
- **Plataforma de desenvolvimento:** Windows 11 + PowerShell 7

---

## 1. Princípios não-negociáveis

1. **Verificação 1:1, nunca 1:N.** O check-in confirma se a criança apresentada é a esperada para a parada — não identifica em base de pessoas.
2. **Biometria é dado pessoal sensível (LGPD Art. 11).** Criança = proteção reforçada + consentimento expresso, destacado, do responsável legal + melhor interesse da criança.
3. **Biometria não é único fator.** Sempre existe fallback (PIN do responsável, lista autorizada, confirmação manual auditada).
4. **Fotos raw nunca persistem.** S3 só armazena template/embedding criptografado. Se um provedor exigir imagem temporária, TTL ≤ 24h + KMS + bucket isolado.
5. **POC só com adultos voluntários.** Crianças apenas em Fase B, fora deste escopo, condicionada a RIPD/DPIA aprovado + consentimento.
6. **Imagens nunca no dispositivo persistente.** Liveness e captura usam buffer em memória; o SDK pode reter por sessão, mas a app nunca grava no disco.
7. **PII em logs só hashada.** Use HMAC-SHA256 com salt por tenant.

---

## 2. Layout do repositório

Tudo a partir do raiz do worktree.

```
src/
  Backend/
    SafeRideKids.Biometria.Api/                  # Lambda handlers (entry points)
    SafeRideKids.Biometria.Core/                 # Domínio: interface, models, value objects
    SafeRideKids.Biometria.Infrastructure/       # Adapters: repositories, S3, KMS, fallback
    SafeRideKids.Biometria.Providers.Aws/        # IFaceVerificationProvider via Rekognition
    SafeRideKids.Biometria.Providers.Unico/      # IFaceVerificationProvider via Unico
    SafeRideKids.Biometria.Providers.Stubs/      # Stubs documentais Azure/FaceTec/Serpro (NotImplementedException + XML doc explicativo)
    SafeRideKids.Biometria.Tests/                # xUnit, mocks, datasets sintéticos
  MobileFamilia/
    SafeRideKids.Familia.Maui/                   # .NET MAUI (Android + iOS)
  MobileMotorista/
    SafeRideKids.Motorista.Maui/                 # .NET MAUI (Android + iOS)
  Dashboard/
    SafeRideKids.Dashboard.Blazor/               # Blazor Server
infra/
  cdk/
    SafeRideKids.Poc.Infra/                      # AWS CDK em C#
docs/
  poc/
    CONTRACTS.md                                 # este arquivo
    matriz-fornecedores.md                       # comparativo de todos
    lgpd-rip-dpia.md                             # RIPD/DPIA esqueleto
    lgpd-consentimento.md                        # texto de consentimento do responsável
    lgpd-retencao.md                             # política de retenção
    plano-de-teste.md                            # protocolo de Fase A (adultos)
SafeRideKids.Poc.sln                             # solução .NET consolidada
```

**Regra de isolamento.** Cada agente é dono exclusivo do seu subdiretório listado acima. Não modifique pastas de outro agente. Arquivos compartilhados (`SafeRideKids.Poc.sln`) são responsabilidade do **Agente 1 (Backend)**, que cria a solution; outros agentes adicionam seus projetos via comentário pedindo inclusão (não tentem editar o `.sln` se não for seu).

---

## 3. Interface canônica `IFaceVerificationProvider`

Definida em `SafeRideKids.Biometria.Core/Providers/IFaceVerificationProvider.cs`. Todos os providers concretos implementam **exatamente** esta interface.

```csharp
using System.Threading;
using System.Threading.Tasks;

namespace SafeRideKids.Biometria.Core.Providers;

public interface IFaceVerificationProvider
{
    /// <summary>Identificador estável do provedor. Ex.: "aws-rekognition", "unico-idcloud".</summary>
    string ProviderId { get; }

    /// <summary>
    /// Enrola N fotos da criança (3-5) e devolve referência opaca para uso posterior em verificações.
    /// Implementações NÃO devem persistir fotos raw em storage durável. Template/embedding pode
    /// ser devolvido (para o caller salvar cifrado) ou ficar armazenado no provedor (referenceId).
    /// </summary>
    Task<EnrollmentResult> EnrollAsync(EnrollmentRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Inicia sessão de liveness no provedor. Devolve sessionId/config que o app mobile usa
    /// para invocar o SDK nativo do provedor (ex.: Rekognition Face Liveness sessionId,
    /// Unico Process SDK configuration).
    /// </summary>
    Task<LivenessSessionInfo> StartLivenessSessionAsync(LivenessSessionRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Verifica resultado do liveness e faz comparação 1:1 contra o enrollment da criança esperada.
    /// Implementações fazem as duas coisas atomicamente quando o SDK suporta (Unico),
    /// ou primeiro consultam status do liveness e depois chamam CompareFaces (AWS).
    /// </summary>
    Task<VerificationResult> VerifyAsync(VerificationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Apaga o enrollment no provedor (atende DSR-delete). Implementação deve ser idempotente.
    /// </summary>
    Task DeleteEnrollmentAsync(string providerReferenceId, CancellationToken cancellationToken);
}

public sealed record EnrollmentRequest(
    string ChildId,
    string TenantId,
    IReadOnlyList<byte[]> Images,            // 3-5 imagens, JPEG, max 1080x1080. Nunca persistidas.
    string ConsentReferenceId                // ID do consentimento que autoriza enrollment
);

public sealed record EnrollmentResult(
    bool Success,
    string ProviderReferenceId,              // ID opaco do provedor (ex.: AWS FaceId, Unico subjectId)
    byte[]? EncryptedTemplate,               // Opcional: alguns provedores devolvem template para o caller salvar
    string? ErrorCode,
    string? ErrorMessage
);

public sealed record LivenessSessionRequest(
    string CheckInId,
    string ChildId,
    string TenantId
);

public sealed record LivenessSessionInfo(
    string SessionId,                        // SessionId do provedor (passar ao SDK mobile)
    IReadOnlyDictionary<string, string> SdkConfig,  // Qualquer parâmetro adicional p/ o SDK
    DateTimeOffset ExpiresAt
);

public sealed record VerificationRequest(
    string SessionId,
    string ChildId,
    string TenantId,
    string ProviderReferenceId
);

public sealed record VerificationResult(
    VerificationOutcome Outcome,             // Approved, Inconclusive, Rejected
    double? Confidence,                      // 0..1 quando disponível
    bool LivenessPassed,
    string? FailureReason,
    long LatencyMs,
    long? ProviderCostMicroCents             // Custo estimado em microcentavos USD (telemetria)
);

public enum VerificationOutcome
{
    Approved,
    Inconclusive,                            // Cair em fallback obrigatoriamente
    Rejected
}
```

**Erros esperados.** Implementações usam `EnrollmentResult.ErrorCode` / `VerificationResult.FailureReason` para reportar — não lançar exceção em fluxo de negócio. Reservam exceção para falha de infra/rede irrecuperável.

---

## 4. Política de roteamento (Approach C — server-side)

`SafeRideKids.Biometria.Core/Routing/IProviderRouter.cs`:

```csharp
public interface IProviderRouter
{
    /// <summary>
    /// Decide qual provedor usar para este check-in. Estratégia padrão é hash determinístico
    /// (childId + UTC date) modulo 2 → 50/50 entre AWS e Unico, com a mesma criança
    /// sempre indo no mesmo provedor no mesmo dia (estabilidade UX e telemetria).
    /// Override por family.preferred_provider_id quando definido.
    /// </summary>
    string ResolveProviderId(string tenantId, string familyId, string childId, DateTimeOffset utcNow);
}
```

Implementação default em `SafeRideKids.Biometria.Infrastructure/Routing/HashBasedProviderRouter.cs`. Lista de provedores ativos configurável via env var `BIOMETRIA_PROVIDERS_ATIVOS` (CSV, default `aws-rekognition,unico-idcloud`).

---

## 5. Modelo de dados (PostgreSQL)

Schema `biometria_poc`. Use migrations (`Backend/SafeRideKids.Biometria.Infrastructure/Persistence/Migrations/`) compatíveis com EF Core 8 ou Dapper-FluentMigrator (escolher EF Core para simplicidade).

```sql
-- families
CREATE TABLE family (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  responsavel_email_hash VARCHAR(128) NOT NULL,  -- HMAC-SHA256
  preferred_provider_id  VARCHAR(64) NULL,       -- override de routing
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  CONSTRAINT family_tenant_email_uk UNIQUE (tenant_id, responsavel_email_hash)
);
CREATE INDEX family_tenant_ix ON family(tenant_id);

-- children (sem nome real — armazena hash p/ buscas idempotentes; nome plaintext fica em coluna cifrada via pgcrypto)
CREATE TABLE child (
  id              UUID PRIMARY KEY,
  family_id       UUID NOT NULL REFERENCES family(id) ON DELETE CASCADE,
  tenant_id       VARCHAR(64) NOT NULL,
  display_name_encrypted BYTEA NOT NULL,         -- pgp_sym_encrypt(name, key)
  birth_year      SMALLINT NOT NULL,
  school_name     VARCHAR(200) NOT NULL,
  boarding_mode   VARCHAR(16) NOT NULL DEFAULT 'manual', -- 'manual' | 'facial' | 'tag'
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX child_family_ix ON child(family_id);

-- consents (LGPD): consentimento explícito do responsável p/ biometria da criança
CREATE TABLE consent (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  family_id       UUID NOT NULL REFERENCES family(id),
  child_id        UUID NOT NULL REFERENCES child(id),
  granted_by      VARCHAR(200) NOT NULL,         -- nome do responsável (auto-declarado)
  responsavel_cpf_hash VARCHAR(128) NOT NULL,    -- HMAC-SHA256 (não persistir CPF claro)
  granted_at      TIMESTAMPTZ NOT NULL,
  revoked_at      TIMESTAMPTZ NULL,
  scope           VARCHAR(64) NOT NULL DEFAULT 'biometria_checkin',
  consent_text_hash VARCHAR(128) NOT NULL,        -- hash do texto exato exibido (auditoria)
  ip_address      INET NOT NULL,
  user_agent      VARCHAR(500) NOT NULL,
  signed_text_storage_key VARCHAR(500) NOT NULL,  -- S3 key do texto + assinatura (PDF gerado)
  CONSTRAINT consent_active_uk UNIQUE (child_id) WHERE revoked_at IS NULL
);

-- enrollments (1 ativo por criança por provedor)
CREATE TABLE enrollment (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  child_id        UUID NOT NULL REFERENCES child(id),
  provider_id     VARCHAR(64) NOT NULL,           -- 'aws-rekognition' | 'unico-idcloud'
  provider_reference_id VARCHAR(200) NOT NULL,   -- ID opaco no provedor
  template_storage_key VARCHAR(500) NULL,         -- S3 key (KMS-encrypted) quando aplicável
  consent_id      UUID NOT NULL REFERENCES consent(id),
  status          VARCHAR(16) NOT NULL,           -- 'active' | 'expired' | 'deleted'
  enrolled_at     TIMESTAMPTZ NOT NULL,
  expires_at      TIMESTAMPTZ NOT NULL,           -- enrolled_at + 6 months
  deleted_at      TIMESTAMPTZ NULL,
  CONSTRAINT enrollment_active_per_provider UNIQUE (child_id, provider_id) WHERE status = 'active'
);
CREATE INDEX enrollment_status_expires_ix ON enrollment(status, expires_at);

-- routes & stops (necessário para o app motorista listar paradas)
CREATE TABLE route (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  motorista_id    VARCHAR(128) NOT NULL,
  vehicle_plate   VARCHAR(16) NOT NULL,
  scheduled_date  DATE NOT NULL,
  created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX route_motorista_date_ix ON route(motorista_id, scheduled_date);

CREATE TABLE route_stop (
  id              UUID PRIMARY KEY,
  route_id        UUID NOT NULL REFERENCES route(id) ON DELETE CASCADE,
  child_id        UUID NOT NULL REFERENCES child(id),
  stop_order      SMALLINT NOT NULL,
  expected_pickup_time TIMETZ NOT NULL,
  address_label   VARCHAR(200) NOT NULL,
  CONSTRAINT route_stop_order_uk UNIQUE (route_id, stop_order)
);

-- check-ins
CREATE TABLE checkin (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  route_id        UUID NOT NULL REFERENCES route(id),
  route_stop_id   UUID NOT NULL REFERENCES route_stop(id),
  child_id        UUID NOT NULL REFERENCES child(id),
  motorista_id    VARCHAR(128) NOT NULL,
  started_at      TIMESTAMPTZ NOT NULL,
  finished_at     TIMESTAMPTZ NULL,
  result          VARCHAR(16) NULL,               -- 'approved' | 'inconclusive' | 'rejected' | 'fallback'
  provider_id     VARCHAR(64) NULL,
  provider_session_id VARCHAR(200) NULL,
  confidence      NUMERIC(5,4) NULL,              -- 0.0000 .. 1.0000
  liveness_passed BOOLEAN NULL,
  used_fallback   BOOLEAN NOT NULL DEFAULT FALSE,
  fallback_type   VARCHAR(32) NULL,               -- 'pin' | 'manual' | 'autorizada'
  latency_ms      INTEGER NULL,
  provider_cost_microcents BIGINT NULL,
  geo_lat         NUMERIC(9,6) NULL,
  geo_lng         NUMERIC(9,6) NULL,
  notes           VARCHAR(500) NULL
);
CREATE INDEX checkin_route_ix ON checkin(route_id);
CREATE INDEX checkin_started_ix ON checkin(started_at);

-- event log do check-in (audit + UX recall)
CREATE TABLE checkin_event (
  id              UUID PRIMARY KEY,
  checkin_id      UUID NOT NULL REFERENCES checkin(id) ON DELETE CASCADE,
  event_type      VARCHAR(64) NOT NULL,           -- 'StartLiveness', 'LivenessFailed', 'VerifyOk', 'FallbackTriggered', etc.
  payload         JSONB NOT NULL DEFAULT '{}'::jsonb,
  occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX checkin_event_checkin_ix ON checkin_event(checkin_id);

-- fallback PIN
CREATE TABLE fallback_pin (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  child_id        UUID NOT NULL REFERENCES child(id),
  pin_hash        VARCHAR(128) NOT NULL,           -- HMAC-SHA256(pin)
  valid_from      TIMESTAMPTZ NOT NULL,
  valid_until     TIMESTAMPTZ NOT NULL,            -- valid_from + 24h
  attempts        SMALLINT NOT NULL DEFAULT 0,
  consumed_at     TIMESTAMPTZ NULL
);
CREATE INDEX fallback_pin_child_validity_ix ON fallback_pin(child_id, valid_until);

-- audit log genérico (LGPD trilha auditável)
CREATE TABLE audit_log (
  id              UUID PRIMARY KEY,
  tenant_id       VARCHAR(64) NOT NULL,
  actor_id_hash   VARCHAR(128) NOT NULL,           -- HMAC do user id
  actor_type      VARCHAR(32) NOT NULL,            -- 'family' | 'motorista' | 'admin' | 'system'
  action          VARCHAR(64) NOT NULL,            -- 'ConsentGranted', 'EnrollmentCreated', 'CheckInRecorded', 'DsrDeleteExecuted'
  target_type     VARCHAR(64) NOT NULL,
  target_id       VARCHAR(200) NOT NULL,
  payload         JSONB NOT NULL DEFAULT '{}'::jsonb,
  occurred_at     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  retention_until DATE NOT NULL                    -- política de retenção aplicada
);
CREATE INDEX audit_log_tenant_action_ix ON audit_log(tenant_id, action);
CREATE INDEX audit_log_retention_ix ON audit_log(retention_until);
```

---

## 6. Endpoints HTTP (mínimo para POC)

Todos sob `/api/v1`. JSON, Bearer JWT do Cognito (POC pode usar pool de teste).

### 6.1 Família (responsável)
- `POST /api/v1/family/register` — `{ email, name, tenantId }` → `{ familyId }`
- `POST /api/v1/family/children` — `{ familyId, displayName, birthYear, school, boardingMode }` → `{ childId }`
- `POST /api/v1/family/consent` — `{ childId, grantedBy, cpf, consentTextVersion }` → `{ consentId }`. Backend: hasheia CPF, salva texto+assinatura em S3, retorna id.
- `POST /api/v1/family/children/{childId}/enroll` — `multipart/form-data` com 3 a 5 imagens JPEG + `consentId`. Backend chama `IFaceVerificationProvider.EnrollAsync` (em **ambos** os providers em paralelo, para permitir A/B futuro). Devolve `{ enrollments: [{ providerId, status, providerReferenceId }] }`.
- `GET /api/v1/family/children/{childId}/enrollment` — status atual.
- `DELETE /api/v1/family/children/{childId}/enrollment` — DSR-delete (apaga em todos providers + S3 + DB).

### 6.2 Motorista
- `GET /api/v1/motorista/route/today` → `{ routeId, scheduledDate, stops: [{ stopId, childId, displayName, expectedPickupTime, addressLabel, boardingMode }] }`
- `POST /api/v1/motorista/checkin/start` — `{ routeStopId, childId }` → `{ checkInId, providerId, sessionInfo }`. Backend: chama `IProviderRouter.ResolveProviderId`, abre sessão liveness, persiste `checkin` com `started_at`.
- `POST /api/v1/motorista/checkin/verify` — `{ checkInId, sessionId }` (mobile invocou o SDK e recebeu confirmação). Backend chama `VerifyAsync`, persiste resultado, dispara notificação ao responsável. Devolve `{ outcome, confidence, suggestNextAction }` onde `suggestNextAction ∈ { "confirm", "fallback_pin", "fallback_manual" }`.
- `POST /api/v1/motorista/checkin/fallback/pin` — `{ checkInId, pin }` → valida PIN, registra `used_fallback=true`, fallback_type=`pin`.
- `POST /api/v1/motorista/checkin/fallback/manual` — `{ checkInId, justification, photo_optional }` → registra fallback manual auditado.
- `POST /api/v1/motorista/checkin/confirm` — `{ checkInId, geoLat, geoLng }` → finaliza, dispara notificação.

### 6.3 Dashboard
- `GET /api/v1/dashboard/checkins?from=&to=&providerId=&outcome=&page=` → paginated
- `GET /api/v1/dashboard/checkins/{id}` → detalhe + eventos
- `GET /api/v1/dashboard/metrics/summary?from=&to=` → `{ totalCheckIns, byOutcome, byProvider, p50LatencyMs, p95LatencyMs, fallbackRate, estimatedCostUsd }`
- `GET /api/v1/dashboard/families` → listar para revisão (sem dados sensíveis no resumo)
- `POST /api/v1/dashboard/dsr/{action}` — `action ∈ { access, delete, portability }`

---

## 7. Política de fallback PIN

- 6 dígitos, gerado server-side com CSPRNG.
- Backend gera 1x por dia por (childId), envia ao responsável via push (FCM mockado na POC).
- Janela: `valid_from = now`, `valid_until = now + 24h`.
- Máximo 3 tentativas; após 3 falhas, PIN invalidado e fallback manual obrigatório.
- Persistido apenas como `pin_hash = HMAC-SHA256(pin, salt_tenant)`.

---

## 8. LGPD baseline

- **Imagens raw:** in-memory only. Se infraestrutura forçar (ex.: Rekognition Face Liveness usa S3 temporário gerenciado pela AWS), documentar e usar TTL ≤ 24h + bucket com lifecycle policy.
- **Templates/embeddings:** S3 `s3://<bucket>/templates/{tenantId}/{childId}/{enrollmentId}` com `BucketEncryption.KMS` e CMK dedicada. Block public access total. Versioning ON. Lifecycle: transição para Glacier após 30 dias se ainda ativo; deleção física 6 meses após `expires_at`.
- **PII em colunas:** `display_name_encrypted` via `pgp_sym_encrypt` da `pgcrypto`. Chave gerenciada no AWS Secrets Manager.
- **PII em logs/métricas:** apenas `HMAC-SHA256(value, salt_tenant)`. Nunca CPF/email/nome plaintext em CloudWatch.
- **Retenção:**
  - Enrollment: 6 meses ativos; auto-expirado e deletado.
  - Check-in (hot): 90 dias no PostgreSQL.
  - Check-in (cold/legal): 6 anos em S3 Glacier (Marco Civil + ECA exposição).
  - Audit log: 6 anos.
  - Fotos raw: nunca.
  - Templates: deletados em DSR-delete imediatamente; senão expiram com enrollment.
- **DSR (Data Subject Request):** endpoints `dashboard/dsr/{access|delete|portability}`. SLA target: 7 dias úteis (LGPD permite 15). Delete cascateia em todos os providers via `DeleteEnrollmentAsync`.
- **DPO/Encarregado:** placeholder `dpo@saferidekids.example` no consent text. Substituir antes de qualquer teste com adultos voluntários.
- **Texto de consentimento:** versionado (`consent_text_hash` rastreia versão exata exibida). Atualização requer novo consent.

---

## 9. Notificações ao responsável

- Canal POC: **mock service** que loga no CloudWatch e grava em tabela `notification_outbox` (apenas para inspecionar pelo dashboard). Não integrar FCM real na POC.
- Templates: `checkin_approved`, `checkin_fallback_used`, `checkin_rejected_attention`, `enrollment_expiring_soon`.
- Conteúdo: nada de imagem; nome da criança (decifrado em runtime), horário, status, link p/ dashboard.

---

## 10. Naming, layout interno e estilo

- **Idioma:** comentários em **português brasileiro**; código (identificadores, mensagens internas) em **inglês**.
- **C#:** PascalCase para tipos/métodos/propriedades; camelCase para parâmetros; `_camelCase` para fields privados; `sealed` por default em records e classes não-hereditárias.
- **Async:** sufixo `Async`. Cancellation token sempre como último parâmetro.
- **Errors:** preferir `Result<T>` ou tipo discriminado a exceções em fluxo de domínio (use library `OneOf` se quiser, ou `record`-based como nas DTOs acima). Exceção apenas para falha de infra.
- **DI:** Microsoft.Extensions.DependencyInjection. Cada projeto expõe `ServiceCollectionExtensions.AddXxx()` para registrar seus serviços.
- **Logging:** `ILogger<T>` (Microsoft.Extensions.Logging). NUNCA logar imagens ou PII clara. Use `LogProperty` para campos hashados.
- **Testes:** xUnit + FluentAssertions + NSubstitute. Project `<Name>.Tests`. Cobertura mínima: providers + router + endpoints de check-in.
- **Migrations:** EF Core 8, code-first. Cada migration nomeada `YYYYMMDD_HHMM_DescricaoCurta`.

---

## 11. Dependências NuGet de referência

- `Amazon.Lambda.Core`, `Amazon.Lambda.AspNetCoreServer.Hosting` (Lambda hosting modelo ASP.NET minimal)
- `AWSSDK.Rekognition`, `AWSSDK.S3`, `AWSSDK.KeyManagementService`, `AWSSDK.SecretsManager`
- `Npgsql`, `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`
- `BCrypt.Net-Next` (PIN hashing alternativo a HMAC se necessário)
- `Microsoft.Extensions.Logging`, `Serilog.Sinks.AwsCloudWatch`
- Unico: SDK Android/iOS nativos via MAUI bindings (não há NuGet oficial; placeholder na POC: classe `UnicoFaceVerificationProvider` que chama REST API documentada — link de docs no XML comment).
- Para mocks/datasets: `Bogus`, FaceNet ONNX para benchmark local (opcional).

---

## 12. Solution layout (`SafeRideKids.Poc.sln`)

O Agente 1 (Backend) cria a solution na raiz e adiciona inicialmente os projetos do backend. Cada outro agente adiciona o seu projeto à solution **na própria pasta do agente** (`dotnet sln add ...`) — `dotnet sln` é seguro para edição concorrente sequencial, mas para evitar conflitos, agentes que terminarem depois deixam instruções claras no resumo final ("rodar `dotnet sln add caminho/Projeto.csproj`").

---

## 13. Convenção de variáveis de ambiente

```
AWS_REGION                            # us-east-1 ou sa-east-1 (preferir sa-east-1 p/ residency)
BIOMETRIA_PROVIDERS_ATIVOS            # csv: 'aws-rekognition,unico-idcloud'
BIOMETRIA_DEFAULT_PROVIDER            # 'aws-rekognition'
BIOMETRIA_S3_TEMPLATE_BUCKET          # nome do bucket
BIOMETRIA_KMS_KEY_ID                  # ARN da CMK para templates
BIOMETRIA_PG_CONNSTRING               # connection string Aurora
BIOMETRIA_PG_ENCRYPTION_KEY_SECRET    # Secrets Manager id contendo chave do pgp_sym_encrypt
UNICO_API_BASE_URL                    # ex.: https://api.unico.io
UNICO_API_KEY_SECRET                  # Secrets Manager id
COGNITO_USER_POOL_ID                  # POC pool
COGNITO_CLIENT_ID
LOG_LEVEL                             # 'Information' | 'Debug'
TENANT_SALT_SECRET                    # Secrets Manager id (salt p/ HMACs)
```

---

## 14. Stub providers documentais (Azure, FaceTec, Serpro)

Implementam `IFaceVerificationProvider` levantando `NotSupportedException("Stub documental. Justificativa: <razão>")`. O XML doc da classe descreve por que ficou stub — para a matriz comparativa em `docs/poc/matriz-fornecedores.md`. Google Cloud Vision NÃO entra como provider (vai só na matriz, justificando por que foi descartado: faz **detecção** facial, não **reconhecimento** 1:1 de pessoa específica).

---

## 15. Critérios de aceite para os agentes

Cada agente, ao terminar, deve devolver:
1. **Arquivos criados:** lista de paths relativos.
2. **Decisões adicionais tomadas:** o que precisou decidir além do que está nos contratos, e por quê.
3. **Pendências:** o que ficou stub ou TODO e por que (recurso externo, decisão de produto, escopo da POC).
4. **Como rodar:** comando(s) para `dotnet build` ou equivalente no respectivo subprojeto.
5. **Pontos de atrito:** se algo no contrato gerou conflito, sinalizar p/ revisão.

---

## 16. O que está fora do escopo da POC

- Rastreamento GPS, billing, leilão/matching, NFS-e, scoring de reputação, tag BLE/NFC.
- Integração com Cognito/Aurora/DynamoDB do projeto principal.
- Apps Família/Motorista completos — só as telas e fluxos descritos aqui.
- Modo offline com verificação biométrica local — fallback usa PIN ou manual.
- Teste com crianças — Fase B, depende de RIPD/DPIA e consentimento dos responsáveis.

---

> **Conflitos de contrato.** Se um agente encontrar conflito real (interface impossível de implementar como descrita para um provedor, etc.), parar, documentar no campo "Pontos de atrito" e seguir com a opção mais conservadora. Não modificar este documento autonomamente.
