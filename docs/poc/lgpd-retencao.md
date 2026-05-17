# Política de retenção e descarte — POC biometria SafeRide Kids

> Baseada em `CONTRACTS.md` Seção 8. Aplicável apenas à POC; em produção a política deve ser revalidada pelo encarregado e refletir o RIPD/DPIA aprovado.
>
> **Encarregado de dados (DPO) — placeholder:** `dpo@saferidekids.example`

---

## 1. Matriz de retenção

| Tipo de dado | Categoria LGPD | Armazenamento ativo | Arquivo / cold | Ação ao final | Mecanismo de descarte |
|---|---|---:|---:|---|---|
| **Foto bruta (selfie/enrollment)** | Sensível biométrico | **0 (não persiste)** | n/a | Descartada após uso em memória | Buffer in-memory; SDK pode reter até fim da sessão; nunca grava em disco do dispositivo (CONTRACTS 1.6) |
| **Foto temporária do provedor (Liveness)** | Sensível biométrico | ≤ 24h no provedor | n/a | Apagada pelo provedor automaticamente | TTL nativo AWS Face Liveness; configurar TTL = 24h em Unico/Azure |
| **Template biométrico (embedding)** | Sensível biométrico | 6 meses no S3 (KMS) | 30 dias adicionais em Glacier IR (lifecycle) | Apagado por lifecycle S3 + DELETE na API do provedor | Lifecycle automático S3 (210 dias total); job diário `EnrollmentExpiryJob` chama `DeleteEnrollmentAsync` no provedor |
| **Registro `enrollment` (DB)** | Sensível (referência ao template) | 6 meses ativo | n/a | Soft delete (`status='deleted'`); hard delete em job mensal | EF Core query + job |
| **Registro `consent` (DB)** | Pessoal (PII do responsável + hash CPF) | 6 anos | n/a | Conservado para prova de base legal | Não apagar mesmo após enrollment expirar |
| **PDF do consentimento assinado (S3)** | Pessoal | 6 anos | Glacier após 1 ano | Apagado por lifecycle | Lifecycle S3 dedicado (bucket separado se necessário em produção) |
| **`checkin` (DB hot)** | Pessoal + sensível (resultado biometria) | 90 dias | n/a | Migração para S3 Glacier (formato Parquet) | Job mensal `CheckinArchiveJob` |
| **`checkin` arquivado (S3 Glacier)** | Pessoal + sensível | n/a | 6 anos | Apagado por lifecycle | Lifecycle S3 Glacier |
| **`checkin_event` (DB)** | Audit trail | 90 dias | 6 anos (junto com checkin) | Mesma rota do checkin | Mesmo job |
| **`audit_log` (DB)** | Audit trail LGPD | 1 ano | 6 anos em S3 Glacier | Apagado por lifecycle | Job mensal `AuditLogArchiveJob` lê `retention_until` |
| **`fallback_pin` (DB)** | Pessoal (hash do PIN) | 24h | n/a | Hard delete | Job diário `FallbackPinCleanupJob` |
| **`notification_outbox` (DB)** | Pessoal | 30 dias | n/a | Hard delete | Job semanal |
| **Logs CloudWatch (Lambda + API GW)** | Telemetria com PII hashada | 30 dias | n/a | Apagado por configuração do log group | Retenção do log group (`RetentionDays.ONE_MONTH`) |
| **Métricas CloudWatch** | Agregado anônimo | 15 meses (default AWS) | n/a | Decaimento automático | n/a |
| **Secrets (Aurora password, salts, pgp key, Unico key)** | Operacional (não tem PII) | Indefinido (até rotação) | n/a | Substituição via rotação manual | Rotação anual sugerida |

---

## 2. Procedimentos automatizados (jobs)

Todos como Lambdas agendadas via EventBridge — provisionar em sprint posterior (fora do escopo desta IaC inicial — vide pendências do README do CDK).

### 2.1 `EnrollmentExpiryJob` — diário, 02:00 BRT
1. `SELECT id, provider_id, provider_reference_id, template_storage_key FROM enrollment WHERE status='active' AND expires_at < NOW()`.
2. Para cada registro:
   - `IFaceVerificationProvider.DeleteEnrollmentAsync(provider_reference_id)`.
   - `s3:DeleteObject` no `template_storage_key` (se existir).
   - `UPDATE enrollment SET status='expired', deleted_at=NOW() WHERE id=?`.
   - `INSERT audit_log (action='EnrollmentExpired', target_id=child_id, payload=...)`.
