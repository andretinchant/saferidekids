# SafeRide Kids

> Transporte escolar inteligente e seguro — conectando pais e motoristas com rastreamento em tempo real, validação de embarque configurável e intermediação financeira regulamentada.

**Status:** concepção arquitetural (abril/2026) · **Fase atual:** modelagem C4 e ADRs técnicos

---

## Visão do Produto

A plataforma endereça dores reais do transporte escolar no Brasil:

- Pais sem visibilidade sobre o trajeto dos filhos
- Motoristas sem validação rigorosa de antecedentes
- Comunicação desorganizada via WhatsApp
- Sem controle digital de embarque/desembarque
- Gestão financeira manual e sem integração

**Para os pais:** buscar motoristas verificados, acompanhar a van em tempo real, receber notificações de embarque, pagar pelo app, avisar faltas.

**Para os motoristas:** receber oportunidades por leilão e renovação, gerenciar rotas otimizadas, executar check-in (manual/facial/tag), e receber por serviço prestado.

**Para a plataforma:** garantir LGPD compliance, mediar disputas, reter custódia regulamentada dos pagamentos e reconhecer receita por diferença entre mensalidade e adjudicação.

## Funcionalidades Principais

| Feature | Descrição |
|---|---|
| **Matching & Bidding** | Leilão reverso por janela + renovação com preço fixo (retenção) |
| **Check-in configurável** | Flag por família: manual (padrão), facial, ou tag BLE/NFC |
| **Rastreamento real-time** | GPS do app motorista → Kinesis → AppSync → app família |
| **Geofencing** | Notificação automática ao entrar em raios casa/escola |
| **Otimização de rotas** | Google Maps com cache para reduzir custos |
| **Pagamento recorrente** | Mensal/semestral/anual com NFS-e automática |
| **Intermediação financeira** | Ledger interno (Custody / Driver Payable / Platform Revenue) |
| **Reputation & Rating** | Score ponderado alimenta scoring de leilões e gate de renovação |
| **Compliance LGPD** | Trilhas auditáveis, DPIA, canal de denúncia, botão de emergência |

## Aplicações

- **SafeRide Kids — Família** (mobile, .NET MAUI)
- **SafeRide Kids — Motorista** (mobile, .NET MAUI)
- **SafeRide Kids — Admin Web** (Blazor)

## Arquitetura

### Stack consolidado

- **Cloud:** AWS Serverless (pay-per-use, elástico para picos escolares)
- **Compute:** Lambda .NET 8 (function-per-endpoint)
- **APIs:** API Gateway REST + AppSync (GraphQL subscriptions para real-time)
- **Bancos:** Aurora Serverless v2 (PostgreSQL) transacional + DynamoDB para dados quentes
- **Eventos:** EventBridge + SQS + SNS (event-driven entre contextos)
- **Streaming GPS:** Kinesis Data Streams
- **Identidade:** Cognito (multi-tenant dia 1 via `custom:tenantId`)
- **Storage:** S3 (documentos, embeddings faciais cifrados, auditoria)
- **Observabilidade:** CloudWatch + X-Ray

### Integrações externas

- **Google Maps Platform** (Directions, Distance Matrix, Geocoding)
- **Firebase Cloud Messaging** (push)
- **Payment Gateway** (ADR-001 — candidato: PagBank)
- **Face Recognition Provider** (ADR-002)
- **Background Check Provider** (ADR-003)
- **NFS-e Provider** (ADR-004)

### 13 Bounded Contexts

Identity & Access · Driver Onboarding · Family & Children · Matching & Bidding · Reputation & Rating · Contracts · Billing & Payments · Wallet & Settlement · Routing · Trip & Tracking · Notifications · Compliance & Audit · Admin / Back-office

## Modelo C4 Navegável

