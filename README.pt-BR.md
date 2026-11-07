# Claude Usage Taskbar

[English](README.md) | **Português (Brasil)**

Veja os limites de uso do Claude Code na barra de tarefas do Windows, sem precisar abrir nada.

![O painel de uso](docs/images/panel-pt-BR.png)

> Projeto não oficial da comunidade. Não tem vínculo, aprovação nem suporte da Anthropic. "Claude" é uma marca da Anthropic.

## Recursos

- Ícones na barra de tarefas com o percentual de uso de **5 horas** e o **semanal**. Mostre os dois, ou só um
- Três estilos de ícone: **Velocímetro**, um mostrador com o número dentro, **Círculo**, que enche em sentido horário a partir do topo, e **Ponteiro**, um mostrador com ponteiro
- Cores por nível: verde, azul ou roxo, mudando em 70% e ficando vermelho em 90%
- Ícones sem fundo e painel grafite ou preto
- Painel escuro com horários de reinício, previsão de ritmo e gráfico de 12 horas com detalhes ao passar o mouse
- Alertas opcionais em 80% e 90% de um limite
- Inglês e português do Brasil, seguindo o idioma do Windows por padrão
- Inicia com o Windows, com atalhos na Área de Trabalho e no menu Iniciar
- Atualizações automáticas assinadas
- **Nunca lê seu login nem seu token do Claude**

![Estilos de ícone](docs/images/icon-styles.png)

## Requisitos

