# SafeRide Kids C4 Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a navigable C4 model for the SafeRide Kids platform using Likec4, exported as a static HTML SPA, plus 4 ADRs covering pending technical decisions.

**Architecture:** Likec4 DSL workspace with one `.c4` file per bounded context, merged automatically by the Likec4 compiler. Static build produces a portable SPA in `c4/dist/`. ADRs follow MADR format.

**Tech Stack:** Likec4 (Node.js toolchain), Markdown. No runtime dependencies in the output — `dist/` is pure HTML/JS/CSS.

**Source spec:** `docs/superpowers/specs/2026-04-15-saferidekids-c4-design.md`

---

## File Structure

```
saferidekids/
├── c4/
│   ├── package.json                     [Task 1]
│   ├── .gitignore                       [Task 1]
│   ├── workspace.c4                     [Task 2 — specification, actors, external systems, platform]
│   ├── contexts/
│   │   ├── identity-access.c4           [Task 3]
│   │   ├── driver-onboarding.c4         [Task 4]
│   │   ├── family-children.c4           [Task 5]
│   │   ├── matching-bidding.c4          [Task 6]
│   │   ├── reputation-rating.c4         [Task 7]
│   │   ├── contracts.c4                 [Task 8]
│   │   ├── billing-payments.c4          [Task 9]
│   │   ├── wallet-settlement.c4         [Task 10]
│   │   ├── routing.c4                   [Task 11]
│   │   ├── trip-tracking.c4             [Task 12]
│   │   ├── notifications.c4             [Task 13]
│   │   ├── compliance-audit.c4          [Task 14]
│   │   └── admin-backoffice.c4          [Task 15]
│   ├── views/
│   │   ├── system-views.c4              [Task 16 — landscape + system context]
│   │   ├── container-view.c4            [Task 17]
│   │   ├── component-views.c4           [Task 18 — one per context]
│   │   └── dynamic-views.c4             [Task 19 — bidding + trip + payment flows]
│   └── dist/                            [Task 21 — gitignored output]
├── docs/
│   ├── adrs/
│   │   ├── ADR-001-payment-gateway.md        [Task 20]
│   │   ├── ADR-002-face-recognition.md       [Task 20]
│   │   ├── ADR-003-background-check.md       [Task 20]
│   │   └── ADR-004-nfse-provider.md          [Task 20]
│   ├── superpowers/
│   │   ├── specs/2026-04-15-saferidekids-c4-design.md   [exists]
│   │   └── plans/2026-04-15-saferidekids-c4-implementation.md  [this file]
└── README.md                            [Task 22]
```

### Design rationale for file split
- **One file per bounded context:** keeps files focused (~50-80 lines each), change-isolation between contexts, and allows parallel work in the future. Each context owns its elements and internal relationships.
- **Views separated from model:** Likec4 merges all `.c4` files; separating views makes it easy to reorganize presentation without touching model definitions.
- **Single `workspace.c4` for specification + globals:** the `specification` block must exist once; actors and external systems are truly global and don't belong to any one context.

### Likec4 DSL reference used
Likec4 is actively developed. If any syntax in this plan fails to compile, consult https://likec4.dev/dsl/ for current reference and adapt. The core DSL constructs used here (`specification`, `model`, `views`, `view`, `dynamic view`, `include`, `autoLayout`, nested elements, `->` relationships) are stable.

---

## Task 1: Workspace Setup

**Files:**
- Create: `c4/package.json`
- Create: `c4/.gitignore`

- [ ] **Step 1: Create `c4/` directory**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
mkdir -p c4/contexts c4/views
```

- [ ] **Step 2: Create `c4/package.json`**

```json
{
  "name": "saferidekids-c4",
  "version": "0.1.0",
  "private": true,
  "description": "SafeRide Kids navigable C4 model",
  "scripts": {
    "build": "likec4 build -o dist",
    "start": "likec4 start",
    "validate": "likec4 validate",
    "export:png": "likec4 export png -o dist/png",
    "export:svg": "likec4 export svg -o dist/svg"
  },
  "devDependencies": {
    "likec4": "^1.20.0"
  }
}
```

- [ ] **Step 3: Create `c4/.gitignore`**

```
node_modules/
dist/
*.log
.likec4cache/
```

- [ ] **Step 4: Install Likec4**

Run:
```bash
cd c4
npm install
```

Expected: `node_modules/` created, `package-lock.json` created. If `likec4@^1.20.0` is unavailable, fall back to the latest available version.

- [ ] **Step 5: Verify Likec4 CLI**

Run:
```bash
npx likec4 --version
```

Expected: prints a version number (e.g., `1.20.x`). If this fails, inspect install errors before continuing.

- [ ] **Step 6: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/package.json c4/.gitignore
git commit -m "chore(c4): scaffold Likec4 workspace"
```

---

## Task 2: Specification + Actors + External Systems + Platform Root

**Files:**
- Create: `c4/workspace.c4`

- [ ] **Step 1: Write the workspace root**

Create `c4/workspace.c4` with:

```
specification {
  element actor {
    style {
      shape person
      color green
    }
  }
  element system
  element externalSystem {
    style {
      color muted
    }
  }
  element context {
    style {
      color indigo
    }
  }
  element component {
    style {
      color sky
    }
  }
  element datastore {
    style {
      shape cylinder
      color amber
    }
  }
  element queue {
    style {
      shape queue
      color slate
    }
  }

  relationship uses
  relationship publishes
  relationship subscribes
  relationship reads
  relationship writes
  relationship invokes

  tag decision-pending
  tag aws
  tag gcp
  tag external-provider
  tag mobile
  tag web
  tag serverless
}

model {
  parent = actor 'Pai / Responsável' {
    description '
      Contrata o serviço via app Família, acompanha o trajeto dos filhos,
      avalia motoristas e configura o modo de embarque.
    '
  }

  driver = actor 'Motorista Escolar' {
    description '
      Passa pelo onboarding rigoroso, participa de leilões por janelas de
      serviço, executa as viagens e recebe crédito por serviço prestado.
    '
  }

  operator = actor 'Operador / Back-office' {
    description 'Equipe interna que usa o Admin para gestão de disputas, dashboards e regras configuráveis.'
  }

  dpo = actor 'DPO / Encarregado de Dados' {
    description 'Responsável LGPD; acessa trilhas de auditoria e atende solicitações de titulares.'
  }

  aws = externalSystem 'AWS' {
    description 'Provedor de plataforma: Lambda, API Gateway, DynamoDB, Aurora Serverless v2, AppSync, Cognito, EventBridge, SQS, SNS, Kinesis, S3, CloudWatch, X-Ray.'
    #aws
  }

  gmaps = externalSystem 'Google Maps Platform' {
    description 'Directions API, Distance Matrix, Geocoding para rotas, ETA e geocodificação de endereços.'
    #external-provider #gcp
  }

  fcm = externalSystem 'Firebase Cloud Messaging' {
    description 'Entrega de push notifications para iOS e Android.'
    #external-provider
  }

  paymentGateway = externalSystem 'Payment Gateway' {
    description 'Processamento de cobrança recorrente, retenção em custódia e transfer para motoristas. Decisão pendente — ver ADR-001 (candidato: PagBank).'
    #decision-pending #external-provider
    link ../docs/adrs/ADR-001-payment-gateway.md
  }

  faceProvider = externalSystem 'Face Recognition Provider' {
    description 'Geração de embeddings e verificação facial. Decisão pendente — ver ADR-002.'
    #decision-pending #external-provider
    link ../docs/adrs/ADR-002-face-recognition.md
  }

  backgroundCheck = externalSystem 'Background Check Provider' {
    description 'Verificação de antecedentes criminais de motoristas. Decisão pendente — ver ADR-003.'
    #decision-pending #external-provider
    link ../docs/adrs/ADR-003-background-check.md
  }

  nfse = externalSystem 'NFS-e Provider' {
    description 'Emissão de notas fiscais eletrônicas de serviço. Decisão pendente — ver ADR-004.'
    #decision-pending #external-provider
    link ../docs/adrs/ADR-004-nfse-provider.md
  }

  saferide = system 'SafeRide Kids' {
    description 'Plataforma serverless multi-tenant que conecta famílias a motoristas de transporte escolar.'

    familyApp = container 'App Família' {
      description 'App mobile .NET MAUI para pais/responsáveis: busca de motoristas, acompanhamento GPS, configuração de embarque, pagamentos.'
      technology '.NET MAUI (iOS + Android)'
      #mobile
    }

    driverApp = container 'App Motorista' {
      description 'App mobile .NET MAUI para motoristas: onboarding, lances em leilão, execução de rota, registro de embarque/desembarque, telemetria GPS.'
      technology '.NET MAUI (iOS + Android)'
      #mobile
    }

    adminWeb = container 'Painel Admin' {
      description 'SPA Blazor para equipe interna e DPO: operações, dashboards financeiros, regras configuráveis, disputas.'
      technology 'Blazor WebAssembly/Server (a definir)'
      #web
    }

    apiGateway = container 'API Gateway (REST)' {
      description 'Fronteira HTTPS síncrona para apps e admin. Auth via Cognito JWT, throttling e cache.'
      technology 'AWS API Gateway'
      #aws #serverless
    }

    appsync = container 'AppSync (GraphQL Subscriptions)' {
      description 'Canal real-time de push de posição GPS aos pais com conexão ativa durante corridas.'
      technology 'AWS AppSync'
      #aws #serverless
    }

    eventBus = container 'Event Bus' {
      description 'Barramento central de eventos de domínio entre contextos.'
      technology 'AWS EventBridge + SQS + SNS'
      #aws #serverless
    }

    gpsStream = queue 'GPS Ingestion Stream' {
      description 'Absorção de telemetria GPS do app motorista em picos concentrados.'
      technology 'AWS Kinesis Data Streams'
      #aws #serverless
    }

    postgres = datastore 'Aurora Serverless v2 (PostgreSQL)' {
      description 'Armazenamento relacional transacional multi-tenant para Contracts, Billing, Wallet, Audit, Family & Children, Driver Onboarding (metadata), Scoring/Settlement rules.'
      technology 'AWS Aurora Serverless v2'
      #aws
    }

    dynamo = datastore 'DynamoDB' {
      description 'Armazenamento NoSQL multi-tenant (tenantId em partition key) para dados quentes: Trip/Tracking, Bidding, Notifications, Ratings, Routing cache, perfis estendidos, sessões.'
      technology 'AWS DynamoDB'
      #aws #serverless
    }

    s3 = datastore 'S3' {
      description 'Objetos: fotos de documentos, fotos do veículo, embeddings faciais cifrados, arquivamento imutável de audit.'
      technology 'AWS S3'
      #aws
    }

    cognito = container 'Cognito User Pool' {
      description 'Autenticação. User Pool único com custom:tenantId e grupos por role (parent, driver, operator, dpo).'
      technology 'AWS Cognito'
      #aws #serverless
    }

    observability = container 'Observability Stack' {
      description 'Métricas, logs, tracing distribuído.'
      technology 'AWS CloudWatch + X-Ray'
      #aws
    }
  }

  // Top-level relationships (actors ↔ system and system ↔ external systems)
  parent -> saferide.familyApp 'Usa para contratar e acompanhar'
  driver -> saferide.driverApp 'Usa para onboarding, lances e execução'
  operator -> saferide.adminWeb 'Gerencia operações'
  dpo -> saferide.adminWeb 'Audita e atende solicitações LGPD'

  saferide.familyApp -> saferide.apiGateway 'HTTPS/JSON'
  saferide.driverApp -> saferide.apiGateway 'HTTPS/JSON'
  saferide.adminWeb -> saferide.apiGateway 'HTTPS/JSON'

  saferide.familyApp -> saferide.appsync 'Subscribe: posição ao vivo'
  saferide.driverApp -> saferide.gpsStream 'Publish: telemetria GPS'
  saferide.driverApp -> saferide.appsync 'Subscribe: eventos de corrida'

  saferide.apiGateway -> saferide.cognito 'Valida JWT'
  saferide.familyApp -> saferide.cognito 'SignIn / SignUp'
  saferide.driverApp -> saferide.cognito 'SignIn / SignUp'
  saferide.adminWeb -> saferide.cognito 'SignIn'

  saferide -> aws 'Runs on'
  saferide -> gmaps 'Directions / Geocoding / Distance Matrix'
  saferide -> fcm 'Envia push notifications'
  saferide -> paymentGateway 'Cobrança, custódia, transfer'
  saferide -> faceProvider 'Embedding + verificação facial'
  saferide -> backgroundCheck 'Consulta antecedentes'
  saferide -> nfse 'Emissão de NFS-e'
}
```

