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
