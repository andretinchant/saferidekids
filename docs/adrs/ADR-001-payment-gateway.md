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