- [ ] **Step 2: Validate the workspace**

Run:
```bash
cd c4
npx likec4 validate
```

Expected: no errors. If validation complains about unknown constructs, inspect the error, check https://likec4.dev/dsl/, and adjust. Specific likely issues:
- `style {}` nesting — if rejected, move style into an inline `{ style { ... } }` on element definition
- `tag` references use `#tagname` — verify the version in use
- `link` syntax — may be `#someTag link="url"` in older versions

If `likec4 validate` does not exist, run `npx likec4 build -o dist` instead — it performs validation as part of build.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/workspace.c4
git commit -m "feat(c4): add specification, actors, external systems and platform root"
```

---

## Task 3: Context — Identity & Access

**Files:**
- Create: `c4/contexts/identity-access.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    identityAccess = context 'Identity & Access' {
      description 'Autenticação, autorização, perfis estendidos, 2FA, Tenant Resolver transversal.'

      signUpFn = component 'SignUpFunction' {
        description 'Cadastro inicial de usuário (pai/motorista).'
        technology 'AWS Lambda (.NET 8)'
      }
      signInFn = component 'SignInFunction' {
        description 'Login com usuário/senha via Cognito.'
        technology 'AWS Lambda (.NET 8)'
      }
      refreshTokenFn = component 'RefreshTokenFunction' {
        description 'Renovação de JWT.'
        technology 'AWS Lambda (.NET 8)'
      }
      enable2faFn = component 'Enable2FAFunction' {
        description 'Habilita 2FA para o usuário.'
        technology 'AWS Lambda (.NET 8)'
      }
      verify2faFn = component 'Verify2FAFunction' {
        description 'Valida código 2FA durante login sensível.'
        technology 'AWS Lambda (.NET 8)'
      }
      tenantResolver = component 'TenantResolverLayer' {
        description 'Middleware compartilhado: extrai custom:tenantId do JWT e injeta em contexto de execução de toda Lambda. Lambda Layer consumida transversalmente.'
        technology 'AWS Lambda Layer (.NET 8)'
      }
      profileStore = component 'ProfileStore' {
        description 'Perfis estendidos (dados que não cabem em Cognito attributes).'
        technology 'DynamoDB table (tenantId, userId)'
      }

      signUpFn -> cognito 'Cria usuário no User Pool'
      signInFn -> cognito 'InitiateAuth'
      refreshTokenFn -> cognito 'RefreshToken'
      enable2faFn -> cognito 'AssociateSoftwareToken'
      verify2faFn -> cognito 'VerifySoftwareToken'
      signUpFn -> profileStore 'Grava perfil estendido'
      signInFn -> profileStore 'Lê perfil'
    }
  }

  // API Gateway routes into identity
  saferide.apiGateway -> saferide.identityAccess.signUpFn 'POST /auth/signup'
  saferide.apiGateway -> saferide.identityAccess.signInFn 'POST /auth/signin'
  saferide.apiGateway -> saferide.identityAccess.refreshTokenFn 'POST /auth/refresh'
  saferide.apiGateway -> saferide.identityAccess.enable2faFn 'POST /auth/2fa/enable'
  saferide.apiGateway -> saferide.identityAccess.verify2faFn 'POST /auth/2fa/verify'
}
```

- [ ] **Step 2: Validate**

Run:
```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/identity-access.c4
git commit -m "feat(c4): model Identity & Access context"
```

---

## Task 4: Context — Driver Onboarding

**Files:**
- Create: `c4/contexts/driver-onboarding.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    driverOnboarding = context 'Driver Onboarding' {
      description 'Cadastro rigoroso de motoristas: CNH D/E, antecedentes, vistoria, fotos, alvará, seguro.'

      submitAppFn = component 'SubmitDriverApplicationFunction' {
        description 'Motorista inicia o processo de cadastro.'
        technology 'AWS Lambda (.NET 8)'
      }
      uploadDocFn = component 'UploadDocumentFunction' {
        description 'Upload de CNH, comprovante, vistoria, fotos do veículo.'
        technology 'AWS Lambda (.NET 8)'
      }
      requestBgCheckFn = component 'RequestBackgroundCheckFunction' {
        description 'Aciona provedor externo para verificação de antecedentes.'
        technology 'AWS Lambda (.NET 8)'
      }
      validateDocFn = component 'ValidateDocumentFunction' {
        description 'OCR + regras de validação de documentos.'
        technology 'AWS Lambda (.NET 8)'
      }
      approveDriverFn = component 'ApproveDriverFunction' {
        description 'Workflow final (automatizado + revisão manual operator).'
        technology 'AWS Lambda (.NET 8)'
      }
      documentMetaStore = component 'DriverDocumentMetadata' {
        description 'Índice de documentos, status de validação, histórico.'
        technology 'PostgreSQL table (tenantId, driverId)'
      }
      documentBlobStore = component 'DriverDocumentBlobs' {
        description 'Arquivos (PDF/JPG) de documentos enviados.'
        technology 'S3 bucket'
      }

      submitAppFn -> documentMetaStore 'Cria registro de application'
      uploadDocFn -> documentBlobStore 'PutObject'
      uploadDocFn -> documentMetaStore 'Atualiza metadata'
      validateDocFn -> documentMetaStore 'Marca documento como validado/rejeitado'
      requestBgCheckFn -> backgroundCheck 'Consulta antecedentes'
      requestBgCheckFn -> documentMetaStore 'Registra resultado'
      approveDriverFn -> documentMetaStore 'Marca driver como approved'
      approveDriverFn -> eventBus 'Publish: DriverApproved'
    }
  }

  saferide.apiGateway -> saferide.driverOnboarding.submitAppFn 'POST /driver/apply'
  saferide.apiGateway -> saferide.driverOnboarding.uploadDocFn 'POST /driver/documents'
  saferide.apiGateway -> saferide.driverOnboarding.requestBgCheckFn 'POST /driver/background-check'
  saferide.apiGateway -> saferide.driverOnboarding.approveDriverFn 'POST /driver/approve (admin)'

  saferide.driverOnboarding.documentMetaStore -> saferide.postgres 'Table owner'
  saferide.driverOnboarding.documentBlobStore -> saferide.s3 'Bucket'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/driver-onboarding.c4