3. Notificar responsáveis (notification_outbox: template `enrollment_expired`).

### 2.2 `CheckinArchiveJob` — mensal, primeiro domingo, 03:00 BRT
1. Selecionar `checkin` + `checkin_event` com `started_at < NOW() - INTERVAL '90 days'`.
2. Exportar para Parquet particionado por `year=yyyy/month=mm` em `s3://<archive-bucket>/checkins/`.
3. Apagar do DB após confirmar checksum.
4. `INSERT audit_log (action='CheckinArchived', payload={count, partition})`.

### 2.3 `AuditLogArchiveJob` — mensal, primeiro domingo, 04:00 BRT
1. `SELECT * FROM audit_log WHERE occurred_at < NOW() - INTERVAL '1 year'`.
2. Exportar para `s3://<archive-bucket>/audit-log/year=yyyy/month=mm/`.
3. Apagar do DB.

### 2.4 `FallbackPinCleanupJob` — diário, 01:00 BRT
1. `DELETE FROM fallback_pin WHERE valid_until < NOW()`.

### 2.5 `DsrJob` (assíncrono, sob demanda)
Acionado por endpoint `POST /api/v1/dashboard/dsr/{action}`:

- **access:** monta JSON com todos os dados do titular, anexa PDF do consentimento, manda para o responsável.
- **delete:** cascateia:
  1. `DeleteEnrollmentAsync` em todos os providers ativos para a criança.
  2. `s3:DeleteObject` em todos os templates.
  3. Soft delete em `child` (preserva `consent` para prova de base legal por 6 anos, mas com flag `child_deleted=true` no audit).
  4. Anonimização do nome da criança (`display_name_encrypted = NULL`).
  5. `INSERT audit_log (action='DsrDeleteExecuted')`.
- **portability:** mesmo JSON do access em formato CSV + JSON dentro de ZIP.

**SLA interno:** 7 dias úteis (LGPD permite até 15).

---

## 3. Comandos manuais (operação)

| Cenário | Comando |
|---|---|
| Expirar enrollment de uma criança específica agora | `dotnet run --project tools/SafeRideKids.Poc.Ops -- expire-enrollment --child-id <uuid>` |
| Executar DSR-delete imediato (escalado por DPO) | `dotnet run --project tools/SafeRideKids.Poc.Ops -- dsr-delete --child-id <uuid> --justification "<motivo>"` |
| Listar enrollments expirando nos próximos 7 dias | `dotnet run --project tools/SafeRideKids.Poc.Ops -- list-expiring --days 7` |
| Forçar rotação de tenant-salt (re-hash de todos os PIIs) | **Procedimento estendido** — requer downtime; documentado separadamente em ADR futuro |

> O projeto `tools/SafeRideKids.Poc.Ops` é responsabilidade do agente Backend; aqui listamos apenas a expectativa.

---

## 4. Exceções

| Cenário | Tratamento |
|---|---|
| **Litígio judicial / ofício** | Suspende exclusão dos registros relacionados (checkin, audit_log) até liberação judicial. Marcação manual: `audit_log.payload = { hold: true, reason, court_order_id }`. DPO documenta caso em planilha controlada. |
| **Incidente de segurança em investigação** | Suspende job de archive de `audit_log` referente à janela investigada. |
| **Pedido de criança que atingiu maioridade** (5 anos no futuro) | Titular passa a poder exercer direitos diretamente — fluxo separado do responsável. Fora do escopo POC. |
| **Solicitação de retenção mais longa pelo responsável** | Não atendida — política única e mínima por LGPD princípio da minimização. |
| **Falha técnica em provedor (delete não confirmado)** | Job marca `enrollment.status='delete_failed'`, gera alarme, retenta a cada 6h, escala para DPO se 3 falhas seguidas. |

---

## 5. Verificação anual

O encarregado executa anualmente um relatório de retenção:
1. Listagem de templates vivos > 6 meses (deve ser zero).
2. Quantidade de DSR-delete atendidas e SLA médio.
3. Volume em Glacier vs. previsão de custo.
4. Revisão de TTL nos provedores externos.

Resultado anexado ao RIPD/DPIA na revisão anual obrigatória.
