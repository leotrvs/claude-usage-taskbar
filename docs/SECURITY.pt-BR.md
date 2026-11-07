# Segurança

[English](../SECURITY.md) | **Português (Brasil)**

## O que o app acessa

| Item | Acesso | Motivo |
|---|---|---|
| `~/.claude/settings.json` | Lê e altera só a entrada `statusLine`, com seu consentimento | Conecta ao Claude Code. Uma cópia `.bak` fica ao lado do arquivo. |
| Dados de status line do Claude Code | Lê em memória | Guarda só percentuais e horários de reinício dos limites |
| Comando `/usage` do Claude Code | Roda a cada 2 minutos e ao abrir o painel, enquanto conectado | Guarda só percentuais e horários de reinício dos limites. Não chama o modelo nem gasta tokens. |
| `~/.claude/sessions` | Lê tipo, ID do processo e status de cada sessão enquanto o painel está aberto | Diz ao mascote se o Claude Code está trabalhando. Nada é guardado. |
| `%LOCALAPPDATA%\ClaudeUsageTaskbar` | Lê e grava | Guarda o `usage.json`, o `history.csv` (24 horas de horários e percentuais de 5 horas) e sua status line anterior, se houver |
| `%LOCALAPPDATA%\Programs\ClaudeUsageTaskbar` | Lê e grava | Guarda o app instalado |
| `HKCU\Software\ClaudeUsageTaskbar` | Lê e grava | Guarda as preferências |
| Chave Run do Windows, lista de apps e atalhos | Lê e grava | Início com o Windows, desinstalação e atalhos |

## O que o app nunca faz

- Ler `~/.claude/.credentials.json` ou qualquer login, token ou cookie do Claude
- Chamar as APIs da Anthropic
- Guardar prompts, respostas, IDs de sessão ou caminhos de projetos
- Enviar analytics ou telemetria
- Exigir permissão de administrador

## Acesso à rede

O app faz um único tipo de conexão: ao abrir e depois a cada hora, baixa arquivos de release deste repositório no GitHub, a menos que você desligue as atualizações automáticas, e faz o mesmo quando você clica em **Verificar atualizações**. O GitHub pode ver seu endereço IP, como em qualquer download. Nenhum dado do Claude é enviado.

## Proteção das atualizações automáticas

Cada release tem um manifesto com a versão e o hash SHA-256, assinado com uma chave RSA de 3072 bits. A chave privada fica no computador do mantenedor, fora deste repositório e do GitHub, e a chave pública é embutida no app.

Antes de instalar uma atualização, o app confere que:

1. A assinatura do manifesto é válida.
2. A versão é mais nova que a instalada, o que bloqueia downgrade.
3. O app baixado bate com o hash assinado.

Se alguma verificação falhar, a atualização é descartada. Mesmo quem controlar o repositório no GitHub não consegue entregar uma atualização sem a chave privada.

## Confira um download

Cada release lista o hash SHA-256 do app. Compare com o seu arquivo:

```powershell
Get-FileHash .\ClaudeUsageTaskbar.exe -Algorithm SHA256
```

O app não tem um certificado pago de assinatura de código, então o Windows SmartScreen pode mostrar um aviso. A assinatura de atualização descrita acima é separada e não é vista pelo SmartScreen.

## Limites conhecidos

- O `usage.json` e o `history.csv` não são criptografados. Eles guardam só percentuais e horários, e qualquer programa rodando no seu usuário do Windows pode ler esses arquivos.
- Sua status line anterior, se houver, é executada com a mesma entrada que já recebia.
- Outros programas rodando no seu usuário do Windows poderiam alterar o app instalado. Isso vale para qualquer app instalado por usuário.

## Como relatar uma vulnerabilidade

Use o [relato privado de vulnerabilidades](https://github.com/leotrvs/claude-usage-taskbar/security/advisories/new), em vez de abrir uma issue pública.
