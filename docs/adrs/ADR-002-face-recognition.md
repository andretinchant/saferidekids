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
