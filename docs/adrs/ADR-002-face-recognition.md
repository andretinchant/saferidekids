# ADR-002 — Face Recognition Provider

- **Status:** In POC
- **Date:** 2026-05-17 (atualização — versão original 2026-04-15)
- **Deciders:** Stakeholders SafeRide Kids
- **POC:** Plano completo em [`docs/poc/CONTRACTS.md`](../poc/CONTRACTS.md) · matriz comparativa em [`docs/poc/matriz-fornecedores.md`](../poc/matriz-fornecedores.md) · plano de teste em [`docs/poc/plano-de-teste.md`](../poc/plano-de-teste.md) · RIPD/DPIA em [`docs/poc/lgpd-rip-dpia.md`](../poc/lgpd-rip-dpia.md)

## Context

Reconhecimento facial é **opcional por família** (flag configurável). Quando habilitado:
- 3-5 fotos da criança geram um embedding facial (hash numérico, não a foto raw)
- No embarque, o app motorista captura frame e compara embedding em tempo real
- Match acima de threshold configurável confirma identidade; fallback por código numérico (PIN) em caso de falha
- Recadastro a cada 6 meses (crianças mudam rápido)

## Pontos não-negociáveis (`CONTRACTS.md` §1)

1. **Verificação 1:1, nunca 1:N.** O check-in confirma se a criança apresentada é a esperada para a parada — não identifica em base de pessoas.
2. **Biometria nunca é fator único.** Sempre existe fallback (PIN do responsável, lista autorizada, confirmação manual auditada).
3. **Fotos raw nunca persistem.** S3 só armazena template/embedding criptografado. Se um provedor exigir imagem temporária, TTL ≤ 24h + KMS + bucket isolado.
4. **Consentimento expresso e destacado** do responsável legal — texto canônico em [`docs/poc/lgpd-consentimento.md`](../poc/lgpd-consentimento.md).
5. **Retenção máxima de 6 meses** para o enrollment (recadastro obrigatório); 24h para fotos temporárias no provedor.
6. **Crianças apenas em Fase B**, condicionada a RIPD/DPIA aprovado e parecer jurídico externo. POC (Fase A) restrita a adultos voluntários.

## Candidatos

| Candidato | Status na POC | Pontos fortes | Pontos de atenção |
|---|---|---|---|
| **AWS Rekognition** (Face Liveness + CompareFaces) | **Funcional (provider concreto)** | Integração nativa com o stack AWS, bom custo (~$0,0035/verificação), disponível em `sa-east-1`, PAD L2 iBeta, IAM granular permite proibir `IndexFaces`/`SearchFaces` (1:N) | Viés com menores e tons de pele escuros — medir; Amplify Liveness SDK não tem binding MAUI oficial |
| **Unico IDCloud** | **Funcional (provider concreto)** | Base de treinamento brasileira, liveness ativo+passivo, maturidade jurídica LGPD em PT-BR, datacenters no Brasil | Sem NuGet/MAUI oficial; integração via REST + custom handler nativo; pricing por contrato |
| **Azure AI Face / Face Liveness** | **Stub documental** (`SafeRideKids.Biometria.Providers.Stubs/`) | NIST top-5, robustez enterprise | Em "Limited Access" desde 2024 — exige formulário e aprovação MS (4-8 semanas); abrir tenant Azure só para isso vai contra "stack único AWS" |
| **FaceTec ZoOm 3D Liveness** | **Stub documental** | Melhor anti-spoofing 3D (máscaras), PAD L2 robusto | Sem SDK MAUI/.NET nativo; pricing enterprise sem tabela pública; over-engineered para o caso |
| **Serpro Datavalid** | **Stub documental** | Estatal (CNH/RG), data residency BR | Foco em CPF-vs-RG/CNH; crianças não têm base biométrica estatal. Faz sentido para ADR-003 (Driver Onboarding), não aqui |
| **Google Cloud Vision** | **Descartado, não vira stub** | n/a para este caso de uso | API faz detecção facial / atributos, **não realiza verificação 1:1 de identidade**; Google retirou esse recurso comercialmente em 2018. Vide [matriz](../poc/matriz-fornecedores.md) §4 |

## Decisão

_Pendente._ Os 2 provedores funcionais (AWS Rekognition e Unico IDCloud) serão avaliados na Fase A da POC com adultos voluntários, em paralelo — backend implementa `IFaceVerificationProvider` para ambos e o `IProviderRouter` distribui 50/50 (CONTRACTS §3-4).

Critérios para o veredito final (depois da Fase A):

1. **Métricas técnicas:** FAR, FRR, taxa de inconclusive, latência, comportamento em redes lentas, anti-spoofing simples — definidas em `plano-de-teste.md`.
2. **Aceitação subjetiva:** Likert ≥ 4 nos voluntários.
3. **Custo** por verificação dentro do orçamento (~$0,003 AWS / ~R$ 0,50 Unico).
4. **Adequação LGPD:** DPA, lista de suboperadores, data residency, DSR operacional.
5. **Esforço de integração** no MAUI (custom handler) e no backend Lambda.

Resultado esperado:
- Cenário A — um vence claramente: promover a único funcional, manter outro como stub para diversidade.
- Cenário B — empate técnico: manter os dois ativos com router 50/50 e usar `family.preferred_provider_id` para casos extremos.
- Cenário C — ambos falham nos critérios mínimos: reabrir o ADR considerando Azure (se Limited Access viabilizado) ou FaceTec.

A **Fase B (crianças)** só inicia depois de:
- RIPD/DPIA assinado por DPO + parecer jurídico externo (`docs/poc/lgpd-rip-dpia.md` §7).
- Consentimento expresso dos responsáveis legais coletado conforme `lgpd-consentimento.md`.
- Métricas da Fase A demonstrando viabilidade técnica e ausência de viés sistêmico inaceitável.

## Consequências

Qualquer que seja o provedor, armazenamos **apenas embeddings cifrados** (S3 AES-256, na POC com CMK dedicada via KMS), **nunca fotos raw** no servidor. Acesso aos embeddings é auditado via contexto Compliance & Audit (`audit_log`). Trocar provedor exige regerar embeddings — impacto em todas as famílias com flag facial ativa.

A interface canônica `IFaceVerificationProvider` (CONTRACTS §3) isola o backend dos detalhes do provedor — trocar ou adicionar é uma classe nova, sem mexer no domínio. O Approach C (roteamento server-side, CONTRACTS §4) permite migração gradual com hash determinístico por `childId+date`, mantendo a mesma criança no mesmo provedor durante o dia.

A retenção de 6 meses, a cifragem em repouso com CMK dedicada e a proibição de 1:N via IAM least-privilege são **garantias arquiteturais**, não escolhas opcionais — independem do provedor que vencer.
