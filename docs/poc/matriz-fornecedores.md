# Matriz comparativa de provedores de biometria facial — POC ADR-002

> Documento de apoio à decisão do ADR-002. **Funcionais na POC:** AWS Rekognition (Face Liveness + CompareFaces) e Unico IDCloud. **Paper-only:** Azure AI Face, FaceTec ZoOm, Serpro Datavalid. **Descartado:** Google Cloud Vision (não realiza verificação 1:1 de identidade — justificativa na última seção).
>
> Critérios e princípios não-negociáveis vêm do `CONTRACTS.md` (Seção 1): verificação **1:1**, biometria nunca como fator único, fotos raw nunca persistem, criança = consentimento expresso do responsável e Fase B condicionada a RIPD aprovado.

---

## Tabela comparativa

Legenda: ✅ atende · ⚠️ atende parcialmente / depende de configuração · ❌ não atende · ❓ informação não disponível publicamente (será medida na POC).

| Critério | AWS Rekognition (Face Liveness + CompareFaces) | Unico IDCloud / Unico Check | Azure AI Face / Face Liveness + Face Verification | FaceTec ZoOm 3D Liveness | Serpro Datavalid | Google Cloud Vision |
|---|---|---|---|---|---|---|
| **Liveness ativo** | ⚠️ Microdesafios passivos (movimento natural durante 3s); não há challenge de fala/gesto explícito | ✅ Possui liveness ativo (gestos guiados) além de passivo | ✅ Liveness passivo nativo (motion challenge opcional via SDK) | ✅ Liveness ativo "ZoOm" com microposicionamento 3D | ⚠️ Liveness terceirizado; depende da combinação contratada | ❌ Não há liveness |
| **Liveness passivo (PAD Level 1/2)** | ✅ iBeta PAD Level 2 (Face Liveness) | ✅ iBeta PAD Level 2 (varia por SDK) | ✅ iBeta PAD Level 2 | ✅ iBeta PAD Level 2 (vetor 3D) | ⚠️ Conforme provedor encadeado | ❌ Não há |
| **Verificação 1:1 (CompareFaces / Match)** | ✅ `CompareFaces` (image-to-image) | ✅ Unico Score 1:1 (selfie vs. enrollment) | ✅ Face API `Verify` 1:1 | ✅ `Match3D` (template vs. ZoOm session) | ✅ `validar-biometria-facial` (selfie vs. base interna do Serpro — vide observação) | ❌ Apenas detecção/atributos, sem identidade |
| **SDK Android** | ✅ AWS Amplify Liveness (Kotlin/Compose) | ✅ SDK nativo Android Java/Kotlin | ✅ Azure AI Face Liveness SDK (Kotlin) | ✅ SDK nativo Android | ⚠️ API REST + integração própria | n/a |
| **SDK iOS** | ✅ AWS Amplify Liveness (Swift/SwiftUI) | ✅ SDK nativo iOS Swift/Obj-C | ✅ Azure AI Face Liveness SDK (Swift) | ✅ SDK nativo iOS | ⚠️ API REST + integração própria | n/a |
| **SDK .NET / bindings MAUI** | ⚠️ Backend C# pleno via `AWSSDK.Rekognition`; **Liveness SDK só Web/Android/iOS** — em MAUI usa Custom Handler/Renderer para envolver o SDK Android/iOS oficial | ⚠️ Sem NuGet/MAUI binding oficial; integração via REST + custom binding nativo no MAUI (esforço médio) | ⚠️ Backend `Azure.AI.Vision.Face` em .NET; Liveness SDK requer MAUI Handler/Renderer | ❌ Sem suporte MAUI nativo; binding pesado | ⚠️ REST puro (.NET HttpClient); selfie capturada manualmente | n/a |
| **Funcionamento em baixa conectividade** | ⚠️ Liveness exige upload de vídeo curto (~1-2 MB) e callback de resultado; falha em 3G fraco | ⚠️ Captura local + envio; tolerância média (SDK retenta) | ⚠️ Similar à AWS — upload de sessão liveness | ⚠️ Liveness local cria template 3D pequeno (~100 KB) — melhor tolerância | ❌ REST síncrono pesado; sem fallback offline | n/a |
| **Latência típica (ponta a ponta)** | ❓ 4-7s (publicado em re:Invent demos; **medir na POC**) | ❓ 3-6s (material Unico; **medir na POC**) | ❓ 4-8s (docs MS Learn; **medir na POC**) | ❓ 2-5s (claim do fornecedor) | ❓ Variável conforme rede + processamento Serpro | n/a |
| **FAR / FRR publicada** | NIST FRVT 2024: ~0.05% FMR @ FNMR 0.5% (ordem de grandeza, depende do `SimilarityThreshold`) | NIST FRVT 2024: presente; classificação intermediária por categoria | NIST FRVT 2024: top-5 em várias categorias | NIST FRVT (algoritmo subjacente Paravision) — top-3 LATAM | Não submete ao NIST FRVT publicamente | n/a |
| **Performance com crianças (5-15 anos)** | ⚠️ AWS reconhece viés em <13 anos; recomenda recadastro frequente. Documentação cita acurácia menor que adultos | ⚠️ Unico tem base brasileira; comportamento melhor em adolescentes que em crianças <8 anos | ⚠️ MS Responsible AI: limita uso pediátrico em alguns SKUs; depende de configuração | ⚠️ Sem dado público específico para crianças | ⚠️ Base focada em adultos (CNH/RG) — desempenho com crianças não documentado | n/a |
| **Anti-spoofing — foto impressa** | ✅ PAD L2 cobre | ✅ PAD L2 cobre | ✅ PAD L2 cobre | ✅ PAD L2 cobre | ⚠️ Depende do fluxo encadeado | ❌ |
| **Anti-spoofing — vídeo em tela** | ✅ Detecta moiré/reflexo | ✅ Detecta | ✅ Detecta | ✅ Detecta | ⚠️ Depende | ❌ |
| **Anti-spoofing — máscara 2D/3D** | ⚠️ Máscara silicone hiperrealista: defesa parcial | ⚠️ Parcial | ⚠️ Parcial | ✅ Mais forte (3D explícito) | ❓ | ❌ |
| **Anti-spoofing — deepfake injetado via virtual cam** | ⚠️ Liveness SDK valida assinatura de captura (mitiga, não elimina) | ⚠️ SDK valida origem da câmera | ⚠️ SDK valida via attestation Android/iOS | ⚠️ Detecção heurística + bind do device | ❌ | ❌ |
| **Armazenamento de imagens raw pelo provedor** | ✅ Imagem do Liveness fica até 24h em S3 gerido pela AWS; deletada por padrão. CompareFaces não persiste | ⚠️ Permite arquivar selfie em "trilha" (recurso opcional — desabilitar). Templates ficam no Unico | ⚠️ Sessão Liveness retém vídeo 24-48h para auditoria (configurável) | ✅ Não persiste imagem; template 3D fica local até envio | ⚠️ Selfie enviada chega ao Serpro, depende do contrato | n/a |
| **Retenção máxima do provedor** | 24h (liveness) / sob demanda (CompareFaces — stateless) | Configurável; default ~30 dias para trilha | 24-48h padrão (configurável até 30d) | Apenas template (sem foto) | Conforme contrato Serpro / Detran | n/a |
| **DSR — delete sob solicitação** | ✅ Stateless (CompareFaces); Liveness expira sozinho | ✅ `DELETE /subjects/{id}` na API documentada | ✅ API `Delete-Person` | ✅ Delete via API (template apenas) | ⚠️ Solicitação manual via canal Serpro | n/a |
| **Data residency Brasil (sa-east-1 / Brasil)** | ✅ Face Liveness e CompareFaces disponíveis em `sa-east-1` | ✅ Datacenters no Brasil | ⚠️ Brazil South (Azure) com Face Liveness em rollout; verificar SKU | ⚠️ Hosted multi-region; cliente pode self-host | ✅ Datacenter Serpro no Brasil | n/a |
| **Criptografia em trânsito** | ✅ TLS 1.2+ obrigatório | ✅ TLS 1.2+ | ✅ TLS 1.2+ | ✅ TLS 1.2+ | ✅ TLS 1.2+ | ✅ |
| **Criptografia em repouso** | ✅ KMS (CMK quando aplicável) | ✅ AES-256 gerido | ✅ Storage Service Encryption + opção CMK | ✅ Self-hosted permite CMK próprio | ✅ Gerido Serpro | n/a |
| **DPA / Termo de operador (LGPD)** | ✅ AWS DPA + Brazil addendum padrão | ✅ DPA Unico + manuais LGPD | ✅ Microsoft DPA + termos Brazil | ✅ DPA padrão FaceTec | ✅ Termos Serpro/CGU | ⚠️ Termos Google (genéricos) |
| **Suboperadores listados** | ✅ Lista pública AWS suboperadores | ✅ Lista parcial sob NDA | ✅ MS Trust Center | ⚠️ Sob NDA | ✅ Datacenter próprio Serpro | ✅ |
| **Facilidade de integração (.NET Lambda + MAUI)** | Alta no backend; média no MAUI (handler nativo) | Média (sem MAUI binding, REST OK) | Alta no backend; média no MAUI | Baixa (foco JS/native) | Média (REST puro) | n/a |
| **Custo por validação (referência pública)** | $0.0025 por sessão Face Liveness + $0.001 por CompareFaces (us-east-1; `sa-east-1` ~10% superior) | Faixa R$ 0,30–0,80 por verificação (depende de volume contratado) | $1 por 1000 transações Face API (Standard); Liveness em pricing separado | $0.30–1.00 por validação enterprise (varia por contrato) | R$ 0,15–0,50 por consulta Datavalid (faixa pública conhecida) | n/a |
| **DSR — exclusão sob solicitação (operacional)** | API + IaC | API + console Unico | API + portal | API + console | E-mail + protocolo | n/a |
| **Auditoria / explicabilidade / contestação** | ✅ CloudTrail + similarity score + bounding boxes; sem decision logs | ✅ Dashboard Unico + trilha de eventos; documenta motivo da rejeição | ✅ Logs Azure Monitor + score | ⚠️ Logs próprios; menos transparência | ✅ Trilha Serpro auditável | n/a |
| **Submetido a NIST FRVT (verificação 1:1)** | ✅ Sim (Amazon) | ✅ Sim (Unico) | ✅ Sim (Microsoft) | ✅ Sim (algoritmo Paravision) | ❌ Não submete | ❌ Não submete |

