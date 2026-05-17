# Termo de consentimento — biometria facial no embarque (POC)

> **Fonte canônica.** Este arquivo contém o texto integral exibido ao responsável legal durante o enrollment biométrico. Toda alteração textual exige nova versão (`consent_text_version`) e novo `consent_text_hash` salvo em `consent.consent_text_hash` (CONTRACTS Seção 5). O aplicativo Família deve **copiar este texto literalmente** para `Resources/Raw/consent-text-v1.txt`.
>
> **Versão atual:** `v1`
> **Última atualização:** 2026-05-17
> **Encarregado de dados (DPO) — placeholder:** `dpo@saferidekids.example` *(substituir por endereço institucional real antes de qualquer Fase B)*

---

## 1. Texto exibido ao responsável legal (Versão v1 — PT-BR)

> **Antes de prosseguir, leia este termo com atenção. Você é responsável legal de uma criança e está autorizando a coleta de dados biométricos faciais dela. Esta é uma decisão importante.**

**1. Quem somos.** Este aplicativo é operado pela SafeRide Kids ("plataforma"). O controlador dos dados pessoais é a SafeRide Kids; o motorista é operador no momento do embarque; AWS (Amazon Web Services) e Unico IDCloud são operadores técnicos que processam os dados biométricos exclusivamente para a finalidade descrita abaixo.

**2. O que vamos coletar.** Você está autorizando que sejam capturadas de 3 a 5 fotografias do rosto da criança identificada como **{{nomeCrianca}}**, nascida em {{anoNascimento}}. Essas fotografias são **dados pessoais sensíveis** porque permitem identificar unicamente uma pessoa natural (Lei Geral de Proteção de Dados — Lei nº 13.709/2018, Art. 5º, II). Por se tratar de criança, aplica-se proteção reforçada da LGPD (Art. 14) e da Resolução CD/ANPD nº 2/2022 (tratamento de dados pessoais de crianças e adolescentes).

**3. Para que vamos usar.** O único uso autorizado é **comparar, no momento do embarque, o rosto da criança apresentada ao motorista com as fotos cadastradas, para confirmar se é a criança esperada naquela parada.** Esta operação é conhecida como **verificação 1:1**.

**4. O que NÃO vamos fazer com esses dados.** Estes dados **não serão usados**:
- para identificar a criança em uma base de outras pessoas (verificação 1:N);
- para qualquer finalidade de marketing, publicidade ou perfilamento;
- para treinar modelos de inteligência artificial da plataforma ou de terceiros;
- para vender, ceder ou compartilhar com qualquer outro propósito que não a finalidade declarada acima.

**5. Onde os dados ficam.**
- As **fotografias originais nunca são gravadas em arquivo permanente**, nem no seu celular, nem no celular do motorista, nem nos nossos servidores. Elas existem apenas em memória durante o cadastro e a verificação.
- A partir das fotos é gerada uma **representação matemática (template biométrico)** — não é possível reconstruir o rosto a partir dela. Essa representação é cifrada e armazenada em data center da Amazon Web Services no Brasil (região São Paulo).
- O motorista vê apenas o nome da criança e o resultado "aprovado" ou "verificação adicional necessária". Ele não tem acesso às fotos cadastradas.

**6. Quem mais terá acesso.**
- **Provedores técnicos de biometria** que fazem a comparação: AWS Rekognition e Unico IDCloud. Ambos têm Termo de Operador firmado e compromissos de segurança equivalentes aos exigidos pela LGPD. Esses provedores **não retêm a foto** depois da verificação (apenas o template, e apenas se você consentir o uso continuado).
- **Nosso encarregado de dados (DPO)**, em casos de auditoria ou exercício de direitos.

**7. Por quanto tempo guardamos.**
- O cadastro biométrico expira automaticamente em **6 meses**. Após esse prazo, o template é apagado dos nossos sistemas e dos provedores. Será necessário um novo cadastro para continuar usando o embarque facial.
- Registros do uso (data e hora dos check-ins, resultado, motorista, parada) ficam por **90 dias ativos** e **6 anos arquivados** para atender obrigações legais (Marco Civil da Internet) e para eventual contestação.
- Você pode pedir a exclusão a qualquer momento (vide item 9).

**8. A biometria é obrigatória?** **Não.** O embarque facial é opcional. Mesmo após o consentimento, **toda viagem tem um método alternativo** (PIN enviado ao seu celular ou conferência manual pelo motorista). Se você não quiser usar biometria, basta não realizar este cadastro — a criança continua podendo ser embarcada normalmente pelos métodos alternativos.

