# SafeRide Kids — Design Document do Modelo C4

- **Data:** 2026-04-15
- **Autor:** andretinchant (discovery colaborativo com os stakeholders)
- **Status:** Concepção — aguardando aprovação para iniciar plano de implementação
- **Escopo:** Modelo C4 navegável (Likec4) + ADRs de decisões em aberto, para apresentação aos stakeholders

---

## 1. Contexto do Projeto

SafeRide Kids é uma plataforma que conecta **pais/responsáveis** a **motoristas de transporte escolar**, trazendo segurança, rastreamento em tempo real, validação de embarque configurável e intermediação financeira. O projeto está em **fase de concepção** — os stakeholders apresentaram a ideia em 2026-04-14 através do documento `SafeRide_Kids_Arquitetura.pptx`.

Este documento redesenha a arquitetura apresentada no PPTX original (Azure + SQL Server + Angular + RabbitMQ) para a direção técnica definida em discovery subsequente: **AWS Serverless + .NET**, otimizada para o padrão de carga sazonal do transporte escolar (picos manhã/tarde, vales longos) e para crescimento via franquia regional.

O entregável final é um **modelo C4 navegável em Likec4**, exportado como SPA HTML estática, portável para qualquer ambiente de apresentação (pendrive, email, browser, sem dependências).

### 1.1 Audiência do modelo

- **Stakeholders não-técnicos** (nível 1 — System Context)
- **Stakeholder arquiteto** (níveis 2 e 3 — Container e Component)
- **Time de implementação futuro** (todos os níveis + ADRs)

---

## 2. Decisões de Negócio Relevantes

Regras de produto que têm impacto arquitetural direto.

### 2.1 Validação de embarque configurável por família
Flag por contrato-família define o modo de confirmação de embarque/desembarque:
- **Padrão:** motorista clica "embarcou" (confirmação manual)
- **Opcional:** validação facial (provedor externo)
- **Opcional:** validação por tag (BLE/NFC lida pelo celular do motorista)

Isso desacopla o reconhecimento facial do caminho crítico e abre espaço para upsell premium.

### 2.2 Modelo financeiro de custódia

```
Pai paga mensalidade antecipada (ex: R$ 1.000/mês)
        │
        ▼
Plataforma retém em custódia (via gateway regulado)
        │
        ▼
Plataforma negocia cada janela de serviço (semana/mês) com motoristas
        │
        ▼
Motorista executa viagem → crédito ao motorista no valor da adjudicação
        │                     (ex: R$ 22 por viagem)
        ▼
Diferença (ex: R$ 28) = receita líquida da plataforma
        │
        ▼
Liberação ao motorista em ciclo pré-definido (semanal/quinzenal)
```

**Implicação regulatória (BCB):** a retenção de pagamento antecipado caracteriza arranjo de pagamento regulado. O dinheiro **não** fica em conta bancária comum da empresa — fica em conta-cliente do gateway. A plataforma é "usuário do arranjo", o gateway é o regulado.

### 2.3 Modelo de negociação com motoristas

**Aquisição** = Leilão reverso por janela (semana/mês), com **scoring ponderado** composto por:
- Valor do lance do motorista
- Nota/avaliação do motorista
- Condição do veículo (inspeções periódicas)
- Histórico (pontualidade, conclusão de trajetos, incidentes)

**Renovação** = Preço fixo tabelado oferecido ao motorista atual, condicionado à satisfação dos clientes. Mecanismo de retenção que também favorece continuidade de vínculo entre criança e motorista.

**Pesos do scoring são configuráveis** pelo Back-office (versionáveis), aplicados em runtime pelo Scoring Engine.

### 2.4 Multi-tenancy desde o dia 1
Shared infra; isolamento por `tenantId` em toda partition key (DynamoDB), coluna (PostgreSQL) e atributo customizado Cognito. Roadmap futuro: evolução para isolamento por conta AWS quando alguma franquia demandar (ex: requisito regulatório ou cliente enterprise).

---

## 3. Decisões Arquiteturais Consolidadas