---

## 1. Por que **AWS Rekognition** é provedor funcional #1 na POC

1. **Alinhamento de stack.** Todo o backend SafeRide Kids roda em Lambda .NET 8 + IAM + KMS + S3. Adicionar Rekognition é zero atrito de plataforma (`AWSSDK.Rekognition`, IAM nativo).
2. **Liveness + Match desacoplados.** Face Liveness e CompareFaces são APIs independentes — combinam exatamente com o fluxo do `IFaceVerificationProvider` (sessão liveness → verify 1:1).
3. **Disponibilidade em `sa-east-1`.** Atende LGPD por data residency sem ginástica de transferência internacional.
4. **Preço previsível e barato.** ~$0.0035 por verificação completa cabe folgado nos $50-90/mês do POC e segue saudável no MVP.
5. **PAD Level 2 iBeta + ARN-level IAM.** Permite least-privilege rigoroso (proibimos `IndexFaces`/`SearchFaces` na role da Lambda — vide `ApiConstruct`).
6. **DSR simples.** CompareFaces é stateless; Face Liveness expira em 24h. Não há "user" a deletar — `DeleteEnrollmentAsync` só precisa cuidar do que guardamos *internamente* (S3 + DB).
7. **Documentação pública robusta e CloudTrail nativo** para audit trail LGPD.