Em [`c4/`](c4/), escrito em [Likec4](https://likec4.dev/).

### Pré-requisitos

- Node.js 20+
- npm

### Gerar HTML portátil (entrega aos stakeholders)

```bash
cd c4
npm install
npm run build:portable
```

Gera **um único arquivo** `c4/dist/index.html` (~4 MB com tudo inline — JS, CSS, SVG). Abre direto em qualquer navegador via `file://` — **sem servidor, sem internet, sem dependências**. Ideal para pendrive, email ou pasta compartilhada.

### Gerar SPA multi-arquivo (para hospedar em servidor)

```bash
npm run build
```

Otimizada para hospedagem (GitHub Pages, S3, etc.). Requer servir via HTTP. Para testar localmente:

```bash
npm run preview
```

### Modo dev (navegação interativa com hot-reload)

```bash
npm start
```

Abre servidor local em http://localhost:5173.

### Exportar imagens para slides

```bash
npm run export:png
npm run export:svg
```

### Estrutura do workspace

```
c4/
├── workspace.c4           Specification, atores, sistemas externos, plataforma
├── contexts/              13 bounded contexts (um arquivo cada)
└── views/
    ├── system-views.c4    Landscape (nível 1) + Containers (nível 2)
    ├── component-views.c4 Components por contexto (nível 3)
    └── dynamic-views.c4   Bidding, Trip Lifecycle, Payment & Settlement
```

### Níveis do modelo (19 views navegáveis)

- **Level 1 — Landscape:** plataforma como caixa preta + atores + sistemas externos
- **Level 2 — Containers:** apps, APIs, infra e os 13 contextos
- **Level 3 — Components:** funções Lambda por endpoint agrupadas por contexto (13 views)
- **Dynamic Views:** Bidding & Renewal Flow, Trip Lifecycle, Payment & Settlement Flow

## Compliance e Regulação

- **LGPD (Lei 13.709/18):** dados de menores exigem consentimento do responsável; biometria facial é dado sensível (Art. 11); DPO obrigatório; DPIA antes do MVP
- **ECA (Lei 8.069/90):** responsabilidade solidária em incidentes; canal de denúncia; botão de emergência
- **CTB Art. 136-139:** CNH D/E, antecedentes, vistoria semestral, curso de transporte escolar
- **BCB (Circulares 3.682/3.683):** retenção de custódia via gateway regulado — plataforma nunca mantém dinheiro em conta comum
- **Marco Civil da Internet:** guarda de logs por 6 meses; direito de exclusão de dados

## Documentação

- **Apresentação original dos stakeholders:** [`SafeRide_Kids_Arquitetura.pptx`](SafeRide_Kids_Arquitetura.pptx)
- **Design spec do C4:** [`docs/superpowers/specs/2026-04-15-saferidekids-c4-design.md`](docs/superpowers/specs/2026-04-15-saferidekids-c4-design.md)
- **Plano de implementação do C4:** [`docs/superpowers/plans/2026-04-15-saferidekids-c4-implementation.md`](docs/superpowers/plans/2026-04-15-saferidekids-c4-implementation.md)
- **ADRs (decisões pendentes):**
  - [ADR-001: Payment Gateway](docs/adrs/ADR-001-payment-gateway.md) — candidato: **PagBank**
  - [ADR-002: Face Recognition Provider](docs/adrs/ADR-002-face-recognition.md)
  - [ADR-003: Background Check Provider](docs/adrs/ADR-003-background-check.md)
  - [ADR-004: NFS-e Provider](docs/adrs/ADR-004-nfse-provider.md)

## Estimativa de Custos de Infraestrutura

Estimativa para manter a plataforma viva em duas fases antes do lançamento comercial. Valores em **USD/mês** para AWS (cobrança na moeda original) e **BRL/mês** para serviços nacionais. Câmbio conservador usado para totalização: **R$ 5,00 / USD**.

> **Premissas comuns:** stack AWS Serverless conforme arquitetura, pay-per-use, multi-tenant com 1 tenant ativo na fase atual, Aurora Serverless v2 com **scale-to-0 ACU + auto-pause** (disponível desde nov/2024) para otimizar períodos ociosos.

### Fase Desenvolvimento (equipe trabalhando, sem usuários reais)

Período: ~4-6 meses de construção do MVP. Tráfego = apenas a equipe testando.

| Serviço AWS | Custo mensal (USD) | Observação |
|---|---:|---|
| Lambda | $0 | Free tier (1M invocações/mês) cobre dev folgadamente |
| API Gateway REST | $0 | Free tier 1M calls no primeiro ano; depois ~$0-3 |
| DynamoDB (on-demand) | $2-8 | Volume muito baixo em dev |
| Aurora Serverless v2 | $10-30 | **Com auto-pause ativo**: compute zera quando ocioso; cobra só storage (~5-20 GB × $0.10) + picos curtos de ACU |
| AppSync | $1-5 | Baixo volume de queries/subscriptions |
| Kinesis Data Streams | $11 | 1 shard mínimo (~$0.015/h × 720h) |
| Cognito | $0 | 50k MAU gratuitos |
| S3 | $2-5 | Documentos de teste + embeddings |
| EventBridge + SQS + SNS | $1-3 | Baixo volume de eventos |
| CloudWatch + X-Ray | $5-15 | Logs e traces de dev |
| Route 53 | $1-2 | 1 zona + poucas queries |
| Secrets Manager | $2-4 | 5-10 secrets |
| Data transfer | $1-3 | Poucos egress |
| **Subtotal AWS dev** | **~$35-90** | **≈ R$ 175-450** |

**Sem custos externos relevantes em dev** — Google Maps dentro dos $200 de crédito mensal gratuito; FCM grátis; gateways/NFS-e/face/background check em modo sandbox.

### Fase Beta (10-20 famílias, ~20 motoristas, 1-2 cidades)

Período: ~2-3 meses de validação real. Volume estimado:
- 20 famílias × 2 viagens/dia × 22 dias úteis = **~880 viagens/mês**
- GPS streaming: ~20 motoristas × 3h/dia × 22 dias × 6 pings/min = **~475k pings/mês**
- API calls gerais: **~200k/mês**
- Push notifications: **~8-15k/mês**

| Serviço AWS | Custo mensal (USD) | Observação |
|---|---:|---|
| Lambda | $1-3 | ~500k-1M invocações |
| API Gateway REST | $1-2 | ~200k chamadas |
| DynamoDB (on-demand) | $3-8 | Trip/telemetry/bidding/ratings |
| Aurora Serverless v2 | $80-180 | 0.5-1.5 ACU médio durante operação (cobrança, ledger, contracts) |
| AppSync | $2-5 | ~500k subscription updates + queries |
| Kinesis Data Streams | $22 | 2 shards para folga |
| Cognito | $0 | Ainda dentro do free tier |
| S3 | $5-15 | Storage crescendo (docs + embeddings + audit archive) |
| EventBridge + SQS + SNS | $2-4 | Volume maior de eventos |
| CloudWatch + X-Ray | $30-60 | Logs + traces em volume real |
| Route 53 | $1-2 | |
| Secrets Manager | $4-8 | Mais secrets (gateway, face, etc.) |
| Data transfer | $5-15 | Egress de mapas, documentos, HTML admin |
| **Subtotal AWS beta** | **~$155-320** | **≈ R$ 775-1.600** |

Serviços externos (variáveis com volume):

| Serviço | Custo estimado | Observação |
|---|---:|---|
| Google Maps Platform | $0 | ~5k requests/mês cabe nos $200 de crédito mensal |
| FCM | $0 | Gratuito |
| Face Recognition (Rekognition) | ~$0.30 | Assumindo 30% famílias com flag facial × 2 check/dia × 22 dias |
| Background Check | R$ 100-250 | 2-5 motoristas novos/mês |
| Payment Gateway | R$ 25-50 | Taxa típica 2,5-4% + R$0,40/tx sobre ~20 mensalidades |
| NFS-e Provider | R$ 40 | ~20 notas × R$2/nota |
| **Subtotal externos beta** | **≈ R$ 165-340 + $0,30** | |

**Total estimado fase Beta: ~R$ 950-1.950/mês** (AWS + externos).

### Custos Únicos (pré-beta)

| Item | Custo | Observação |
|---|---:|---|
| Apple Developer Program | $99/ano | Obrigatório para publicar no iOS |
| Google Play Developer | $25 (one-time) | Vale para sempre |
| Domínio `.com.br` ou `.com` | R$ 40-60/ano | Registro.br ou registrador internacional |
| Pen-testing + auditoria externa | R$ 6.000-10.000 | Obrigatório antes do MVP ir ao ar |
| Consultoria jurídica (LGPD + ECA + CTB + BCB) | R$ 5.000-8.000 | Termos, política, DPO, revisão do arranjo de pagamento |
| **Total único pré-beta** | **~R$ 11.000-18.000** | |

### Alavancas de otimização (se custos surpreenderem)

- **Aurora v2 auto-pause**: em dev, configurar timeout agressivo (ex: 5 min) — fora do expediente zera compute automaticamente
- **Route Cache no contexto Routing**: cache DynamoDB com TTL reduz 60-80% das chamadas ao Google Maps
- **Lambda Reserved Concurrency**: evita surpresas em picos escolares
- **DynamoDB on-demand → provisioned**: quando volume virar previsível (~pós-beta), migrar pode cortar 40-60% dos custos NoSQL
- **CloudWatch Logs retention**: padrão é retenção infinita — configurar 30/90 dias reduz storage
- **VPC NAT Gateway**: evitar se possível (~$33/mês fixo); usar VPC Endpoints para serviços AWS quando Aurora estiver em subnet privada
- **Spot EC2 para workloads batch**: se surgirem processamentos noturnos pesados (ex: recomputação de scores, relatórios)

### O que **não** está incluso nesta estimativa

- Salários da equipe de desenvolvimento (PPTX estimou R$ 15-40k/mês para time de 3-4 devs)
- Marketing e aquisição de clientes
- Reserva para incidentes e escalabilidade imprevista
- Seguro de responsabilidade civil da plataforma

## Roadmap

### Fase atual (concluída)
- [x] Discovery com stakeholders
- [x] Design spec arquitetural
- [x] Modelo C4 navegável (Context + Container + Component + Dynamic)
- [x] 4 ADRs para decisões pendentes

### Próximos passos
- [ ] Validação do C4 com stakeholders
- [ ] Decisão formal dos 4 ADRs (POCs com candidatos)
- [ ] Contratação de assessoria jurídica (LGPD + ECA + CTB + BCB)
- [ ] Setup de infraestrutura cloud + CI/CD
- [ ] Desenvolvimento: Auth + cadastro + matching (primeira sprint)
- [ ] Beta com 10-20 famílias
- [ ] Pen-testing + LGPD compliance antes do launch
- [ ] Publicação nas lojas (iOS + Android)

## Créditos

- **Ideia original:** Vinicius Saveta
- **Documento de arquitetura e planejamento estratégico inicial:** Vick Monteiro (abril/2026)
- **Modelagem arquitetural, C4 navegável e estimativa de custos:** [@andretinchant](https://github.com/andretinchant)