git commit -m "feat(c4): model Driver Onboarding context"
```

---

## Task 5: Context — Family & Children

**Files:**
- Create: `c4/contexts/family-children.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    familyChildren = context 'Family & Children' {
      description 'Cadastro de família, filhos, escola, endereço, foto facial e flag de embarque configurável.'

      registerFamilyFn = component 'RegisterFamilyFunction' {
        description 'Cria perfil de família completo.'
        technology 'AWS Lambda (.NET 8)'
      }
      addChildFn = component 'AddChildFunction' {
        description 'Adiciona filho ao perfil (nome, escola, necessidades).'
        technology 'AWS Lambda (.NET 8)'
      }
      uploadChildPhotoFn = component 'UploadChildPhotoFunction' {
        description 'Upload das 3-5 fotos faciais e geração do embedding via provedor externo.'
        technology 'AWS Lambda (.NET 8)'
      }
      configureBoardingFn = component 'ConfigureBoardingModeFunction' {
        description 'Configura flag de embarque (manual | facial | tag) por filho ou família.'
        technology 'AWS Lambda (.NET 8)'
      }
      familyStore = component 'FamilyStore' {
        description 'Entidades relacionais: família, filhos, endereço, escola.'
        technology 'PostgreSQL table (tenantId, familyId)'
      }
      embeddingStore = component 'ChildEmbeddingStore' {
        description 'Embeddings faciais cifrados (nunca fotos raw).'
        technology 'S3 bucket (AES-256 at rest)'
      }

      registerFamilyFn -> familyStore 'INSERT family'
      addChildFn -> familyStore 'INSERT child'
      uploadChildPhotoFn -> faceProvider 'Gera embedding'
      uploadChildPhotoFn -> embeddingStore 'PutObject (cifrado)'
      configureBoardingFn -> familyStore 'UPDATE boarding_mode'
    }
  }

  saferide.apiGateway -> saferide.familyChildren.registerFamilyFn 'POST /family'
  saferide.apiGateway -> saferide.familyChildren.addChildFn 'POST /family/children'
  saferide.apiGateway -> saferide.familyChildren.uploadChildPhotoFn 'POST /family/children/{id}/photo'
  saferide.apiGateway -> saferide.familyChildren.configureBoardingFn 'PUT /family/children/{id}/boarding-mode'

  saferide.familyChildren.familyStore -> saferide.postgres 'Table owner'
  saferide.familyChildren.embeddingStore -> saferide.s3 'Bucket'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/family-children.c4
git commit -m "feat(c4): model Family & Children context"
```

---

## Task 6: Context — Matching & Bidding

**Files:**
- Create: `c4/contexts/matching-bidding.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    matchingBidding = context 'Matching & Bidding' {
      description 'Núcleo do marketplace: leilão reverso por janela + renovação com preço fixo. Scoring ponderado (lance + nota + condição do veículo + histórico).'

      openBidWindowFn = component 'OpenBidWindowFunction' {
        description 'Abre leilão para um contrato-família recém-criado ou janela encerrada sem renovação.'
        technology 'AWS Lambda (.NET 8)'
      }
      notifyEligibleDriversFn = component 'NotifyEligibleDriversFunction' {
        description 'Seleciona motoristas elegíveis pela região/score e notifica.'
        technology 'AWS Lambda (.NET 8)'
      }
      submitBidFn = component 'SubmitBidFunction' {
        description 'Motorista envia lance para uma BidWindow aberta.'
        technology 'AWS Lambda (.NET 8)'
      }
      scoringEngineFn = component 'ScoringEngineFunction' {
        description 'Aplica pesos versionados (lance, nota, veículo, histórico) e ranqueia lances.'
        technology 'AWS Lambda (.NET 8)'
      }
      awardAssignmentFn = component 'AwardAssignmentFunction' {
        description 'Seleciona vencedor do leilão e emite ServiceAssignment.'
        technology 'AWS Lambda (.NET 8)'
      }
      offerRenewalFn = component 'OfferRenewalFunction' {
        description 'Propõe renovação com preço fixo ao motorista atual se satisfação é adequada.'
        technology 'AWS Lambda (.NET 8)'
      }
      acceptRenewalFn = component 'AcceptRenewalFunction' {
        description 'Motorista aceita renovação.'
        technology 'AWS Lambda (.NET 8)'
      }
      declineRenewalFn = component 'DeclineRenewalFunction' {
        description 'Motorista recusa renovação; volta para fluxo de leilão.'
        technology 'AWS Lambda (.NET 8)'
      }
      bidStore = component 'BidStore' {
        description 'BidWindows ativas e histórico de lances.'
        technology 'DynamoDB table (tenantId, bidWindowId)'
      }
      scoringRulesStore = component 'ScoringRulesStore' {
        description 'Pesos versionados do scoring (config gerenciada pelo Admin).'
        technology 'PostgreSQL table (tenantId, version)'
      }

      openBidWindowFn -> bidStore 'Cria BidWindow'
      openBidWindowFn -> eventBus 'Publish: BidWindowOpened'
      notifyEligibleDriversFn -> eventBus 'Subscribe: BidWindowOpened'
      submitBidFn -> bidStore 'Insere lance'
      submitBidFn -> eventBus 'Publish: BidSubmitted'
      scoringEngineFn -> scoringRulesStore 'Lê versão ativa'
      scoringEngineFn -> bidStore 'Lê lances'
      awardAssignmentFn -> scoringEngineFn 'Invoca'
      awardAssignmentFn -> eventBus 'Publish: ServiceAssignmentAwarded'
      offerRenewalFn -> eventBus 'Publish: RenewalOffered'
      acceptRenewalFn -> eventBus 'Publish: RenewalAccepted'
      declineRenewalFn -> eventBus 'Publish: RenewalDeclined'
    }
  }

  saferide.apiGateway -> saferide.matchingBidding.submitBidFn 'POST /bids'
  saferide.apiGateway -> saferide.matchingBidding.acceptRenewalFn 'POST /renewals/{id}/accept'
  saferide.apiGateway -> saferide.matchingBidding.declineRenewalFn 'POST /renewals/{id}/decline'

  saferide.matchingBidding.bidStore -> saferide.dynamo 'Table owner'
  saferide.matchingBidding.scoringRulesStore -> saferide.postgres 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/matching-bidding.c4
git commit -m "feat(c4): model Matching & Bidding context"
```

---

## Task 7: Context — Reputation & Rating

**Files:**
- Create: `c4/contexts/reputation-rating.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    reputationRating = context 'Reputation & Rating' {
      description 'Avaliações, inspeções de veículo, métricas de qualidade. Alimenta Matching & Bidding (scoring, gate de renovação) e Admin (dashboards).'

      submitRatingFn = component 'SubmitRatingFunction' {
        description 'Pai avalia motorista pós-trajeto ou pós-contrato.'
        technology 'AWS Lambda (.NET 8)'
      }
      recordIncidentFn = component 'RecordIncidentFunction' {
        description 'Registra incidente reportado (atraso, conduta, acidente).'
        technology 'AWS Lambda (.NET 8)'
      }
      runInspectionFn = component 'RunVehicleInspectionFunction' {
        description 'Inspeção periódica do veículo (upload de fotos, checklist).'
        technology 'AWS Lambda (.NET 8)'
      }
      computeScoreFn = component 'ComputeDriverScoreFunction' {
        description 'Agrega histórico em score consolidado consumido pelo Matching.'
        technology 'AWS Lambda (.NET 8)'
      }
      evaluateSatisfactionFn = component 'EvaluateFamilySatisfactionFunction' {
        description 'Gate da renovação: avalia satisfação agregada das famílias para decidir se oferece renovação.'
        technology 'AWS Lambda (.NET 8)'
      }
      ratingStore = component 'RatingStore' {
        description 'Ratings, inspeções, incidentes, scores computados.'
        technology 'DynamoDB table (tenantId, driverId)'
      }

      submitRatingFn -> ratingStore 'Insert rating'
      submitRatingFn -> eventBus 'Publish: RatingSubmitted'
      recordIncidentFn -> ratingStore 'Insert incident'
      recordIncidentFn -> eventBus 'Publish: IncidentRecorded'
      runInspectionFn -> ratingStore 'Insert inspection'
      runInspectionFn -> eventBus 'Publish: InspectionCompleted'
      computeScoreFn -> ratingStore 'Read + Write aggregated score'
      evaluateSatisfactionFn -> ratingStore 'Read aggregated metrics'
      evaluateSatisfactionFn -> eventBus 'Publish: SatisfactionEvaluated'
    }
  }

  saferide.apiGateway -> saferide.reputationRating.submitRatingFn 'POST /ratings'
  saferide.apiGateway -> saferide.reputationRating.recordIncidentFn 'POST /incidents'
  saferide.apiGateway -> saferide.reputationRating.runInspectionFn 'POST /inspections'

  saferide.reputationRating.ratingStore -> saferide.dynamo 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/reputation-rating.c4
