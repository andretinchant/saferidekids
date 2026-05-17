# RIPD / DPIA — POC Biometria Facial SafeRide Kids

> **Esqueleto** de Relatório de Impacto à Proteção de Dados Pessoais (RIPD / DPIA), conforme ANPD e LGPD Art. 38. Documento **não substitui** revisão jurídica externa — campos `[A PREENCHER]` dependem de parecer formal antes de qualquer Fase B.
>
> **Importante:** este RIPD deve estar **assinado e aprovado pelo encarregado e por parecer jurídico externo** antes de qualquer coleta biométrica de criança. A Fase A da POC ocorre **somente com adultos voluntários** (CONTRACTS Seção 16).

| Campo | Valor |
|---|---|
| Versão | 0.1 (esqueleto POC) |
| Data | 2026-05-17 |
| Status | `Em elaboração — não apto para produção` |
| Próxima revisão | Antes do início da Fase B; depois anual |

---

## 1. Identificação do controlador e do encarregado

| Item | Conteúdo |
|---|---|
| Controlador | SafeRide Kids — `[A PREENCHER: razão social, CNPJ, endereço]` |
| Encarregado (DPO) | `[A PREENCHER: nome, CPF, e-mail institucional]` (placeholder atual: `dpo@saferidekids.example`) |
| Operadores | AWS Brasil (`AWSSDK.Rekognition` + Face Liveness em `sa-east-1`); Unico IDCloud Brasil; *(adicionar Azure / FaceTec / Serpro se forem promovidos a funcionais)* |
| Responsável técnico | `[A PREENCHER: nome do engenheiro líder]` |
| Patrocinador interno | `[A PREENCHER: nome do CTO/CEO]` |

---

## 2. Descrição da operação

### 2.1 Natureza
Verificação biométrica facial **1:1** (a criança apresentada ao motorista é comparada contra um cadastro previamente registrado pelo responsável) no momento do embarque no transporte escolar.

### 2.2 Escopo e ciclo de vida
1. **Enrollment** (no app Família): responsável aceita termo destacado, captura 3-5 fotos da criança; foto bruta nunca persiste, template gerado pelos provedores e cifrado em S3 + KMS.
2. **Verify** (no app Motorista): captura selfie + sinal de vivacidade (liveness), comparada 1:1 contra o template; resultado `Approved` / `Inconclusive` / `Rejected`.
3. **Fallback obrigatório** (sempre disponível): PIN de 6 dígitos enviado ao responsável, ou conferência manual auditada — nunca depender exclusivamente de biometria.
4. **Expiração**: enrollment apagado em 6 meses (recadastro periódico devido ao crescimento da criança).

### 2.3 Dados tratados
- Foto facial bruta (transiente in-memory).
- Template biométrico (vetor matemático, cifrado em repouso).
- Nome da criança (cifrado em coluna `display_name_encrypted`).
- Nome e CPF do responsável (CPF armazenado apenas como hash HMAC).
- Registro do check-in (data/hora, parada, resultado, motorista, GPS).
- Hash de versão do termo de consentimento assinado.

### 2.4 Titulares
- **Crianças** (5-15 anos): titulares dos dados biométricos. Proteção reforçada (LGPD Art. 14 + Resolução CD/ANPD 2/2022).
- **Responsáveis legais**: pais ou tutores que autorizam o tratamento e cujo nome/CPF são tratados.
- **Motoristas**: ID profissional aparece nos registros de check-in (não há biometria do motorista).

### 2.5 Volume estimado (Fase A POC)
- 10 a 20 adultos voluntários (`não` crianças).
- ~200-400 verificações totais no período de 4 semanas.

### 2.6 Volume estimado (Fase B — `[A PREENCHER]`)
- Definir após sucesso da Fase A; depende de aprovação deste RIPD.

---

## 3. Necessidade e proporcionalidade

### 3.1 Justificativa do uso de biometria
Verificar identidade da criança no embarque é necessário para prevenir:
- Embarque de criança errada na van (risco de segurança imediata).
- Negação de embarque a criança certa por falha humana (motorista substituto, lista impressa, etc.).
- Disputa pós-fato entre família e motorista sobre quem embarcou onde.