| Área | Decisão | Justificativa |
|---|---|---|
| Cloud | **AWS** | Padrão pico-vale favorece consumo; ecossistema coeso para .NET serverless |
| Compute | **AWS Lambda (.NET 8)** com **function-per-endpoint** | Granularidade máxima; no C4 Container level agrupa-se por contexto para legibilidade |
| APIs síncronas | **API Gateway REST** | Auth nativo com Cognito, throttling, cache |
| Real-time | **AWS AppSync (GraphQL subscriptions)** | Para rastreamento GPS aos pais; substitui WebSocket Lambda |
| Ingestão GPS | **Kinesis Data Streams** | Absorve picos concentrados sem Lambda quente |
| Banco transacional | **Aurora Serverless v2 (PostgreSQL)** | ACID para domínios relacionais: Contracts, Billing, Wallet (ledger), Compliance/Audit, Family & Children, Driver Onboarding (metadata), regras de scoring e settlement. Escala por ACU paga por uso |
| Banco NoSQL | **DynamoDB** | Dados quentes ou event-sourced: Trip/Tracking (com TTL), Matching/Bidding (BidStore), Notifications, Reputation (ratings), Routing (cache), perfis estendidos Cognito, sessões |
| Eventos | **EventBridge + SQS + SNS** | Bus principal + filas para processamento assíncrono + fan-out |
| Identidade | **AWS Cognito** (User Pool único) | Atributo `custom:tenantId` + grupos por role |
| Storage | **S3** | Fotos de documentos, embeddings faciais cifrados, evidências |
| Observabilidade | **CloudWatch + X-Ray** | Métricas + tracing distribuído |
| Mobile | **.NET MAUI** | Apps Família e Motorista (iOS + Android) |
| Admin Web | **Blazor** (WebAssembly ou Server — decisão de UI posterior) | Alinhado ao stack .NET |
| Mapas | **Google Maps Platform** | Directions, Distance Matrix, Geocoding |
| Push | **Firebase Cloud Messaging** | Gratuito, multiplataforma |
| IoT Core | **Fora do MVP** | Reintroduzido se houver hardware embarcado no veículo |

### 3.1 Tenant Resolver (componente transversal)
Middleware/Lambda Layer que extrai `tenantId` do JWT Cognito em **toda** requisição. Qualquer query a DynamoDB ou PostgreSQL usa esse valor como filtro obrigatório. Registrado no C4 como componente compartilhado.

---

## 4. Bounded Contexts

Treze bounded contexts, todos detalhados no nível Component.

### 4.1 Identity & Access
Autenticação, autorização, perfis de usuário, 2FA, multi-tenancy.

**Components principais:**
- `SignUpFunction`, `SignInFunction`, `RefreshTokenFunction`
- `Enable2FAFunction`, `Verify2FAFunction`
- `TenantResolverLayer` (middleware transversal)
- `CognitoUserPool` (infra)
- `ProfileStore` (DynamoDB: perfis estendidos por role)

**Integrações internas:** consumido por todos os contextos.
**Externas:** Cognito.

### 4.2 Driver Onboarding
Cadastro rigoroso de motoristas: CNH D/E, antecedentes criminais, vistoria do veículo, fotos, alvará, seguro.

**Components:**
- `SubmitDriverApplicationFunction`
- `UploadDocumentFunction`
- `RequestBackgroundCheckFunction` → provedor externo
- `ValidateDocumentFunction` (OCR + regras)
- `ApproveDriverFunction` (workflow de aprovação manual + automatizada)
- `DriverDocumentStore` (S3 + metadata em PostgreSQL)

**Externas:** Background Check Provider (ADR-003).

### 4.3 Family & Children
Cadastro de pai/responsável, filhos, escola, endereço, foto facial para reconhecimento, flag de embarque.

**Components:**
- `RegisterFamilyFunction`, `AddChildFunction`
- `UploadChildPhotoFunction` → gera embedding facial
- `ConfigureBoardingModeFunction` (flag: manual / facial / tag)
- `FamilyStore` (PostgreSQL: entidades), `ChildEmbeddingStore` (S3 cifrado)

**Externas:** Face Recognition Provider (ADR-002) para gerar embedding.

### 4.4 Matching & Bidding
Núcleo do marketplace. Dois fluxos: leilão reverso (aquisição) e renovação com preço fixo (retenção).