git commit -m "feat(c4): model Reputation & Rating context"
```

---

## Task 8: Context — Contracts

**Files:**
- Create: `c4/contexts/contracts.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    contracts = context 'Contracts' {
      description 'Dois tipos de acordo: Family Subscription (pai ↔ plataforma, valor fixo, recorrente) e Service Assignment (plataforma ↔ motorista, janela semanal/mensal, valor variável).'

      createSubscriptionFn = component 'CreateFamilySubscriptionFunction' {
        description 'Cria contrato recorrente com o pai (mensal/semestral/anual).'
        technology 'AWS Lambda (.NET 8)'
      }
      renewSubscriptionFn = component 'RenewFamilySubscriptionFunction' {
        description 'Renova contrato do pai no ciclo.'
        technology 'AWS Lambda (.NET 8)'
      }
      cancelSubscriptionFn = component 'CancelFamilySubscriptionFunction' {
        description 'Cancela contrato do pai (com regras de reembolso pro-rata).'
        technology 'AWS Lambda (.NET 8)'
      }
      createAssignmentFn = component 'CreateServiceAssignmentFunction' {
        description 'Materializa a adjudicação do leilão ou renovação em um assignment ativo.'
        technology 'AWS Lambda (.NET 8)'
      }
      closeAssignmentFn = component 'CloseServiceAssignmentFunction' {
        description 'Fim da janela: dispara gatilho de renovação ou relicitação.'
        technology 'AWS Lambda (.NET 8)'
      }
      contractStore = component 'ContractStore' {
        description 'Tabelas relacionais: FamilySubscription, ServiceAssignment, renovações, cancelamentos.'
        technology 'PostgreSQL schema (tenantId em cada tabela)'
      }

      createSubscriptionFn -> contractStore 'INSERT subscription'
      createSubscriptionFn -> eventBus 'Publish: FamilySubscriptionCreated'
      renewSubscriptionFn -> contractStore 'INSERT renewed subscription'
      renewSubscriptionFn -> eventBus 'Publish: FamilySubscriptionRenewed'
      cancelSubscriptionFn -> contractStore 'UPDATE status=canceled'
      cancelSubscriptionFn -> eventBus 'Publish: FamilySubscriptionCanceled'
      createAssignmentFn -> contractStore 'INSERT assignment'
      createAssignmentFn -> eventBus 'Publish: ServiceAssignmentCreated'
      closeAssignmentFn -> contractStore 'UPDATE status=closed'
      closeAssignmentFn -> eventBus 'Publish: ServiceAssignmentClosed, AssignmentPeriodEnding'
    }
  }

  saferide.apiGateway -> saferide.contracts.createSubscriptionFn 'POST /subscriptions'
  saferide.apiGateway -> saferide.contracts.cancelSubscriptionFn 'DELETE /subscriptions/{id}'

  saferide.contracts.contractStore -> saferide.postgres 'Schema owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/contracts.c4
git commit -m "feat(c4): model Contracts context"
```

---

## Task 9: Context — Billing & Payments

**Files:**
- Create: `c4/contexts/billing-payments.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    billingPayments = context 'Billing & Payments' {
      description 'Cobrança recorrente dos pais, emissão de NFS-e, inadimplência, estornos.'

      chargeSubscriptionFn = component 'ChargeSubscriptionFunction' {
        description 'Agendado por CloudWatch Events: cobra ciclo via Payment Gateway.'
        technology 'AWS Lambda (.NET 8)'
      }
      paymentWebhookFn = component 'HandlePaymentWebhookFunction' {
        description 'Callback HTTPS do gateway: confirmação, falha, chargeback.'
        technology 'AWS Lambda (.NET 8)'
      }
      issueInvoiceFn = component 'IssueInvoiceFunction' {
        description 'Emite NFS-e após pagamento confirmado.'
        technology 'AWS Lambda (.NET 8)'
      }
      handleRefundFn = component 'HandleRefundFunction' {
        description 'Processa reembolsos pro-rata em cancelamento.'
        technology 'AWS Lambda (.NET 8)'
      }
      delinquencyStore = component 'DelinquencyStore' {
        description 'Cobranças em aberto, tentativas, inadimplentes.'
        technology 'PostgreSQL table'
      }

      chargeSubscriptionFn -> paymentGateway 'Create charge'
      paymentWebhookFn -> delinquencyStore 'Atualiza status'
      paymentWebhookFn -> eventBus 'Publish: SubscriptionPaid | PaymentFailed'
      issueInvoiceFn -> nfse 'Emit NFS-e'
      handleRefundFn -> paymentGateway 'Refund'
      handleRefundFn -> eventBus 'Publish: PaymentRefunded'
    }
  }

  saferide.apiGateway -> saferide.billingPayments.paymentWebhookFn 'POST /webhooks/payment (gateway callback)'
  saferide.apiGateway -> saferide.billingPayments.handleRefundFn 'POST /payments/{id}/refund (admin)'

  saferide.billingPayments.delinquencyStore -> saferide.postgres 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/billing-payments.c4
git commit -m "feat(c4): model Billing & Payments context"
```

---

## Task 10: Context — Wallet & Settlement

**Files:**
- Create: `c4/contexts/wallet-settlement.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    walletSettlement = context 'Wallet & Settlement' {
      description 'Ledger interno com três contas lógicas: Custody (saldo retido), Driver Payable (a pagar), Platform Revenue (margem reconhecida). Executa liberações periódicas via gateway.'

      recordCustodyFn = component 'RecordCustodyFunction' {
        description 'Consumer de SubscriptionPaid: registra saldo em custódia.'
        technology 'AWS Lambda (.NET 8)'
      }
      creditDriverFn = component 'CreditDriverFunction' {
        description 'Consumer de TripCompleted: move Custody → Driver Payable + Platform Revenue.'
        technology 'AWS Lambda (.NET 8)'
      }
      closeCycleFn = component 'CloseSettlementCycleFunction' {
        description 'Agendado: fecha ciclo (semanal/quinzenal) e marca saldos elegíveis para transferência.'
        technology 'AWS Lambda (.NET 8)'
      }
      executeTransferFn = component 'ExecuteDriverTransferFunction' {
        description 'Executa transfer para motorista via Payment Gateway.'
        technology 'AWS Lambda (.NET 8)'
      }
      disputeFn = component 'HandleDisputeFunction' {
        description 'Tratamento de estornos, chargebacks e retenção preventiva em disputa.'
        technology 'AWS Lambda (.NET 8)'
      }
      ledgerStore = component 'LedgerStore' {
        description 'Livro-razão com restrições transacionais fortes. Entradas imutáveis (append-only).'
        technology 'PostgreSQL schema (tenantId, ACID)'
      }
      settlementRulesStore = component 'SettlementRulesStore' {
        description 'Regras de ciclo, janela de contestação, mínimo de saque.'
        technology 'PostgreSQL table (tenantId, version)'
      }

      recordCustodyFn -> eventBus 'Subscribe: SubscriptionPaid'
      recordCustodyFn -> ledgerStore 'INSERT custody entry'
      creditDriverFn -> eventBus 'Subscribe: TripCompleted'
      creditDriverFn -> ledgerStore 'INSERT payable + revenue entries'
      closeCycleFn -> settlementRulesStore 'Lê regras'
      closeCycleFn -> ledgerStore 'Marca elegíveis'
      closeCycleFn -> eventBus 'Publish: SettlementCycleClosed'
      executeTransferFn -> paymentGateway 'Transfer'
      executeTransferFn -> ledgerStore 'Marca como paid'
      executeTransferFn -> eventBus 'Publish: DriverPaid'
      disputeFn -> eventBus 'Subscribe: PaymentRefunded'
      disputeFn -> ledgerStore 'Reverter/reter'
    }
  }

  saferide.walletSettlement.ledgerStore -> saferide.postgres 'Schema owner'
  saferide.walletSettlement.settlementRulesStore -> saferide.postgres 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/wallet-settlement.c4
git commit -m "feat(c4): model Wallet & Settlement context"
```

---

## Task 11: Context — Routing

**Files:**
- Create: `c4/contexts/routing.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    routing = context 'Routing' {
      description 'Otimização de rotas multi-aluno, geofencing, ETA, detecção de desvio.'

      planRouteFn = component 'PlanRouteFunction' {
        description 'Calcula rota otimizada entre alunos do contrato e destino (escola).'
        technology 'AWS Lambda (.NET 8)'
      }
      computeEtaFn = component 'ComputeETAFunction' {
        description 'Calcula ETA dinâmico considerando tráfego em tempo real.'
        technology 'AWS Lambda (.NET 8)'
      }
      checkGeofenceFn = component 'CheckGeofenceFunction' {
        description 'Detecta entrada/saída nos raios de 200m (casa/escola) a partir da telemetria.'
        technology 'AWS Lambda (.NET 8)'
      }
      detectDeviationFn = component 'DetectRouteDeviationFunction' {
        description 'Alerta se a van se desviar > 500m da rota planejada.'
        technology 'AWS Lambda (.NET 8)'
      }
      routeCache = component 'RouteCache' {
        description 'Cache de rotas calculadas (reduz custo Google Maps).'
        technology 'DynamoDB table com TTL'
      }

      planRouteFn -> gmaps 'Directions + Distance Matrix'
      planRouteFn -> routeCache 'Put/Get'
      planRouteFn -> eventBus 'Publish: RoutePlanned'
      computeEtaFn -> gmaps 'Directions (real-time traffic)'
      checkGeofenceFn -> eventBus 'Publish: GeofenceEntered'
      detectDeviationFn -> eventBus 'Publish: LocationDeviated'
    }
  }

  saferide.apiGateway -> saferide.routing.planRouteFn 'POST /routes/plan'
  saferide.apiGateway -> saferide.routing.computeEtaFn 'GET /routes/{id}/eta'

  saferide.routing.routeCache -> saferide.dynamo 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/routing.c4
