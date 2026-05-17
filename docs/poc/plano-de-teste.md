# Plano de teste — POC biometria SafeRide Kids (Fase A — adultos)

> Documento operacional para a Fase A da POC. **Sem crianças**. Fase B depende de RIPD aprovado e fica fora deste plano.
>
> Versão: 0.1 · Data: 2026-05-17 · Encarregado (placeholder): `dpo@saferidekids.example`

---

## 1. Objetivo

Decidir o provedor de biometria facial do MVP (ADR-002) com **dados de campo**, comparando AWS Rekognition (Face Liveness + CompareFaces) e Unico IDCloud em condições representativas (iluminação variada, ruído, conectividade limitada, aparelhos heterogêneos), avaliando latência, taxa de acerto, robustez a spoofing simples e aceitação subjetiva.

---

## 2. Hipóteses

| ID | Hipótese | Critério quantitativo |
|---|---|---|
| H1 | AWS Rekognition tem **latência ponta-a-ponta** ≤ Unico em 4G médio (~5 Mbps) | Mediana e p95 menor ou estatisticamente empatada |
| H2 | Ambos os provedores têm **FAR (falso aceite)** < 1% em condições controladas com tentativas adversariais simples (foto impressa / vídeo em tela) | FAR < 0,01 em 50 tentativas |
| H3 | **FRR (falso bloqueio)** < 5% em condições normais para adultos | Em 100 verificações legítimas, ≤ 5 bloqueios |
| H4 | **Inconclusive rate** < 10% em condições adversas (luz baixa, óculos, contraluz) | Inconclusive < 0,10 |
| H5 | Diferença de FRR entre **aparelhos high-end iOS e low-end Android (~Moto G)** é ≤ 5 pontos percentuais | Variação por aparelho registrada |
| H6 | **Aceitação subjetiva** dos voluntários é ≥ 4 (escala Likert 1-5) | Mediana Likert ≥ 4 |
| H7 | **Modo offline** dispara fallback PIN em ≤ 2 segundos após detecção de falha de rede | Tempo até prompt fallback < 2s em 95% dos casos |
| H8 | **Custo médio por verificação** se mantém dentro do orçamento previsto (~$0,003 AWS / ~R$ 0,50 Unico) | Coletado via telemetria `provider_cost_microcents` |

---

## 3. População e recrutamento

- **N = 10-20 adultos voluntários**, recrutados internamente (equipe + familiares maiores de idade).
- Distribuição alvo (não exigência rígida — relatar viés se faltar):
  - Tom de pele: pelo menos 3 níveis Fitzpatrick representados (II/III/V).
  - Idade: faixa 20-60 anos.
  - Óculos: ≥ 30% dos participantes usuários.
  - Barba: ≥ 30% dos participantes (homens) com barba média/grande.
- **Consentimento escrito obrigatório** (template no Anexo A) — sem assinatura, voluntário não participa.
- Cada voluntário recebe:
  - Cópia assinada do termo.
  - E-mail com o ID interno gerado (UUID), prazo para revogação (a qualquer momento) e canal do DPO.
  - Compromisso de exclusão definitiva dos templates em ≤ 7 dias úteis após o fim da POC (ou imediato se revogar).

---

## 4. Aparelhos

Mínimo de 3 modelos físicos, cobrindo:

| Tier | Modelo de referência | OS |
|---|---|---|
| High-end iOS | iPhone 14/15 (qualquer) | iOS 17/18 |
| Mid Android | Samsung A54 / Pixel 7a / Motorola Edge 50 | Android 13/14 |
| Low Android | Motorola Moto G54 / Samsung A15 | Android 13/14 |

Cada cenário (C1-C6) executado em **todos os 3 tiers**.

---

## 5. Cenários

### C1 — Captura inicial (enrollment), ambiente controlado
- Sala iluminação difusa (~500 lux), fundo neutro, distância 30-40 cm.
- 3 a 5 fotos.
- Repete enrollment em **ambos** os provedores em paralelo (rota Backend já faz isso).
- **Métrica:** sucesso, tempo total de enrollment, recusas do liveness.

