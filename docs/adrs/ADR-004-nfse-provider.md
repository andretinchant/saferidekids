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