- Windows 10 ou 11
- [Claude Code](https://code.claude.com/docs) em um **terminal**, conectado a um plano Pro, Max ou Ultra

> **O app desktop do Claude não alimenta este widget.** Nos testes, a aba Code não executou a status line, então os percentuais só aparecem enquanto você usa o Claude Code em um terminal.

## Instalação

1. Se ainda não tiver, instale o Claude Code, rode `claude` em um terminal e faça login.
2. Baixe o `ClaudeUsageTaskbar.exe` na [versão mais recente](https://github.com/ggleoo/claude-usage-taskbar/releases/latest).
3. Execute o arquivo. O Windows pode mostrar um aviso do SmartScreen, porque o app não tem um certificado pago de assinatura de código. Escolha **Mais informações** e depois **Executar assim mesmo**. Se preferir, antes [confira o arquivo](docs/SECURITY.pt-BR.md#confira-um-download) ou [compile você mesmo](#compilar-o-código).
4. Escolha **Sim** para instalar. O app é instalado só para o seu usuário do Windows, e depois você pode apagar o arquivo baixado.
5. Deixe os ícones visíveis: arraste-os do menu **^** para a barra de tarefas, ou vá em **Configurações > Personalização > Barra de Tarefas > Outros ícones da bandeja do sistema**.
6. Envie uma mensagem no Claude Code em um terminal. Os percentuais aparecem logo depois da resposta.

O instalador faz a conexão com o Claude Code para você. Veja [como a conexão funciona](#como-a-conexão-funciona).

## Como usar

- **Passe o mouse** sobre um ícone para ver o percentual e o horário de reinício.
- **Clique** em um ícone para abrir o painel. É lá que você escolhe o estilo do ícone.
- **Configurações**, no painel, reúne idioma, ícones exibidos, atualizações automáticas, alertas de uso, iniciar com o Windows, a conexão com o Claude Code e **Aparência**, com o modelo de cor e o tema do painel.
- **Fechar widget** fecha só este app. Ele não sai da sua conta do Claude. Para reabrir, use o menu Iniciar ou a Área de Trabalho.

Nos ícones, o limite de 5 horas tem número branco, e o semanal tem número colorido.

### Previsão de ritmo

- **Limite de 5 horas:** a previsão combina a média de toda a janela com a tendência recente, para que um pico isolado não domine o resultado. Ela aparece a partir de 5% de uso e 15 minutos de dados.
- **Limite semanal:** compara seu uso com um ritmo uniforme ao longo da semana. Mostra uma diferença, não uma previsão.

### Mascote

O painel mostra um mascote animado mexendo no notebook. Este é um projeto de fã, sem vínculo ou aprovação da Anthropic, e o personagem pertence aos seus donos. A licença MIT não cobre o personagem.

## Como a conexão funciona

O Claude Code pode enviar dados da sessão para um comando de [status line](https://code.claude.com/docs/en/statusline), e esses dados incluem os percentuais dos limites. O app usa esse recurso oficial:

1. Na instalação, ele adiciona uma entrada `statusLine` em `~/.claude/settings.json` e salva um backup do arquivo ao lado dele.
2. O Claude Code executa o app em segundo plano e envia os dados da sessão.
3. O app guarda só os percentuais e os horários de reinício dos limites de 5 horas e semanal em `%LOCALAPPDATA%\ClaudeUsageTaskbar`, e descarta o resto.
4. O widget lê esse arquivo.

Bom saber:

- **Os percentuais só atualizam com o Claude Code rodando em um terminal.** Eles mudam a cada resposta, então o uso feito em outros lugares, como o claude.ai ou o app desktop, aparece na sua próxima resposta no terminal.
- **Uma status line que você já tenha é mantida.** Ela continua rodando e mostrando o mesmo de antes. Ao desinstalar, ela é restaurada.
- **Para desconectar,** desligue **Conectado ao Claude Code** em Configurações.

## Segurança e privacidade

- O app não lê seu login, seu token nem seu arquivo de credenciais.
- Ele não guarda seus prompts, dados de sessão nem caminhos de projetos.
- Ele não tem analytics, telemetria nem conta.
- O único acesso à rede é a verificação de atualizações assinadas no GitHub, que você pode desligar.

Os detalhes estão em [docs/SECURITY.pt-BR.md](docs/SECURITY.pt-BR.md).

## Créditos

O painel usa a fonte [Inter](https://github.com/rsms/inter), sob a licença SIL Open Font License. Veja [assets/fonts/NOTICE.md](assets/fonts/NOTICE.md).

## Atualizações

A cada 6 horas, o app verifica a versão mais recente no GitHub. Ele só instala uma atualização se a assinatura digital bater com a chave embutida no app, então um arquivo adulterado é rejeitado. Você pode desligar isso em Configurações, onde **Verificar atualizações** também faz uma verificação na hora. Se o painel estiver aberto quando uma atualização for encontrada, ele mostra um link para instalar.

## Desinstalação

Vá em **Configurações do Windows > Aplicativos > Aplicativos instalados > Claude Usage Taskbar > Desinstalar**. Isso remove o app, os atalhos e as configurações, e restaura sua status line do Claude Code.

## Solução de problemas

| Problema | O que fazer |
|---|---|
| "Aguardando o Claude Code" | Envie uma mensagem no Claude Code em um terminal. Os dados de limite chegam depois da primeira resposta. O app desktop não fornece esses dados. |
| Os percentuais continuam sem aparecer | Confira se **Conectado ao Claude Code** está ligado e reinicie o Claude Code. Os dados existem nos planos Pro, Max e Ultra. |
| Os ícones estão escondidos | Siga o passo 5 da [Instalação](#instalação). |
| Não foi possível ligar a conexão | O `~/.claude/settings.json` pode ter um JSON inválido. Corrija e tente de novo. |
| Um percentual parece antigo | É o último valor informado pelo Claude Code. Ele muda na próxima resposta. |

## Compilar o código

Não é preciso instalar SDK, porque o Windows já inclui o compilador C#.

```powershell
git clone https://github.com/ggleoo/claude-usage-taskbar.git
cd claude-usage-taskbar
pwsh -File scripts/build.ps1
```

O app é gerado em `bin\ClaudeUsageTaskbar.exe`. Use `--portable` para rodar sem instalar.

Quem quiser contribuir deve ler o [CONTRIBUTING.md](CONTRIBUTING.md).

## Licença

[MIT](LICENSE)