**Components:**
- `OpenBidWindowFunction` (cria leilão para um contrato-família)
- `NotifyEligibleDriversFunction`
- `SubmitBidFunction`
- `ScoringEngineFunction` (aplica pesos versionados)
- `AwardAssignmentFunction` (seleciona vencedor → cria ServiceAssignment)
- `OfferRenewalFunction` (preço fixo tabelado)
- `AcceptRenewalFunction` / `DeclineRenewalFunction`
- `BidStore` (DynamoDB), `ScoringRulesStore` (PostgreSQL)

**Eventos consumidos:** `ContractCreated`, `AssignmentPeriodEnding`, `SatisfactionEvaluated`
**Eventos produzidos:** `BidWindowOpened`, `BidSubmitted`, `ServiceAssignmentAwarded`, `RenewalOffered`, `RenewalAccepted`, `RenewalDeclined`

### 4.5 Reputation & Rating
Avaliações, inspeções de veículo, métricas de qualidade. Alimenta Matching & Bidding e Admin.

**Components:**
- `SubmitRatingFunction` (pai avalia motorista pós-trajeto)
- `RecordIncidentFunction`
- `RunVehicleInspectionFunction` (agenda + captura)
- `ComputeDriverScoreFunction` (agrega histórico em score consolidado)
- `EvaluateFamilySatisfactionFunction` (gate da renovação)
- `RatingStore` (DynamoDB)

**Eventos produzidos:** `RatingSubmitted`, `InspectionCompleted`, `IncidentRecorded`, `SatisfactionEvaluated`

### 4.6 Contracts
Dois tipos de acordo jurídico/operacional.

**Family Subscription** (pai ↔ plataforma): mensal/semestral/anual, valor fixo.
**Service Assignment** (plataforma ↔ motorista): janela semanal/mensal, valor variável por leilão ou tabela.

**Components:**
- `CreateFamilySubscriptionFunction`, `RenewFamilySubscriptionFunction`, `CancelFamilySubscriptionFunction`
- `CreateServiceAssignmentFunction` (invocado pelo `AwardAssignmentFunction` ou `AcceptRenewalFunction` do Matching & Bidding)
- `CloseServiceAssignmentFunction` (fim da janela → gatilho de renovação)
- `ContractStore` (PostgreSQL)

**Eventos produzidos:** `FamilySubscriptionCreated`, `FamilySubscriptionRenewed`, `FamilySubscriptionCanceled`, `ServiceAssignmentCreated`, `ServiceAssignmentClosed`, `AssignmentPeriodEnding`

### 4.7 Billing & Payments
Cobrança recorrente dos pais, emissão de NFS-e, gestão de inadimplência.

**Components:**
- `ChargeSubscriptionFunction` (invocado por CloudWatch Events no ciclo de cobrança)
- `HandlePaymentWebhookFunction` (callback do gateway)
- `IssueInvoiceFunction` → NFS-e Provider
- `HandleRefundFunction`
- `DelinquencyStore` (PostgreSQL)

**Externas:** Payment Gateway (ADR-001, candidato: **PagBank**), NFS-e Provider (ADR-004).
**Eventos produzidos:** `SubscriptionPaid`, `PaymentFailed`, `PaymentRefunded`

### 4.8 Wallet & Settlement
Ledger interno com três contas lógicas: **Custody**, **Driver Payable**, **Platform Revenue**.

**Components:**
- `RecordCustodyFunction` (consumer de `SubscriptionPaid`)
- `CreditDriverFunction` (consumer de `TripCompleted` — move Custody → Driver Payable + Platform Revenue)
- `CloseSettlementCycleFunction` (agendado: fecha ciclo semanal/quinzenal)
- `ExecuteDriverTransferFunction` → Payment Gateway (transfer ao motorista)
- `HandleDisputeFunction` (tratamento de estornos e retenção preventiva)
- `LedgerStore` (PostgreSQL com restrições transacionais fortes)
- `SettlementRulesStore` (PostgreSQL: regras de ciclo, janela de contestação, mínimo de saque)

**Externas:** Payment Gateway (transfer out).
**Eventos consumidos:** `SubscriptionPaid`, `TripCompleted`, `PaymentRefunded`
**Eventos produzidos:** `SettlementCycleClosed`, `DriverPaid`