**9. Seus direitos como responsável legal pelo titular.** A LGPD garante a você (em nome da criança) os direitos de:
- **Confirmar** que tratamos dados da criança;
- **Acessar** os dados que tratamos;
- **Corrigir** dados incompletos, desatualizados ou inexatos;
- **Anonimizar, bloquear ou eliminar** dados desnecessários ou tratados em desconformidade;
- **Portar** os dados a outro fornecedor;
- **Eliminar** os dados tratados com base em consentimento, exceto nas hipóteses do Art. 16 da LGPD;
- **Revisar** decisões automatizadas;
- **Revogar** este consentimento a qualquer momento, sem ônus.

**Como exercer.** Envie e-mail para `dpo@saferidekids.example` ou utilize a opção "Solicitar exclusão de dados" dentro do aplicativo. Responderemos em até 7 dias úteis (LGPD permite até 15 dias).

**10. Em caso de incidente.** Se acontecer qualquer incidente de segurança envolvendo os dados da criança, comunicaremos você e a Autoridade Nacional de Proteção de Dados (ANPD) em prazo razoável, conforme exigido pela LGPD.

**11. Melhor interesse da criança.** A SafeRide Kids se compromete, em conformidade com a Resolução CD/ANPD nº 2/2022, a tratar os dados biométricos da criança **sempre observando o seu melhor interesse**, com transparência, finalidade específica e mínimo necessário.

**12. Base legal.** Este tratamento ocorre com base no **consentimento expresso, destacado, do responsável legal** (LGPD Art. 7º, I e Art. 11, I), e em conformidade com o Art. 14, §1º (tratamento de dados de crianças).

---

## 2. Bloco de assinatura

O aplicativo coleta e persiste em `consent` (Aurora) os seguintes campos:

| Campo no banco | O que é exibido / pedido | Tratamento técnico |
|---|---|---|
| `granted_by` | Nome completo do responsável (auto-declarado) | Plaintext (necessário para identificação humana) |
| `responsavel_cpf_hash` | CPF (digitado) | `HMAC-SHA256(cpf, salt_tenant)` — **CPF claro nunca é persistido**; exibido na tela mascarado `***.***.***-12` |
| `granted_at` | Timestamp do clique no botão "Aceito" | UTC ISO-8601 |
| `scope` | "biometria_checkin" | Fixo nesta versão |
| `consent_text_hash` | hash deste arquivo `v1` | `SHA-256` do bloco "Texto exibido ao responsável legal" exato |
| `ip_address` | IP do dispositivo | Persistido (auditoria) |
| `user_agent` | User-Agent do app | Persistido |
| `signed_text_storage_key` | Chave S3 do PDF gerado | PDF inclui o texto + bloco de assinatura + hash |

O PDF gerado e armazenado em S3 (`signed_text_storage_key`) deve conter, ao final:

```
Responsável: {{nome do responsável}}
CPF: ***.***.***-{{4 últimos dígitos}}
Aceito em: {{timestamp}}
IP de origem: {{ip}}
Versão do termo: v1
Hash do texto: {{consent_text_hash}}
ID do consentimento: {{consent.id}}
```

---

## 3. Operacional — botões e affordances no app Família

| Tela | Elemento | Texto |
|---|---|---|
| Onboarding biometria | Cabeçalho | "Cadastro facial — opcional" |
| Onboarding biometria | Botão primário (após scroll completo do texto) | "Aceito e quero cadastrar agora" |
| Onboarding biometria | Botão secundário | "Não quero usar biometria" |
| Onboarding biometria | Link rodapé | "Como funciona o método alternativo (PIN)" |
| Configurações > Privacidade | Item | "Revogar consentimento biométrico" |
| Configurações > Privacidade | Item | "Solicitar exclusão de dados (LGPD)" |

**Regras de UX obrigatórias:**

1. O botão "Aceito" só fica habilitado após o usuário rolar **até o final do texto** (ou abrir um modal de leitura).
2. O termo é exibido **antes** de qualquer captura de imagem — não é possível chegar à tela de captura sem o aceite.
3. A opção "Não quero usar biometria" deve estar visualmente equivalente em prominência ao "Aceito" (não escondida em link cinza claro). LGPD veta consentimento por "indução".
4. Após o aceite, o usuário recebe e-mail com o PDF anexado, contendo o texto consentido e o hash.

---

## 4. Versionamento

| Versão | Data | Mudanças | Hash de referência |
|---|---|---|---|
| `v1` | 2026-05-17 | Versão inicial POC | Calculado em build a partir do bloco "Texto exibido ao responsável legal" deste arquivo |

Qualquer alteração no texto exige:
1. Nova entrada nesta tabela com `v2`, data, descrição do delta.
2. Recálculo do hash.
3. Notificação aos responsáveis com consentimento ativo e solicitação de re-aceite.
4. Bloqueio do uso biométrico até re-aceite (cair em fallback PIN).