### C2 — Verificação em condições variadas
Subcondições, cada uma 5 verificações por voluntário por aparelho:
| Sub | Condição |
|---|---|
| C2a | Luz dia (~600 lux) — baseline |
| C2b | Luz baixa (~100 lux) |
| C2c | Contraluz (janela atrás) |
| C2d | Sombra parcial no rosto |
| C2e | Óculos (se voluntário usa) |
| C2f | Máscara textil tipo bandana (cobrindo nariz/boca) |

- **Métrica:** outcome, confidence, liveness pass, latência, retentativas.

### C3 — Tentativas adversariais simples (anti-spoofing)
| Sub | Adversário | Esperado |
|---|---|---|
| C3a | Foto impressa em A4 da face do voluntário | Rejeitar via liveness |
| C3b | Foto exibida em tela de celular (selfie do voluntário) | Rejeitar via liveness |
| C3c | Vídeo curto (5s) exibido em tela de celular | Rejeitar via liveness |

- **Métrica:** quantos passam (≡ falsos aceites = falha do provedor).
- **Limites éticos:** sem máscaras silicone, sem deepfake gerado (custos + escopo de ataque sofisticado fora da POC).

### C4 — Latência por rede
| Rede | Configuração |
|---|---|
| Wi-Fi corporativo | ~100 Mbps |
| 4G | Tethering, ~5 Mbps |
| 3G simulado | Throttle do Charles/Proxyman para 384 Kbps |

- 10 verificações por rede por provedor por aparelho.
- **Métrica:** latência total (`StartLiveness` → `Verify` retornado), separada por etapa.

### C5 — Comportamento offline
- Modo avião acionado a meio do fluxo.
- **Métrica:** tempo até app cair em fallback PIN; quantos cliques extras; consistência da mensagem; PIN realmente válido.

### C6 — Aceitação subjetiva
Após cada bateria, voluntário responde:
| Pergunta | Escala |
|---|---|
| Quão rápido você sentiu o processo? | 1 (muito lento) — 5 (muito rápido) |
| Quão claro foi o que precisava fazer? | 1-5 |
| Quão confortável você ficou? | 1-5 |
| Você usaria diariamente? | 1-5 |
| (Aberta) O que mais incomodou? | texto livre |

---

## 6. Métricas exigidas no relatório final

| Métrica | Cálculo |
|---|---|
| Tempo médio de verificação (s) | mean(`finished_at` − `started_at`) por provedor |
| Mediana e p95 de latência (ms) | percentil sobre `latency_ms` |
| Taxa de sucesso (%) | count(`Approved`) / count(verificações legítimas) |
| Taxa de inconclusive (%) | count(`Inconclusive`) / count(total) |
| Falso aceite (FAR) | count(approved em C3) / count(C3) |
| Falso bloqueio (FRR) | count(rejected em verificações legítimas C1+C2) / count(legítimas) |
| Tempo médio até fallback offline (ms) | mean(intervalo desde falha de rede até prompt) |
| Comparativo por aparelho | Tabela cruzada provider × tier de aparelho |
| Aceitação Likert | Mediana + IQR por pergunta por provedor |
| Custo por verificação (USD/BRL) | sum(`provider_cost_microcents`) / count |
| Quebra por tom de pele e por óculos | Tabelas cruzadas separadas (com nota explícita sobre tamanho amostral) |

---

## 7. Datasets sintéticos complementares (sem PII)

Para caracterização estatística adicional, **fora do app** e **sem produção de check-in**, usamos datasets públicos consagrados:

| Dataset | Uso |
|---|---|
| **LFW** (Labeled Faces in the Wild) | Benchmark de 1:1 verification em pares positivos/negativos (rosto adulto, "in the wild") |
| **FRLL** (Face Research London Lab) | Variação controlada de iluminação e ângulo |

Esses datasets:
- São executados **localmente** num notebook offline contra o `IFaceVerificationProvider` via script Python/.NET tool — **não passam pelo bucket S3 da POC** nem pelo banco de dados do app.
- Servem apenas para complementar a Fase A com volume estatístico maior em conditions específicas.
- **Não substituem** os voluntários — adicionam só ao analytics.

---

## 8. Procedimento operacional