### 4.9 Routing
Otimização de rotas, geofencing, cálculo de ETA.

**Components:**
- `PlanRouteFunction` (otimização multi-aluno + destino)
- `ComputeETAFunction`
- `CheckGeofenceFunction` (entrada/saída dos raios casa/escola)
- `DetectRouteDeviationFunction`
- `RouteCache` (DynamoDB com TTL)

**Externas:** Google Maps Platform (Directions, Distance Matrix, Geocoding).
**Eventos produzidos:** `RoutePlanned`, `GeofenceEntered`, `RouteDeviated`

### 4.10 Trip & Tracking
Ciclo de vida da corrida: start → telemetria GPS → embarques → chegada → conclusão. Inclui Boarding (configurável).

**Components:**
- `StartTripFunction`
- `IngestGpsTelemetryFunction` (Lambda consumer do Kinesis)
- `PublishPositionToSubscribersFunction` (via AppSync subscriptions)
- `RecordBoardingFunction` (modo: manual | facial | tag)
- `VerifyFaceMatchFunction` → Face Recognition Provider
- `VerifyTagReadFunction`
- `RecordAlightingFunction` (check-out com geoloc)
- `CompleteTripFunction` (emite `TripCompleted`)
- `TripStore` (DynamoDB), `TelemetryStore` (DynamoDB com TTL 90 dias)

**Externas:** Face Recognition Provider (ADR-002).
**Eventos consumidos:** `RoutePlanned`, `ServiceAssignmentCreated`
**Eventos produzidos:** `TripStarted`, `BoardingRecorded`, `AlightingRecorded`, `TripCompleted`, `LocationDeviated`

### 4.11 Notifications
Push, alertas contextuais, canais.

**Components:**
- `SendPushNotificationFunction` → FCM
- `NotificationTemplateStore` (DynamoDB)
- `NotificationPreferencesStore` (DynamoDB por usuário)
- `NotificationAuditStore` (histórico para Compliance)

**Externas:** Firebase Cloud Messaging.
**Eventos consumidos:** praticamente todos os eventos de domínio (via EventBridge rules)

### 4.12 Compliance & Audit
Trilhas de auditoria, DPIA, retenção de dados LGPD, canal de denúncia, botão de emergência, logs invioláveis.

**Components:**
- `RecordAuditEventFunction` (consumer genérico de eventos sensíveis)
- `HandleDataSubjectRequestFunction` (LGPD: acesso, retificação, exclusão)
- `RunRetentionPolicyFunction` (agendado)
- `RaiseEmergencyFunction` (botão do app)
- `AuditLogStore` (PostgreSQL + S3 para arquivamento imutável)

**Externas:** nenhuma no MVP.

### 4.13 Admin / Back-office
Painel operacional (Blazor) para equipe interna e DPO.

**Components:**
- `AdminAuthFunction` (Cognito com grupo admin)
- `ManageDriversFunction`, `ManageFamiliesFunction`
- `ViewLedgerFunction`, `ViewReportsFunction`
- `ManageScoringRulesFunction` (versiona pesos do Matching)
- `ManageSettlementRulesFunction`
- `DisputesFunction` (mediação pais ↔ motoristas)

**Consome:** read models de quase todos os contextos via APIs internas.

---

## 5. Integrações Externas

### 5.1 Decisões consolidadas (nomeadas no C4)
- **AWS** — provedor de plataforma
- **Google Maps Platform** — mapas e rotas
- **Firebase Cloud Messaging** — push notifications

### 5.2 Em ADR / roadmap (abstratas no C4)
- `Payment Gateway` → ADR-001 — candidato: **PagBank**; alternativas: Asaas, Stripe Connect, Iugu
- `Face Recognition Provider` → ADR-002 — candidatos: AWS Rekognition, Azure Face API
- `Background Check Provider` → ADR-003 — candidatos: IDwall, Truora, Serasa
- `NFS-e Provider` → ADR-004 — candidatos: NFe.io, Focus NFe

Cada "external system" abstrato no Likec4 tem um link que aponta para o ADR correspondente no repositório.

---

## 6. Eventos entre Contextos (Consolidado)