git commit -m "feat(c4): model Routing context"
```

---

## Task 12: Context — Trip & Tracking

**Files:**
- Create: `c4/contexts/trip-tracking.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    tripTracking = context 'Trip & Tracking' {
      description 'Ciclo de vida da corrida: start → telemetria GPS → embarques (modo configurável) → chegada → conclusão.'

      startTripFn = component 'StartTripFunction' {
        description 'Motorista inicia a corrida (associada a um ServiceAssignment).'
        technology 'AWS Lambda (.NET 8)'
      }
      ingestGpsFn = component 'IngestGpsTelemetryFunction' {
        description 'Consumer do Kinesis: processa e persiste telemetria.'
        technology 'AWS Lambda (.NET 8) — Kinesis consumer'
      }
      publishPositionFn = component 'PublishPositionToSubscribersFunction' {
        description 'Empurra posição para AppSync subscribers (apps família com conexão ativa).'
        technology 'AWS Lambda (.NET 8)'
      }
      recordBoardingFn = component 'RecordBoardingFunction' {
        description 'Registra embarque (modo manual | facial | tag conforme flag da família).'
        technology 'AWS Lambda (.NET 8)'
      }
      verifyFaceFn = component 'VerifyFaceMatchFunction' {
        description 'Verificação facial quando flag = facial.'
        technology 'AWS Lambda (.NET 8)'
      }
      verifyTagFn = component 'VerifyTagReadFunction' {
        description 'Validação de leitura BLE/NFC quando flag = tag.'
        technology 'AWS Lambda (.NET 8)'
      }
      recordAlightingFn = component 'RecordAlightingFunction' {
        description 'Check-out com geolocalização. Se local diferente do cadastrado, marca localização real.'
        technology 'AWS Lambda (.NET 8)'
      }
      completeTripFn = component 'CompleteTripFunction' {
        description 'Finaliza a corrida, calcula valor da adjudicação para essa viagem, emite TripCompleted.'
        technology 'AWS Lambda (.NET 8)'
      }
      tripStore = component 'TripStore' {
        description 'Corridas ativas e histórico com composição (quais crianças).'
        technology 'DynamoDB table (tenantId, tripId)'
      }
      telemetryStore = component 'TelemetryStore' {
        description 'Telemetria GPS por corrida. TTL 90 dias.'
        technology 'DynamoDB table com TTL'
      }

      startTripFn -> tripStore 'INSERT trip'
      startTripFn -> eventBus 'Publish: TripStarted'
      ingestGpsFn -> gpsStream 'Subscribe (Kinesis consumer)'
      ingestGpsFn -> telemetryStore 'PutItem'
      ingestGpsFn -> publishPositionFn 'Invoke'
      publishPositionFn -> appsync 'Publish subscription update'
      recordBoardingFn -> tripStore 'UPDATE composition'
      recordBoardingFn -> eventBus 'Publish: BoardingRecorded'
      verifyFaceFn -> faceProvider 'Verify match'
      verifyTagFn -> tripStore 'Lê composição esperada'
      recordAlightingFn -> tripStore 'UPDATE alighting'
      recordAlightingFn -> eventBus 'Publish: AlightingRecorded | LocationDeviated'
      completeTripFn -> tripStore 'UPDATE status=completed'
      completeTripFn -> eventBus 'Publish: TripCompleted'
    }
  }

  saferide.apiGateway -> saferide.tripTracking.startTripFn 'POST /trips/start'
  saferide.apiGateway -> saferide.tripTracking.recordBoardingFn 'POST /trips/{id}/boarding'
  saferide.apiGateway -> saferide.tripTracking.recordAlightingFn 'POST /trips/{id}/alighting'
  saferide.apiGateway -> saferide.tripTracking.completeTripFn 'POST /trips/{id}/complete'

  saferide.tripTracking.tripStore -> saferide.dynamo 'Table owner'
  saferide.tripTracking.telemetryStore -> saferide.dynamo 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/trip-tracking.c4
git commit -m "feat(c4): model Trip & Tracking context"
```

---

## Task 13: Context — Notifications

**Files:**
- Create: `c4/contexts/notifications.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    notifications = context 'Notifications' {
      description 'Push, alertas contextuais, canais, preferências, histórico auditável.'

      sendPushFn = component 'SendPushNotificationFunction' {
        description 'Entrega push via FCM conforme template e preferências do destinatário.'
        technology 'AWS Lambda (.NET 8)'
      }
      templateStore = component 'NotificationTemplateStore' {
        description 'Templates versionados (pt-BR + outras futuras).'
        technology 'DynamoDB table'
      }
      preferencesStore = component 'NotificationPreferencesStore' {
        description 'Preferências por usuário (canais, horários, tipos).'
        technology 'DynamoDB table (tenantId, userId)'
      }
      auditStore = component 'NotificationAuditStore' {
        description 'Histórico de entregas para Compliance.'
        technology 'DynamoDB table com TTL longo'
      }

      sendPushFn -> eventBus 'Subscribe: múltiplos eventos de domínio'
      sendPushFn -> templateStore 'Lê template'
      sendPushFn -> preferencesStore 'Lê preferências'
      sendPushFn -> fcm 'Enviar push'
      sendPushFn -> auditStore 'Registra entrega'
    }
  }

  saferide.notifications.templateStore -> saferide.dynamo 'Table owner'
  saferide.notifications.preferencesStore -> saferide.dynamo 'Table owner'
  saferide.notifications.auditStore -> saferide.dynamo 'Table owner'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/notifications.c4
git commit -m "feat(c4): model Notifications context"
```

---

## Task 14: Context — Compliance & Audit

**Files:**
- Create: `c4/contexts/compliance-audit.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    complianceAudit = context 'Compliance & Audit' {
      description 'Trilhas de auditoria, DPIA, retenção LGPD, canal de denúncia, botão de emergência, logs invioláveis.'

      recordAuditFn = component 'RecordAuditEventFunction' {
        description 'Consumer genérico de eventos sensíveis; grava trilha append-only.'
        technology 'AWS Lambda (.NET 8)'
      }
      dsrFn = component 'HandleDataSubjectRequestFunction' {
        description 'LGPD: atendimento a solicitações de titular (acesso, retificação, exclusão).'
        technology 'AWS Lambda (.NET 8)'
      }
      retentionFn = component 'RunRetentionPolicyFunction' {
        description 'Agendado: aplica políticas de retenção e deleta dados vencidos.'
        technology 'AWS Lambda (.NET 8)'
      }
      emergencyFn = component 'RaiseEmergencyFunction' {
        description 'Botão de emergência do app família/motorista; notifica operator imediatamente.'
        technology 'AWS Lambda (.NET 8)'
      }
      auditDb = component 'AuditLogStoreDB' {
        description 'Índice de trilhas (pesquisável).'
        technology 'PostgreSQL schema'
      }
      auditArchive = component 'AuditArchive' {
        description 'Arquivamento imutável (S3 Object Lock em modo compliance).'
        technology 'S3 bucket com Object Lock'
      }

      recordAuditFn -> eventBus 'Subscribe: * (eventos sensíveis)'
      recordAuditFn -> auditDb 'INSERT audit entry'
      recordAuditFn -> auditArchive 'PutObject (append-only)'
      dsrFn -> auditDb 'Consulta'
      retentionFn -> auditDb 'Deleta vencidos'
      emergencyFn -> eventBus 'Publish: EmergencyRaised'
    }
  }

  saferide.apiGateway -> saferide.complianceAudit.dsrFn 'POST /dsr/{type}'
  saferide.apiGateway -> saferide.complianceAudit.emergencyFn 'POST /emergency'

  saferide.complianceAudit.auditDb -> saferide.postgres 'Schema owner'
  saferide.complianceAudit.auditArchive -> saferide.s3 'Bucket'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/compliance-audit.c4