### 3.2 Alternativas consideradas
| Alternativa | Análise |
|---|---|
| **Lista impressa + conferência visual (status quo)** | Sujeita a erro humano, troca de motorista, criança parecida. Não escala. |
| **PIN do responsável** | Funciona, mas obriga responsável a estar disponível em cada embarque. Inviável para rotina diária. **Mantido como fallback.** |
| **Tag BLE/NFC com a criança** | Boa precisão, mas crianças perdem/trocam tags. Custo de hardware. Considerado como roadmap pós-MVP. |
| **QR-code dinâmico no celular do responsável** | Mesmo problema do PIN. |
| **Reconhecimento facial 1:1** *(escolhido)* | Atrito mínimo no embarque (~3-5s), independe do responsável estar online, **sempre opcional**. |

### 3.3 Proporcionalidade
- Coleta limitada ao **mínimo necessário** (3-5 fotos, válidas por 6 meses).
- Uso restrito à finalidade declarada (não 1:N, não marketing, não treinamento de modelo).
- **Sempre opcional**: família que não quer biometria continua usando o serviço com fallback.
- Provedores escolhidos com data residency no Brasil.

### 3.4 Princípio do melhor interesse da criança
A operação foi desenhada para reduzir o risco mais grave (embarque errado), com salvaguardas que evitam tornar a criança um objeto técnico:
- Motorista nunca vê a foto cadastrada — apenas o resultado e o nome.
- Falha biométrica não expõe a criança publicamente; cai em fallback discreto.
- Recadastro semestral evita prisão por algoritmo treinado em "criança aos 6" quando ela já tem 8.

---

## 4. Riscos aos direitos do titular

| ID | Risco | Probabilidade | Impacto | Vetor / cenário típico |
|---|---|---|---|---|
| R-01 | Vazamento de templates biométricos por incidente em S3/KMS | Baixa | Alto | Mis-config de bucket policy, credencial AWS exposta em log/repo |
| R-02 | Vazamento do template no provedor externo (AWS/Unico) | Baixa | Alto | Incidente do operador; coberto por DPA |
| R-03 | Viés algorítmico — falso bloqueio sistemático em crianças de pele mais escura | Média | Médio | Algoritmo treinado predominantemente em adultos brancos (literatura) |
| R-04 | Viés etário — falso bloqueio em crianças <8 anos por mudança rápida da face | Média | Médio | Recadastro 6m mitiga, mas não elimina |
| R-05 | Falso positivo (aprovar criança errada por parecença) | Muito baixa | Alto | `SimilarityThreshold` baixo, irmãos gêmeos |
| R-06 | Falso negativo embaraçoso na frente de outros pais/colegas | Média | Baixo (emocional) | Iluminação ruim, mudança visual (óculos novo, machucado) |
| R-07 | Uso fora da finalidade — alguém na equipe consultar foto/template por curiosidade | Baixa | Alto | Acesso indevido ao dashboard ou ao bucket |
| R-08 | Deepfake injetado em câmera virtual no app Motorista | Muito baixa | Alto | Adversário sofisticado |
| R-09 | Coerção / consentimento "obrigatório" pela escola | Média | Alto | Escola condicionar matrícula ao uso biométrico |
| R-10 | Inferência indevida (idade aparente, etnia, expressão emocional) por API que retorna atributos além do match | Baixa | Médio | CompareFaces retorna bounding box + landmarks; não usamos, mas podem aparecer em log se mal feito |
| R-11 | Retenção excessiva por falha em job de expiração | Baixa | Médio | Job falha silenciosamente |
| R-12 | Compartilhamento involuntário via push notification (foto/atributos da criança em texto de notificação) | Baixa | Médio | Erro de template de notificação |

---

## 5. Medidas de mitigação