| Evento | Produtor | Consumidores |
|---|---|---|
| `FamilySubscriptionCreated` | Contracts | Matching & Bidding, Billing |
| `SubscriptionPaid` | Billing & Payments | Wallet (abre custody), Contracts |
| `PaymentFailed` | Billing & Payments | Contracts, Notifications |
| `PaymentRefunded` | Billing & Payments | Wallet |
| `BidWindowOpened` | Matching & Bidding | Notifications |
| `BidSubmitted` | Matching & Bidding | Scoring Engine (interno) |
| `ServiceAssignmentAwarded` | Matching & Bidding | Contracts, Notifications |
| `ServiceAssignmentCreated` | Contracts | Trip & Tracking, Routing |
| `AssignmentPeriodEnding` | Contracts | Matching & Bidding |
| `SatisfactionEvaluated` | Reputation & Rating | Matching & Bidding |
| `RenewalOffered` / `RenewalAccepted` / `RenewalDeclined` | Matching & Bidding | Contracts |
| `RoutePlanned` | Routing | Trip & Tracking |
| `TripStarted` | Trip & Tracking | Notifications |
| `BoardingRecorded` / `AlightingRecorded` | Trip & Tracking | Notifications, Compliance |
| `GeofenceEntered` | Routing | Notifications |
| `LocationDeviated` | Trip & Tracking | Notifications, Compliance |
| `TripCompleted(awardValue)` | Trip & Tracking | Wallet, Reputation |
| `RatingSubmitted` | Reputation & Rating | Admin |
| `SettlementCycleClosed` | Wallet & Settlement | Payment Gateway (transfer) |
| `DriverPaid` | Wallet & Settlement | Notifications, Compliance |

---

## 7. Dynamic Views (Fluxos Críticos)

Três diagramas de sequência navegáveis no Likec4.

### 7.1 Bidding Flow
Abertura do leilão → notificação de motoristas elegíveis → lances → scoring ponderado → adjudicação → emissão do `ServiceAssignmentCreated` → renovação no ciclo seguinte ou novo leilão.

### 7.2 Trip Lifecycle
`ServiceAssignmentCreated` → `RoutePlanned` → `TripStarted` → ingestão contínua de GPS via Kinesis → distribuição aos pais via AppSync → `BoardingRecorded` (conforme flag) → `AlightingRecorded` → `TripCompleted` → notificações.

### 7.3 Payment & Settlement Flow
`SubscriptionPaid` → Wallet abre custody → a cada `TripCompleted(awardValue)` move Custody → Driver Payable + Platform Revenue → `SettlementCycleClosed` dispara transfer ao motorista via gateway → `DriverPaid`.

---

## 8. Estrutura do Workspace Likec4

```
saferidekids/
├── SafeRide_Kids_Arquitetura.pptx      (doc original dos stakeholders — preservado)
├── c4/
│   ├── workspace.c4                     (raiz do modelo: imports, views)
│   ├── model/
│   │   ├── actors.c4                    (pai, motorista, admin, DPO)
│   │   ├── external-systems.c4          (AWS, Google Maps, FCM, gateways abstratos)
│   │   └── platform.c4                  (container raiz SafeRide Kids com os 13 contextos)
│   ├── contexts/                        (um arquivo por bounded context)
│   │   ├── identity-access.c4
│   │   ├── driver-onboarding.c4
│   │   ├── family-children.c4
│   │   ├── matching-bidding.c4
│   │   ├── reputation-rating.c4
│   │   ├── contracts.c4
│   │   ├── billing-payments.c4
│   │   ├── wallet-settlement.c4
│   │   ├── routing.c4
│   │   ├── trip-tracking.c4
│   │   ├── notifications.c4
│   │   ├── compliance-audit.c4
│   │   └── admin-backoffice.c4
│   ├── views/
│   │   ├── system-context.c4            (nível 1)
│   │   ├── container-view.c4            (nível 2 — agrupado por contexto)
│   │   ├── component-views.c4           (nível 3 — uma view por contexto)
│   │   └── dynamic/
│   │       ├── bidding-flow.c4
│   │       ├── trip-lifecycle.c4
│   │       └── payment-settlement-flow.c4
│   └── dist/                            (HTML estático gerado — entregue aos stakeholders)
├── docs/
│   ├── adrs/
│   │   ├── ADR-001-payment-gateway.md
│   │   ├── ADR-002-face-recognition.md
│   │   ├── ADR-003-background-check.md
│   │   └── ADR-004-nfse-provider.md
│   └── superpowers/specs/
│       └── 2026-04-15-saferidekids-c4-design.md  (este documento)
└── README.md                            (como navegar o modelo e gerar o HTML)
```