git commit -m "feat(c4): model Compliance & Audit context"
```

---

## Task 15: Context — Admin / Back-office

**Files:**
- Create: `c4/contexts/admin-backoffice.c4`

- [ ] **Step 1: Write the context file**

```
model {
  extend saferide {
    adminBackoffice = context 'Admin / Back-office' {
      description 'APIs do Painel Admin: operações, relatórios, regras configuráveis, mediação de disputas.'

      manageDriversFn = component 'ManageDriversFunction' {
        description 'CRUD + aprovação/suspensão de motoristas.'
        technology 'AWS Lambda (.NET 8)'
      }
      manageFamiliesFn = component 'ManageFamiliesFunction' {
        description 'Busca e suporte a famílias.'
        technology 'AWS Lambda (.NET 8)'
      }
      viewLedgerFn = component 'ViewLedgerFunction' {
        description 'Dashboard financeiro: custody, payable, revenue por tenant.'
        technology 'AWS Lambda (.NET 8)'
      }
      viewReportsFn = component 'ViewReportsFunction' {
        description 'Relatórios: ocupação, rentabilidade por rota/motorista, SLAs.'
        technology 'AWS Lambda (.NET 8)'
      }
      manageScoringRulesFn = component 'ManageScoringRulesFunction' {
        description 'Versiona pesos do scoring do Matching & Bidding.'
        technology 'AWS Lambda (.NET 8)'
      }
      manageSettlementRulesFn = component 'ManageSettlementRulesFunction' {
        description 'Versiona regras de ciclo do Wallet & Settlement.'
        technology 'AWS Lambda (.NET 8)'
      }
      disputesFn = component 'DisputesFunction' {
        description 'Mediação de disputas pais ↔ motoristas.'
        technology 'AWS Lambda (.NET 8)'
      }

      manageScoringRulesFn -> saferide.matchingBidding.scoringRulesStore 'Atualiza versão'
      manageSettlementRulesFn -> saferide.walletSettlement.settlementRulesStore 'Atualiza versão'
    }
  }

  saferide.apiGateway -> saferide.adminBackoffice.manageDriversFn 'GET/PUT /admin/drivers'
  saferide.apiGateway -> saferide.adminBackoffice.manageFamiliesFn 'GET /admin/families'
  saferide.apiGateway -> saferide.adminBackoffice.viewLedgerFn 'GET /admin/ledger'
  saferide.apiGateway -> saferide.adminBackoffice.viewReportsFn 'GET /admin/reports'
  saferide.apiGateway -> saferide.adminBackoffice.manageScoringRulesFn 'PUT /admin/scoring-rules'
  saferide.apiGateway -> saferide.adminBackoffice.manageSettlementRulesFn 'PUT /admin/settlement-rules'
  saferide.apiGateway -> saferide.adminBackoffice.disputesFn 'POST /admin/disputes'
}
```

- [ ] **Step 2: Validate**

```bash
cd c4
npx likec4 validate
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/contexts/admin-backoffice.c4
git commit -m "feat(c4): model Admin / Back-office context"
```

---

## Task 16: System Context View (Landscape)

**Files:**
- Create: `c4/views/system-views.c4`

- [ ] **Step 1: Write the view file**

```
views {
  view landscape {
    title 'SafeRide Kids — Landscape (System Context)'
    description 'Visão nível 1 do C4: a plataforma como caixa preta com atores e sistemas externos.'

    include *

    exclude
      saferide.familyApp,
      saferide.driverApp,
      saferide.adminWeb,
      saferide.apiGateway,
      saferide.appsync,
      saferide.eventBus,
      saferide.gpsStream,
      saferide.postgres,
      saferide.dynamo,
      saferide.s3,
      saferide.cognito,
      saferide.observability

    autoLayout LeftRight
  }
}
```

- [ ] **Step 2: Validate + render**

```bash
cd c4
npx likec4 validate
npx likec4 build -o dist
```

Expected: no errors. `dist/index.html` generated. If `autoLayout LeftRight` is rejected, try `TopBottom` or remove the directive.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/views/system-views.c4
git commit -m "feat(c4): add landscape view (level 1)"
```

---

## Task 17: Container View

**Files:**
- Modify: `c4/views/system-views.c4`

- [ ] **Step 1: Append the container view**

Add to `c4/views/system-views.c4`:

```
views {
  view containers of saferide {
    title 'SafeRide Kids — Containers (Level 2)'
    description 'Visão nível 2: apps, APIs, canal real-time, eventos, dados, observabilidade, e os 13 bounded contexts agrupados.'

    include *

    autoLayout TopBottom
  }
}
```

- [ ] **Step 2: Validate + render**

```bash
cd c4
npx likec4 validate
npx likec4 build -o dist
```

Expected: no errors.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/views/system-views.c4
git commit -m "feat(c4): add container view (level 2)"
```

---

## Task 18: Component Views (one per context)

**Files:**
- Create: `c4/views/component-views.c4`

- [ ] **Step 1: Write the file**

```
views {
  view identityAccess of saferide.identityAccess {
    title 'Identity & Access — Components'
    include *
    include saferide.apiGateway, saferide.cognito with { color: muted }
    autoLayout TopBottom
  }

  view driverOnboarding of saferide.driverOnboarding {
    title 'Driver Onboarding — Components'
    include *
    include saferide.apiGateway, saferide.postgres, saferide.s3, backgroundCheck, saferide.eventBus with { color: muted }
    autoLayout TopBottom
  }

  view familyChildren of saferide.familyChildren {
    title 'Family & Children — Components'
    include *
    include saferide.apiGateway, saferide.postgres, saferide.s3, faceProvider with { color: muted }
    autoLayout TopBottom
  }

  view matchingBidding of saferide.matchingBidding {
    title 'Matching & Bidding — Components'
    include *
    include saferide.apiGateway, saferide.dynamo, saferide.postgres, saferide.eventBus with { color: muted }
    autoLayout TopBottom
  }

  view reputationRating of saferide.reputationRating {
    title 'Reputation & Rating — Components'
    include *
    include saferide.apiGateway, saferide.dynamo, saferide.eventBus with { color: muted }
    autoLayout TopBottom
  }

  view contracts of saferide.contracts {
    title 'Contracts — Components'
    include *
    include saferide.apiGateway, saferide.postgres, saferide.eventBus with { color: muted }
    autoLayout TopBottom
  }

  view billingPayments of saferide.billingPayments {
    title 'Billing & Payments — Components'
    include *
    include saferide.apiGateway, saferide.postgres, saferide.eventBus, paymentGateway, nfse with { color: muted }
    autoLayout TopBottom
  }

  view walletSettlement of saferide.walletSettlement {
    title 'Wallet & Settlement — Components'
    include *
    include saferide.postgres, saferide.eventBus, paymentGateway with { color: muted }
    autoLayout TopBottom
  }

  view routing of saferide.routing {
    title 'Routing — Components'
    include *
    include saferide.apiGateway, saferide.dynamo, saferide.eventBus, gmaps with { color: muted }
    autoLayout TopBottom
  }

  view tripTracking of saferide.tripTracking {
    title 'Trip & Tracking — Components'
    include *
    include saferide.apiGateway, saferide.appsync, saferide.gpsStream, saferide.dynamo, saferide.eventBus, faceProvider with { color: muted }
    autoLayout TopBottom
  }

  view notifications of saferide.notifications {
    title 'Notifications — Components'
    include *
    include saferide.dynamo, saferide.eventBus, fcm with { color: muted }
    autoLayout TopBottom
  }

  view complianceAudit of saferide.complianceAudit {
    title 'Compliance & Audit — Components'
    include *
    include saferide.apiGateway, saferide.postgres, saferide.s3, saferide.eventBus with { color: muted }
    autoLayout TopBottom
  }

  view adminBackoffice of saferide.adminBackoffice {
    title 'Admin / Back-office — Components'
    include *
    include saferide.apiGateway, saferide.adminWeb with { color: muted }
    autoLayout TopBottom
  }
}
```

- [ ] **Step 2: Validate + render**

```bash
cd c4
npx likec4 validate
npx likec4 build -o dist
```

Expected: no errors. If the `with { color: muted }` modifier isn't supported in the Likec4 version installed, remove it from all views — the diagrams still render correctly without the muting.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/views/component-views.c4
git commit -m "feat(c4): add component views for all 13 bounded contexts"
```

---

## Task 19: Dynamic Views (Bidding Flow, Trip Lifecycle, Payment & Settlement Flow)

**Files:**
- Create: `c4/views/dynamic-views.c4`

- [ ] **Step 1: Write the file**

