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

Projeto concebido por Vick Monteiro. Modelagem arquitetural e C4 por [@andretinchant](https://github.com/andretinchant).