**Riscos assumidos:**
- Acurácia em crianças <10 anos é provavelmente abaixo do que ainda precisamos validar; recadastro a cada 6 meses (regra do produto) atenua.
- Amplify Liveness SDK não tem binding MAUI oficial → necessário Custom Handler no app Família para enrollment.

---

## 2. Por que **Unico IDCloud** é provedor funcional #2 na POC

1. **Base de treinamento brasileira.** A maior parte do dataset Unico vem de KYC bancário no Brasil — distribuição étnica mais próxima da população real do que datasets US/Europe-centric.
2. **Liveness ativo + passivo no mesmo SDK.** Útil para comparar UX com a abordagem só-passiva da AWS na Fase A.
3. **Maturidade jurídica LGPD.** DPA e RIPD-friendly material disponível em PT-BR; canal de DPO direto.
4. **Independência de fornecedor.** Reduz lock-in da arquitetura ao manter dois provedores que falam a mesma interface (`IFaceVerificationProvider`).
5. **Pricing competitivo em volume nacional.**

**Riscos assumidos:**
- Sem binding .NET/MAUI oficial — integração via REST + Custom Handler nativo (esforço médio).
- Curva de onboarding comercial é mais lenta que AWS (precisa contrato + KYC empresa) — segredo `unico-api-key` fica vazio até esse passo.
- Algumas funcionalidades premium (trilha de auditoria persistente) custam à parte.