```
views {
  dynamic view biddingFlow {
    title 'Dynamic — Bidding & Renewal Flow'
    description 'Do gatilho do leilão até a adjudicação/renovação.'

    saferide.contracts.createSubscriptionFn -> saferide.eventBus 'Publish: FamilySubscriptionCreated'
    saferide.eventBus -> saferide.matchingBidding.openBidWindowFn 'Deliver event'
    saferide.matchingBidding.openBidWindowFn -> saferide.matchingBidding.bidStore 'Create BidWindow'
    saferide.matchingBidding.openBidWindowFn -> saferide.eventBus 'Publish: BidWindowOpened'
    saferide.eventBus -> saferide.matchingBidding.notifyEligibleDriversFn 'Deliver event'
    saferide.matchingBidding.notifyEligibleDriversFn -> saferide.notifications.sendPushFn 'Request push'
    saferide.notifications.sendPushFn -> fcm 'Deliver push to drivers'
    fcm -> saferide.driverApp 'Push notification'
    saferide.driverApp -> saferide.apiGateway 'POST /bids'
    saferide.apiGateway -> saferide.matchingBidding.submitBidFn 'Invoke'
    saferide.matchingBidding.submitBidFn -> saferide.matchingBidding.bidStore 'Insert bid'
    saferide.matchingBidding.submitBidFn -> saferide.eventBus 'Publish: BidSubmitted'

    saferide.matchingBidding.awardAssignmentFn -> saferide.matchingBidding.scoringEngineFn 'Score bids'
    saferide.matchingBidding.scoringEngineFn -> saferide.matchingBidding.scoringRulesStore 'Read active weights'
    saferide.matchingBidding.scoringEngineFn -> saferide.matchingBidding.bidStore 'Read bids'
    saferide.matchingBidding.awardAssignmentFn -> saferide.eventBus 'Publish: ServiceAssignmentAwarded'
    saferide.eventBus -> saferide.contracts.createAssignmentFn 'Deliver event'
    saferide.contracts.createAssignmentFn -> saferide.contracts.contractStore 'Insert assignment'
    saferide.contracts.createAssignmentFn -> saferide.eventBus 'Publish: ServiceAssignmentCreated'

    // Renewal path
    saferide.contracts.closeAssignmentFn -> saferide.eventBus 'Publish: AssignmentPeriodEnding'
    saferide.eventBus -> saferide.reputationRating.evaluateSatisfactionFn 'Deliver event'
    saferide.reputationRating.evaluateSatisfactionFn -> saferide.eventBus 'Publish: SatisfactionEvaluated (ok)'
    saferide.eventBus -> saferide.matchingBidding.offerRenewalFn 'Deliver event'
    saferide.matchingBidding.offerRenewalFn -> saferide.eventBus 'Publish: RenewalOffered'
    saferide.driverApp -> saferide.apiGateway 'POST /renewals/{id}/accept'
    saferide.apiGateway -> saferide.matchingBidding.acceptRenewalFn 'Invoke'
    saferide.matchingBidding.acceptRenewalFn -> saferide.eventBus 'Publish: RenewalAccepted'
  }

  dynamic view tripLifecycle {
    title 'Dynamic — Trip Lifecycle'
    description 'Do planejamento à conclusão da viagem.'

    saferide.eventBus -> saferide.routing.planRouteFn 'Deliver: ServiceAssignmentCreated'
    saferide.routing.planRouteFn -> gmaps 'Directions + Distance Matrix'
    saferide.routing.planRouteFn -> saferide.routing.routeCache 'Put'
    saferide.routing.planRouteFn -> saferide.eventBus 'Publish: RoutePlanned'

    saferide.driverApp -> saferide.apiGateway 'POST /trips/start'
    saferide.apiGateway -> saferide.tripTracking.startTripFn 'Invoke'
    saferide.tripTracking.startTripFn -> saferide.tripTracking.tripStore 'Insert'
    saferide.tripTracking.startTripFn -> saferide.eventBus 'Publish: TripStarted'

    saferide.driverApp -> saferide.gpsStream 'Publish GPS every 5-10s'
    saferide.gpsStream -> saferide.tripTracking.ingestGpsFn 'Consume'
    saferide.tripTracking.ingestGpsFn -> saferide.tripTracking.telemetryStore 'PutItem'
    saferide.tripTracking.ingestGpsFn -> saferide.tripTracking.publishPositionFn 'Invoke'
    saferide.tripTracking.publishPositionFn -> saferide.appsync 'Publish subscription'
    saferide.appsync -> saferide.familyApp 'Live position'
    saferide.routing.checkGeofenceFn -> saferide.eventBus 'Publish: GeofenceEntered'

    saferide.driverApp -> saferide.apiGateway 'POST /trips/{id}/boarding'
    saferide.apiGateway -> saferide.tripTracking.recordBoardingFn 'Invoke'
    saferide.tripTracking.recordBoardingFn -> saferide.tripTracking.verifyFaceFn 'If flag=facial'
    saferide.tripTracking.verifyFaceFn -> faceProvider 'Verify match'
    saferide.tripTracking.recordBoardingFn -> saferide.eventBus 'Publish: BoardingRecorded'
    saferide.eventBus -> saferide.notifications.sendPushFn 'Notify parent'

    saferide.driverApp -> saferide.apiGateway 'POST /trips/{id}/alighting'
    saferide.apiGateway -> saferide.tripTracking.recordAlightingFn 'Invoke'
    saferide.tripTracking.recordAlightingFn -> saferide.eventBus 'Publish: AlightingRecorded'

    saferide.driverApp -> saferide.apiGateway 'POST /trips/{id}/complete'
    saferide.apiGateway -> saferide.tripTracking.completeTripFn 'Invoke'
    saferide.tripTracking.completeTripFn -> saferide.tripTracking.tripStore 'UPDATE status=completed'
    saferide.tripTracking.completeTripFn -> saferide.eventBus 'Publish: TripCompleted(awardValue)'
  }

  dynamic view paymentSettlementFlow {
    title 'Dynamic — Payment & Settlement Flow'
    description 'Do pagamento do pai à liberação para o motorista.'

    saferide.billingPayments.chargeSubscriptionFn -> paymentGateway 'Create charge'
    paymentGateway -> saferide.apiGateway 'POST /webhooks/payment'
    saferide.apiGateway -> saferide.billingPayments.paymentWebhookFn 'Invoke'
    saferide.billingPayments.paymentWebhookFn -> saferide.eventBus 'Publish: SubscriptionPaid'
    saferide.eventBus -> saferide.walletSettlement.recordCustodyFn 'Deliver event'
    saferide.walletSettlement.recordCustodyFn -> saferide.walletSettlement.ledgerStore 'INSERT custody'

    saferide.eventBus -> saferide.walletSettlement.creditDriverFn 'Deliver: TripCompleted'
    saferide.walletSettlement.creditDriverFn -> saferide.walletSettlement.ledgerStore 'Move custody → payable + revenue'

    saferide.walletSettlement.closeCycleFn -> saferide.walletSettlement.settlementRulesStore 'Read rules'
    saferide.walletSettlement.closeCycleFn -> saferide.walletSettlement.ledgerStore 'Mark eligible'
    saferide.walletSettlement.closeCycleFn -> saferide.eventBus 'Publish: SettlementCycleClosed'
    saferide.eventBus -> saferide.walletSettlement.executeTransferFn 'Deliver event'
    saferide.walletSettlement.executeTransferFn -> paymentGateway 'Transfer to driver'
    saferide.walletSettlement.executeTransferFn -> saferide.walletSettlement.ledgerStore 'Mark paid'
    saferide.walletSettlement.executeTransferFn -> saferide.eventBus 'Publish: DriverPaid'
    saferide.eventBus -> saferide.notifications.sendPushFn 'Notify driver'
  }
}
```

- [ ] **Step 2: Validate + render**

```bash
cd c4
npx likec4 validate
npx likec4 build -o dist
```

Expected: no errors. If `dynamic view` syntax differs (older versions used sequential step notation), consult https://likec4.dev/dsl/dynamic-views/ and adapt — the sequential order of the relationships in the block is what defines the sequence in Likec4.

- [ ] **Step 3: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add c4/views/dynamic-views.c4
git commit -m "feat(c4): add dynamic views for bidding, trip lifecycle and payment settlement"
```

---

## Task 20: Architecture Decision Records (ADRs)

**Files:**
- Create: `docs/adrs/ADR-001-payment-gateway.md`
- Create: `docs/adrs/ADR-002-face-recognition.md`
- Create: `docs/adrs/ADR-003-background-check.md`
- Create: `docs/adrs/ADR-004-nfse-provider.md`

- [ ] **Step 1: ADR-001 Payment Gateway**

Create `docs/adrs/ADR-001-payment-gateway.md`:

```markdown
# ADR-001 — Payment Gateway

- **Status:** Proposed
- **Date:** 2026-04-15
- **Deciders:** Stakeholders SafeRide Kids

## Context

A plataforma recebe mensalidades antecipadas dos pais (ex: R$ 1.000/mês), retém em custódia via gateway regulado (não em conta bancária comum — evita caracterizar arranjo de pagamento BCB direto), e libera ao motorista o valor negociado por viagem executada. A diferença é receita da plataforma.

O provedor de pagamento precisa oferecer:
- Cobrança recorrente de cartão / Pix / boleto
- Split / custódia programática
- Transfer para contas de terceiros (motoristas)
- Webhooks confiáveis
- Suporte a NFS-e (nativo ou via parceiro)
- Aderência regulatória brasileira

## Candidatos

| Candidato | Pontos fortes | Pontos de atenção |
|---|---|---|
| **PagBank** (favorito) | Capilaridade BR, suporte local, split nativo | Documentação menos rica que competidores globais |
| Asaas | NFS-e integrado, split + retenção, forte em SMB BR | Menor escala histórica |
| Stripe Connect | Documentação excelente, APIs robustas | NFS-e via parceiro; operação BR mais recente |
| Iugu | Forte em assinaturas recorrentes | Pivot de estratégia nos últimos anos |

## Decisão

_Pendente._ **PagBank é o candidato favorito** pela orientação de priorizar opção nacional. Decisão formal requer:
1. POC de cobrança + split + transfer
2. Análise de taxas para o volume esperado
3. Due diligence de SLA e estabilidade de webhooks
4. Revisão jurídica da documentação do arranjo

## Consequências

Independente do gateway escolhido, o contexto **Wallet & Settlement** continua dono do ledger interno. O gateway é a instituição regulada que efetivamente mantém o dinheiro em conta-cliente. Trocar o gateway depois envolve refazer integrações de webhook e transfer, sem alterar a estrutura do ledger.
```

- [ ] **Step 2: ADR-002 Face Recognition**

Create `docs/adrs/ADR-002-face-recognition.md`:

```markdown
# ADR-002 — Face Recognition Provider

- **Status:** Proposed
- **Date:** 2026-04-15
- **Deciders:** Stakeholders SafeRide Kids

## Context

Reconhecimento facial é **opcional por família** (flag configurável). Quando habilitado:
- 3-5 fotos da criança geram um embedding facial (hash numérico, não a foto raw)
- No embarque, o app motorista captura frame e compara embedding em tempo real
- Match > 95% confirma identidade; fallback por código numérico em caso de falha
- Recadastro a cada 6 meses (crianças mudam rápido)