### 5.1 Técnicas
| Risco | Mitigação | Onde está implementado |
|---|---|---|
| R-01, R-02 | Templates cifrados com CMK dedicada (rotação anual), bucket com Block Public Access total, SSL-only via policy, KMS-only via policy | `StorageConstruct.cs` |
| R-01 | Lambda execution role least-privilege, sem `IndexFaces`/`SearchFaces` (proíbe 1:N) | `ApiConstruct.cs` (PolicyStatement explícito) |
| R-01 | Aurora em subnet isolada, security group restrito ao SG do Lambda | `DatabaseConstruct.cs` |
| R-03, R-04 | Recadastro semestral obrigatório; resultado `Inconclusive` cai em fallback (não em rejeição) | Backend (router + endpoints) |
| R-05 | `SimilarityThreshold` calibrado conservadoramente (a definir após Fase A); irmãos com cadastros distintos em famílias distintas | Aplicação |
| R-06 | UX do motorista nunca mostra "rejeitado" público — apenas "verifique com PIN" | App Motorista |
| R-07 | Audit log em toda leitura de template (`audit_log.action='TemplateRead'`); acesso ao dashboard com MFA opcional + IAM | Backend + Cognito |
| R-08 | SDK nativo do provedor valida assinatura de captura; liveness obrigatório; fallback se evidência insuficiente | SDKs + backend |
| R-09 | UX explicita que biometria é opcional; rejeição da família não impacta serviço; alerta na onboarding institucional | App Família + comunicação institucional |
| R-10 | Logs do backend não persistem bounding box ou attributes — apenas score e outcome | Convenção de logging (CONTRACTS 1.7) |
| R-11 | Alarme CloudWatch quando job de expiração falha; relatório anual de retenção | Observability + processo |
| R-12 | Template de notificação proibido de conter atributos físicos; testes unitários | App Família + dashboard |

### 5.2 Organizacionais
| Mitigação | Responsável | Periodicidade |
|---|---|---|
| Treinamento de motoristas sobre uso correto do app e privacidade | RH + DPO | Onboarding + anual |
| Treinamento da equipe interna em LGPD + biometria | DPO | Semestral |
| Revisão anual deste RIPD | DPO + jurídico | Anual |
| Auditoria interna em logs e DSR | DPO | Semestral |
| Canal de denúncia anônimo | Plataforma | Sempre |
| Política de não-coerção: cláusula em contrato com escolas | Comercial | Padrão |
| Política de exclusão imediata em até 7 dias úteis | Operação | Por solicitação |
| Comunicação em incidente: ANPD + titular em prazo razoável | DPO | Quando aplicável |

---

## 6. Análise de riscos residuais

Após aplicação das medidas:

| Risco residual | Nível | Aceito? | Justificativa |
|---|---|---|---|
| Falsos negativos em crianças <8 anos | Médio-Baixo | ⚠️ A decidir após Fase A | Depende de métricas reais; se ≥10% de inconclusive, suspender uso até retreino |
| Viés étnico residual | Médio | ⚠️ A decidir após Fase A | Monitorar segregando métricas; ser transparente nas comunicações |
| Risco de coerção de fato pela escola | Baixo | ✅ Sim | Mitigação contratual e UX explícita |
| Vazamento por incidente do provedor | Muito Baixo | ✅ Sim | DPA + SLA de comunicação |
| Inferência atributiva via deepfake adversarial | Muito Baixo | ✅ Sim | Custo do ataque > valor extraível |

**Riscos NÃO aceitos hoje** (bloqueiam Fase B até resolução):
- Operação **sem fallback** — biometria como único fator é proibida.
- Operação **sem consentimento expresso destacado** do responsável legal.
- Operação que armazene **foto bruta** em qualquer mídia durável.
- Operação que use os dados para **finalidade não declarada** ao titular.

---

## 7. Aprovação

| Função | Nome | Assinatura | Data |
|---|---|---|---|
| Encarregado (DPO) | `[A PREENCHER]` | `[A PREENCHER]` | `[A PREENCHER]` |
| Responsável técnico | `[A PREENCHER]` | `[A PREENCHER]` | `[A PREENCHER]` |
| Parecer jurídico externo | `[A PREENCHER]` | `[A PREENCHER]` | `[A PREENCHER]` |
| Patrocinador interno | `[A PREENCHER]` | `[A PREENCHER]` | `[A PREENCHER]` |

---

## 8. Versionamento deste RIPD

| Versão | Data | Mudanças | Aprovador |
|---|---|---|---|
| 0.1 | 2026-05-17 | Esqueleto inicial (POC) | `[A PREENCHER]` |
| `[Próxima]` | `[A PREENCHER após Fase A]` | Resultados POC + ajustes | `[A PREENCHER]` |

---

## 9. Compromisso explícito

A SafeRide Kids **não trata dados biométricos de criança real até que este RIPD seja aprovado e assinado**. A Fase A da POC restringe-se a adultos voluntários da equipe e familiares, com consentimento próprio escrito (template separado em `docs/poc/plano-de-teste.md`).