---

## 3. Por que **Azure / FaceTec / Serpro** entraram apenas como stub documental

Para todos os três, mantemos `IFaceVerificationProvider` stub (`SafeRideKids.Biometria.Providers.Stubs/*`) que lança `NotSupportedException("Stub documental. Justificativa: ...")`. Isso documenta a decisão arquitetural sem inflar o escopo da POC.

### Azure AI Face / Face Liveness
- Tecnicamente excelente, NIST top-5.
- **Bloqueio principal:** o produto Face API entrou em "Limited Access" desde 2024 — exige formulário de aprovação Microsoft + revisão por compliance, com período típico de 4-8 semanas. Não cabe no calendário da POC.
- **Custo operacional adicional:** abrir tenant Azure só para isso vai contra a regra "stack único AWS" (vide README raiz).
- **Vantagem futura:** se SafeRide adotar Microsoft 365 no admin, reabilitar como opção.

### FaceTec ZoOm
- Melhor anti-spoofing 3D do mercado para máscaras realistas.
- **Bloqueio principal:** sem SDK MAUI / .NET nativo; binding requer trabalho considerável de Custom Handler em ambos os apps.
- **Negociação comercial fechada (enterprise).** Pricing público inexistente, ciclo de venda longo (3-6 meses).
- **Adequação ao caso:** verificação de crianças no embarque não precisa de PAD nível 3D — risco de máscara silicone é desprezível no contexto.
- **Manter na matriz** para revisão se SafeRide expandir para casos com risco de fraude maior (ex.: KYC de motorista).

### Serpro Datavalid
- Único provedor "estatal" — interessante para integração com bases CNH/RG dos motoristas no contexto **Driver Onboarding** (ADR-003), não no **Family Check-in** (ADR-002).
- **Bloqueio principal:** API foca em validar identidade contra base oficial (CPF + selfie vs. foto do RG/CNH) — não é projetada para o caso "esta criança é a esperada para esta parada". Crianças nem têm registro biométrico estatal.
- **Manter na matriz** como referência para o ADR-003 e como opção futura de validação cruzada do responsável legal no momento do consentimento.

---

## 4. Por que **Google Cloud Vision** não qualifica como provedor de verificação 1:1

Google Cloud Vision **não é um provedor de reconhecimento facial 1:1**. O conjunto de APIs faciais que o serviço oferece (`FACE_DETECTION`) executa:

- **Detecção facial:** desenha bounding boxes e landmarks (olhos, boca, sobrancelhas) em uma imagem.
- **Análise de atributos:** estima emoção, exposição, headwear, ângulo da cabeça etc.
- **Detecção de likelihood** (alegria, raiva, tristeza, surpresa).

O que ele **não faz**:

- Não gera template/embedding persistente associado a uma pessoa.
- Não compara duas imagens para responder "é a mesma pessoa?".
- Não opera enrollment + verify.
- Não submete algoritmo ao NIST FRVT (porque não é um algoritmo de reconhecimento).

O Google [explicitamente removeu](https://cloud.google.com/vision/docs/face-tutorial) e [politicamente proíbe](https://cloud.google.com/vision/docs/detecting-faces) o uso de Cloud Vision para "facial recognition" — desde 2018, a empresa decidiu não oferecer biometria de identificação como produto comercial geral (apenas via Google Workspace/Photos para uso pessoal). A única opção Google para reconhecimento facial enterprise é via **Vertex AI custom model**, o que sai do escopo de "provedor terceirizado plug-and-play" e exige treinamento próprio com risco regulatório enorme (controlador passa a ser SafeRide Kids treinando modelo a partir de imagens reais de crianças — inviável LGPD).

**Conclusão:** Google Cloud Vision **não atende** ao requisito de verificação 1:1 do `IFaceVerificationProvider`. Permanece listado na matriz por completude — para deixar registrado o motivo de descarte em revisão de ADR — mas não tem stub no código.