## Candidatos

| Candidato | Pontos fortes | Pontos de atenção |
|---|---|---|
| **AWS Rekognition** | Integração nativa com o stack AWS, bom custo | Viés com menores e tons de pele escuros — testar |
| Azure Face API | Alta precisão, suporte enterprise | Conta Azure adicional para uso pontual |

## Decisão

_Pendente._ Para preservar coerência com o stack AWS, **AWS Rekognition** é o favorito natural. Decisão requer:
1. Teste extensivo com amostras representativas (crianças de 5-15 anos, tons de pele variados)
2. Avaliação de custo para volume esperado (~30k verificações/mês no MVP)
3. Revisão de conformidade LGPD (biometria é dado sensível Art. 11)

## Consequências

Qualquer que seja o provedor, armazenamos **apenas embeddings cifrados** (S3 AES-256), **nunca fotos raw** no servidor. Acesso aos embeddings é auditado via contexto Compliance & Audit. Trocar provedor exige regerar embeddings — impacto em todas as famílias com flag facial ativa.
```

- [ ] **Step 3: ADR-003 Background Check**

Create `docs/adrs/ADR-003-background-check.md`:

```markdown
# ADR-003 — Background Check Provider

- **Status:** Proposed
- **Date:** 2026-04-15
- **Deciders:** Stakeholders SafeRide Kids

## Context

Motoristas precisam passar por verificação rigorosa de antecedentes como parte do Driver Onboarding. Requisitos:
- Consulta a antecedentes criminais
- Validação de CNH (categoria D/E, pontuação)
- Opcional: exame toxicológico, curso de transporte escolar, restrições profissionais

## Candidatos

| Candidato | Pontos fortes | Pontos de atenção |
|---|---|---|
| **IDwall** | API robusta, foco BR, muitas fontes | Custo por consulta |
| Truora | Latam-wide, preço competitivo | Cobertura BR em evolução |
| Serasa Experian | Base nacional forte, crédito + antecedentes | Integração B2B mais pesada |

## Decisão

_Pendente._ Decisão requer:
1. Mapa de fontes consultadas (Detran, Polícia, TJ)
2. SLA de resposta (síncrona vs. assíncrona)
3. Custo por consulta para volume MVP

## Consequências

O contexto **Driver Onboarding** depende da API do provedor escolhido. Resultado da verificação é persistido em `DriverDocumentMetadata` (PostgreSQL). Caso do motorista com antecedentes desqualificantes, o workflow rejeita automaticamente.
```

- [ ] **Step 4: ADR-004 NFS-e Provider**

Create `docs/adrs/ADR-004-nfse-provider.md`:

```markdown
# ADR-004 — NFS-e Provider

- **Status:** Proposed
- **Date:** 2026-04-15
- **Deciders:** Stakeholders SafeRide Kids

## Context

Emissão obrigatória de NFS-e para cada mensalidade paga, conforme legislação municipal. Complexidade: cada município tem padrão próprio de integração com prefeitura.

## Candidatos

| Candidato | Pontos fortes | Pontos de atenção |
|---|---|---|
| **NFe.io** | Cobertura ampla de municípios, API simples | Custo por nota |
| Focus NFe | Alta performance, cobertura sólida | Documentação menos rica |
| Asaas (embutido) | Se adotarmos Asaas como gateway (ADR-001), NFS-e é nativo | Acopla decisão do gateway |

## Decisão

_Pendente._ Depende parcialmente da ADR-001. Se Payment Gateway = **Asaas**, usar o NFS-e nativo simplifica. Se for outro gateway, **NFe.io** é o favorito.

## Consequências

O contexto **Billing & Payments** emite NFS-e via `IssueInvoiceFunction`. Se houver mudança de provedor, refazer mapeamento de municípios atendidos.
```

- [ ] **Step 5: Validate ADRs render as markdown**

Open any one of the files in a markdown viewer (VS Code preview is fine) and confirm headings, tables and code blocks render correctly.

- [ ] **Step 6: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add docs/adrs/
git commit -m "docs(adrs): add 4 ADRs for pending provider decisions"
```

---

## Task 21: Build HTML Static SPA

**Files:**
- Generated: `c4/dist/`

- [ ] **Step 1: Full build**

```bash
cd c4
npx likec4 build -o dist
```

Expected: `c4/dist/index.html` + assets. No validation errors. If errors surface, resolve before continuing.

- [ ] **Step 2: Smoke-test the build**

Open `c4/dist/index.html` in a browser (Windows: double-click or `start dist/index.html` from PowerShell; from Git Bash: `explorer dist/index.html`).

Verify:
- Landscape view renders (actors + external systems + platform)
- Container view renders (all contexts + infra containers)
- All 13 component views navigable via left panel
- All 3 dynamic views navigable, with sequential numbered steps
- Clicking an external system marked `decision-pending` opens the ADR link (relative `../docs/adrs/ADR-00X-*.md` — verify the link works; may need to adjust to absolute relative from `dist/`)

If any view is missing or fails to render, inspect console for Likec4 errors and re-run `build`.

- [ ] **Step 3: Optional — run dev server for exploration**

```bash
cd c4
npx likec4 start
```

Opens an interactive dev server (default http://localhost:5173). Navigate end-to-end once to ensure parity with the static build.

- [ ] **Step 4: No commit needed**

`dist/` is gitignored — it's a build artifact, regenerated any time from source.

---

## Task 22: README

**Files:**
- Create: `README.md` (repository root)

- [ ] **Step 1: Write the README**

```markdown
# SafeRide Kids

Plataforma serverless multi-tenant que conecta famílias a motoristas de transporte escolar, com rastreamento em tempo real, validação de embarque configurável e intermediação financeira com custódia regulamentada.

**Status:** concepção arquitetural (2026-04).

## Documentação

- **Documento original dos stakeholders:** `SafeRide_Kids_Arquitetura.pptx`
- **Design spec (C4):** `docs/superpowers/specs/2026-04-15-saferidekids-c4-design.md`
- **Plano de implementação do C4:** `docs/superpowers/plans/2026-04-15-saferidekids-c4-implementation.md`
- **ADRs:** `docs/adrs/` — decisões técnicas pendentes com candidatos avaliados

## Modelo C4 Navegável

O C4 está em `c4/`, escrito em Likec4. Para gerar e visualizar:

### Pré-requisitos
- Node.js 20+
- npm

### Gerar SPA estática para apresentar aos stakeholders
```bash
cd c4
npm install
npm run build
```

O resultado fica em `c4/dist/`. Basta abrir `c4/dist/index.html` em qualquer navegador — **sem servidor, sem internet, sem dependências**. Ideal para apresentar em qualquer laptop (pendrive, email, pasta compartilhada).

### Modo dev (navegação interativa com hot-reload)
```bash
cd c4
npm start
```

Abre servidor local (padrão http://localhost:5173).

### Exportar imagens
```bash
cd c4
npm run export:png
npm run export:svg
```

### Estrutura do modelo

- `c4/workspace.c4` — specification, atores, sistemas externos, plataforma
- `c4/contexts/*.c4` — 13 bounded contexts (um por arquivo)
- `c4/views/*.c4` — views organizadas por nível (system context, container, component, dynamic)

### Níveis do modelo

- **Level 1 — Landscape:** plataforma como caixa preta + atores + sistemas externos
- **Level 2 — Containers:** apps, APIs, infra, e os 13 contextos
- **Level 3 — Components:** funções Lambda por endpoint agrupadas por contexto
- **Dynamic Views:** Bidding Flow, Trip Lifecycle, Payment & Settlement Flow

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

Ver `docs/adrs/` para contexto e candidatos:
- ADR-001: Payment Gateway (favorito: PagBank)
- ADR-002: Face Recognition Provider
- ADR-003: Background Check Provider
- ADR-004: NFS-e Provider
```

- [ ] **Step 2: Commit**

```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git add README.md
git commit -m "docs: add README covering project, C4 workspace and decisions"
```

---

## Final Verification

- [ ] Full rebuild from clean
```bash
cd c4
rm -rf dist node_modules
npm install
npm run build
```

- [ ] Open `c4/dist/index.html` and navigate end-to-end:
  - Landscape (level 1)
  - Containers (level 2)
  - All 13 component views (level 3)
  - All 3 dynamic views
  - Click a `decision-pending` external system → link opens the ADR

- [ ] Check git log
```bash
cd N:/Users/tinch/Documents/Projetos/saferidekids/
git log --oneline
```

Expected: 20+ incremental commits since `304e9d6` (Initial version).

---

## Notes for the Executor

- **Likec4 DSL references:** If any syntax in this plan fails to compile on the installed Likec4 version, consult https://likec4.dev/dsl/ — the DSL evolves. The plan uses conservative syntax (elements, relationships, nested model blocks, `include *`, `autoLayout`, `dynamic view`) that has been stable across recent versions. Feature flags like `with { color: muted }` may need to be removed if unsupported — diagrams still render without them.
- **Multi-file model merging:** Likec4 auto-discovers all `.c4` files in the workspace root and subdirectories. No explicit `import` needed. The `extend` keyword attaches elements to an already-declared parent defined in another file.
- **Commits are intentionally incremental** — one per task. Easier to review, easier to roll back a specific piece, and each commit is a working build.
- **Dev server is strictly optional.** The static build is the deliverable. Use `npm start` only for your own exploration while authoring new contexts.
