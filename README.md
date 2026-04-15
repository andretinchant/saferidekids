# SafeRide Kids

Plataforma serverless multi-tenant que conecta famílias a motoristas de transporte escolar, com rastreamento em tempo real, validação de embarque configurável e intermediação financeira com custódia regulamentada.

**Status:** concepção arquitetural (2026-04).

## Documentação

- **Apresentação original dos stakeholders:** [`SafeRide_Kids_Arquitetura.pptx`](SafeRide_Kids_Arquitetura.pptx)
- **Design spec do C4:** [`docs/superpowers/specs/2026-04-15-saferidekids-c4-design.md`](docs/superpowers/specs/2026-04-15-saferidekids-c4-design.md)
- **Plano de implementação do C4:** [`docs/superpowers/plans/2026-04-15-saferidekids-c4-implementation.md`](docs/superpowers/plans/2026-04-15-saferidekids-c4-implementation.md)
- **ADRs (decisões pendentes):** [`docs/adrs/`](docs/adrs/)

## Modelo C4 Navegável

O C4 está em [`c4/`](c4/), escrito em [Likec4](https://likec4.dev/). Para gerar e apresentar aos stakeholders:

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

Gera estrutura multi-arquivo em `c4/dist/` otimizada para hospedagem (GitHub Pages, S3, etc.). Requer servir via HTTP — não abre em `file://`. Para testar localmente: `npm run preview`.

### Modo dev (navegação interativa com hot-reload)

```bash
cd c4
npm start
```

Abre servidor local (padrão http://localhost:5173).

### Exportar imagens para slides

```bash
cd c4
npm run export:png
npm run export:svg
```

## Estrutura do modelo

```
c4/
├── workspace.c4           Specification, atores, sistemas externos, plataforma
├── contexts/              13 bounded contexts (um arquivo cada)
└── views/
    ├── system-views.c4    Landscape (nível 1) + Containers (nível 2)
    ├── component-views.c4 Components por contexto (nível 3)
    └── dynamic-views.c4   Bidding, Trip Lifecycle, Payment & Settlement
```

## Níveis do modelo

- **Level 1 — Landscape:** plataforma como caixa preta + atores + sistemas externos
- **Level 2 — Containers:** apps, APIs, infra, e os 13 contextos
- **Level 3 — Components:** funções Lambda por endpoint agrupadas por contexto
- **Dynamic Views:** Bidding & Renewal Flow, Trip Lifecycle, Payment & Settlement Flow

Total: **19 views navegáveis**.

## Arquitetura — decisões consolidadas

- AWS Serverless (.NET 8 Lambdas function-per-endpoint)
- Aurora Serverless v2 (PostgreSQL) + DynamoDB híbrido
- API Gateway REST + AppSync GraphQL subscriptions (real-time)
- Kinesis Data Streams (ingestão GPS)
- EventBridge + SQS + SNS (event-driven)
- Cognito (identidade multi-tenant)
- Google Maps Platform, FCM, S3, CloudWatch + X-Ray
- Apps: .NET MAUI (família, motorista); Web: Blazor (admin)
- Multi-tenancy shared-infra via `custom:tenantId` (dia 1)

## Decisões pendentes

Cada um desses aparece no modelo C4 marcado com tag `decision-pending`, linkado ao ADR correspondente:

- [ADR-001: Payment Gateway](docs/adrs/ADR-001-payment-gateway.md) — candidato: **PagBank**
- [ADR-002: Face Recognition Provider](docs/adrs/ADR-002-face-recognition.md)
- [ADR-003: Background Check Provider](docs/adrs/ADR-003-background-check.md)
- [ADR-004: NFS-e Provider](docs/adrs/ADR-004-nfse-provider.md)