### 8.1 Convenções
- Cada contexto define seus componentes em seu próprio `.c4` usando `extend` no modelo raiz
- Nomes de componentes seguem o padrão `<Verbo><Substantivo>Function` (ex: `SubmitBidFunction`) para evidenciar a granularidade function-per-endpoint
- External systems abstratos recebem metadado `status = "decision-pending"` e link para o ADR
- Cores: um tom por contexto (ajuda a leitura do nível Container agrupado)

---

## 9. Entregáveis

| Artefato | Descrição | Uso |
|---|---|---|
| `c4/dist/` | SPA HTML estática navegável | Principal: apresentação aos stakeholders em qualquer ambiente |
| Screenshots PNG/SVG | Exportados dos diagramas Likec4 | Para inclusão em slides, emails |
| 4 ADRs | Decisões técnicas em aberto com candidatos | Pauta para próximas reuniões de decisão |
| Este design doc | Referência textual consolidada | Fonte da verdade para o modelo |
| PPTX original preservado | `SafeRide_Kids_Arquitetura.pptx` | Contexto histórico da proposta inicial |

---

## 10. Riscos e Gaps Remanescentes

### 10.1 Do PPTX original (ainda válidos)
- **LGPD com dados de menores** exige consentimento destacado e base legal específica; DPO obrigatório; DPIA antes do MVP ir ao ar
- **Viés algorítmico em reconhecimento facial** para crianças e tons de pele escuros — testar extensivamente
- **Crescimento da criança altera biometria** → recadastro obrigatório a cada 6 meses
- **ECA Art. solidária** — responsabilidade da plataforma em incidentes
- **CTB Art. 136-139** — requisitos de transporte escolar no cadastro do motorista
- **Custos do Google Maps** — otimizar com cache; Route Cache no contexto Routing
- **Disponibilidade em horário de pico** — manhã/tarde escolar; planejamento de concorrência no Lambda

### 10.2 Decisões pendentes (em ADR)
- Payment Gateway definitivo (ADR-001)
- Face Recognition Provider (ADR-002)
- Background Check Provider (ADR-003)
- NFS-e Provider (ADR-004)

### 10.3 Decisões deixadas para fase posterior
- Escolha entre **Blazor Server** vs **Blazor WebAssembly** no Admin (depende de requisitos de UX e offline)
- Ciclo de liberação do Wallet & Settlement (semanal vs quinzenal vs mensal) — regra de negócio a ser definida com o jurídico
- Mecânica de leilão detalhada (timeout, número mínimo de lances, preço de reserva) — especificação do produto
- Tabela de preços de renovação — regra de negócio
- Estratégia de recadastro biométrico de crianças (6 meses)
- Hardware embarcado no veículo → se/quando entrar, reintroduz IoT Core

### 10.4 Não abordado (explicitamente fora de escopo do C4 de concepção)
- Detalhamento de schemas DynamoDB (access patterns) — vem no design de cada contexto
- Schemas PostgreSQL — idem
- Estratégia de deployment (IaC: CDK, Terraform, SAM) — decisão posterior
- CI/CD específico — decisão posterior
- Estratégia de testes (unit/integration/E2E) — decisão posterior

---

## 11. Próximos Passos

1. Usuário revisa este design doc e aprova ou pede ajustes
2. Invocar `superpowers:writing-plans` para produzir plano executável que construirá:
   - Workspace Likec4 completo (13 contextos + 3 dynamic views + views consolidadas)
   - 4 ADRs estruturados (formato MADR)
   - README do repositório com instruções de build e navegação
   - Build do HTML estático em `c4/dist/`
3. Executar o plano incrementalmente, commitando a cada seção
4. Apresentação aos stakeholders