| Sessão | Duração | Conteúdo |
|---|---|---|
| Briefing | 30 min | Apresentação da POC, leitura do termo, dúvidas, assinatura |
| Enrollment (C1) | 15 min | Cadastro em ambos os provedores |
| Bateria C2 | 30 min | Verificações em condições variadas |
| Bateria C3 (anti-spoofing) | 15 min | Adversariais simples |
| Bateria C4 (latência) | 30 min | Por aparelho × por rede |
| C5 (offline) | 5 min | Fallback |
| C6 (questionário) | 10 min | Likert + aberta |
| **Total por voluntário** | **~2h15** | |

Operador da POC:
- Registra ID anônimo do voluntário em planilha controlada (acesso só DPO + lead técnico).
- Executa o fluxo via app Família + app Motorista, em ambiente staging.
- Após cada sessão, exporta JSON do dashboard de métricas e arquiva sob ID do voluntário.

---

## 9. Critérios de saída da Fase A

Para considerar a Fase A concluída e gerar a decisão do ADR-002:

1. Mínimo de **10 voluntários completos** (todas as baterias).
2. Cobertura dos 3 tiers de aparelhos.
3. Relatório consolidado com todas as métricas da Seção 6.
4. Comparativo final em duas formas:
   - Tabela quantitativa (winner por métrica).
   - Análise qualitativa (UX, suporte, integração).
5. **Recomendação justificada** ao ADR-002 (mantém os 2 funcionais? promove 1 e descarta o outro? muda configuração de threshold?).
6. Apresentação aos stakeholders + arquivamento do relatório.
7. **Exclusão definitiva** de templates + dados dos voluntários ≤ 7 dias úteis após apresentação.

---

## 10. Fora deste plano

- **Fase B (crianças):** depende de RIPD assinado, consentimento dos responsáveis legais, parecer jurídico externo, eventual aprovação institucional/escolar. Plano específico será escrito separadamente, **bloqueado** até este pre-requisito.
- **Teste com motoristas reais em rota real:** apenas em Fase B.
- **Testes adversariais sofisticados** (máscara silicone, deepfake otimizado, ataque de injeção em câmera virtual): fora do escopo POC, possivelmente em pen-test pré-MVP (vide README raiz).
- **Comparativo com provedores stub** (Azure, FaceTec, Serpro): paper-only (vide `matriz-fornecedores.md`).

---

## Anexo A — Template de termo de consentimento para voluntários adultos

> **Distribuído antes da sessão, assinado em duas vias.**

---

**Termo de consentimento — Participação em POC SafeRide Kids — Biometria Facial (Fase A)**

Eu, _________________________________________________________ (nome completo), CPF ________________________, declaro que tenho mais de 18 anos e que **livremente concordo** em participar como voluntário(a) na Fase A da Prova de Conceito de biometria facial da SafeRide Kids.

**Entendo que:**

1. Serão capturadas fotografias do meu rosto e dados biométricos para fins de **testes técnicos** de provedores de reconhecimento facial. Não há finalidade comercial, marketing ou treinamento de modelo.
2. Os dados serão **processados** pelos provedores AWS Rekognition e Unico IDCloud, ambos com data residency no Brasil.
3. **Não serão usados** para identificar pessoas em base de terceiros (verificação 1:N), nem compartilhados fora deste escopo.
4. Os templates biométricos e qualquer dado meu serão **excluídos definitivamente** em até 7 dias úteis após o fim da POC, ou **imediatamente** se eu solicitar.
5. Posso **revogar** este consentimento a qualquer momento, sem qualquer prejuízo, contatando `dpo@saferidekids.example`.
6. Receberei cópia deste termo assinado.

**Confirmo a presença na sessão de Fase A em ____ / ____ / ______.**

| Item | Valor |
|---|---|
| Nome completo | ___________________________________________ |
| CPF | ____________________________ |
| E-mail | ____________________________ |
| Telefone | ____________________________ |
| Modelos de aparelhos que vou usar | ____________________________ |
| Data | ____ / ____ / ______ |
| Assinatura | ________________________________________________ |
| Testemunha (operador POC) | ________________________________________________ |

*Em caso de dúvida sobre meus direitos como titular, posso contatar o encarregado (DPO) pelo e-mail acima ou a Autoridade Nacional de Proteção de Dados (ANPD).*
