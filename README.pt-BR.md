# Claude Usage Taskbar

[English](README.md) | **Português (Brasil)** | [Español](README.es.md) | [Русский](README.ru.md)

Veja seus limites do Claude Code de relance na barra de tarefas do Windows e saiba quando vão acabar ou se vão durar até o reinício.

[![Baixar para Windows](https://img.shields.io/badge/Baixar_para_Windows-DE6E49?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/leotrvs/claude-usage-taskbar/releases/latest/download/ClaudeUsageTaskbar.exe)

![Os ícones e o painel acompanhando o uso enquanto ele sobe](docs/images/demo-pt-BR.gif)

> Projeto não oficial da comunidade. Não tem vínculo, aprovação nem suporte da Anthropic. "Claude" é uma marca da Anthropic.

## Recursos

- Ícones na barra de tarefas com o uso de **5 horas** e o **semanal**. Mostre os dois, ou só um
- Três estilos de ícone: **Círculo**, **Velocímetro** e **Ponteiro**
- Três modelos de cor e quatro temas, incluindo um que segue o Windows
- Painel com horários de reinício, previsão de ritmo e gráfico de 12 horas
- Mascote animado, com versões em pixels que reagem ao seu uso
- Alertas opcionais em 80% e 90% de um limite, e aviso quando um limite renova
- Menu com o botão direito nos ícones, com Abrir painel, Atualizar agora, Configurações e Encerrar
- Inglês e português do Brasil, seguindo o idioma do Windows por padrão
- Inicia com o Windows, com atalhos na Área de Trabalho e no menu Iniciar
- Atualizações automáticas assinadas
- **Nunca lê seu login nem seu token do Claude**

## Instalação

### Antes de começar

Você precisa de:

- Windows 10 ou 11
- [Claude Code](https://code.claude.com/docs) instalado e conectado a um plano Pro, Max ou Ultra. Para fazer login, rode `claude` uma vez em um terminal

Não é preciso deixar um terminal aberto.

### Passo a passo

1. **Baixe** o `ClaudeUsageTaskbar.exe` na [versão mais recente](https://github.com/leotrvs/claude-usage-taskbar/releases/latest).
2. **Abra o arquivo baixado.** O Windows pode mostrar um aviso do SmartScreen, porque o app não tem um certificado pago de assinatura de código. Escolha **Mais informações** e depois **Executar assim mesmo**. Se preferir, antes [confira o arquivo](docs/SECURITY.pt-BR.md#confira-um-download) ou [compile você mesmo](#compilar-o-código).
3. **Escolha Sim** na janela de instalação. O app é instalado só para o seu usuário do Windows e faz a conexão com o Claude Code para você. Depois, você pode apagar o arquivo baixado.
4. **Deixe os ícones visíveis.** O Windows esconde ícones novos da bandeja. Arraste-os do menu **^** para a barra de tarefas, ou vá em **Configurações > Personalização > Barra de Tarefas > Outros ícones da bandeja do sistema** e ligue-os. São dois ícones, um para cada limite.
5. **Aguarde alguns segundos.** Os percentuais aparecem nos ícones, o que significa que está funcionando.

## Como usar

1. **Leia os ícones.** O limite de 5 horas tem número branco, e o semanal tem número colorido. A cor muda em 70% e fica vermelha em 90%.
2. **Passe o mouse** sobre um ícone para ver o percentual e o horário de reinício.
3. **Clique** em um ícone para abrir o painel. Clique fora dele ou aperte **Esc** para fechar. **Clique com o botão direito** em um ícone para ver o menu com **Abrir painel**, **Atualizar agora**, **Configurações** e **Encerrar**.
4. **Escolha o estilo do ícone** em **Aparência**.
5. **Abra Configurações** no painel para o resto, e **Aparência** para cores e mascote.

**Encerrar** fecha só este app. Ele não sai da sua conta do Claude. Para reabrir, use o menu Iniciar ou a Área de Trabalho.

## O que você pode ver e mudar

### Estilos de ícone

![Estilos de ícone](docs/images/icon-styles.gif)

Escolha em **Aparência**. De cima para baixo:

- **Velocímetro:** um mostrador com o número dentro.
- **Círculo:** enche em sentido horário a partir do topo. É o padrão.
- **Ponteiro:** um mostrador com ponteiro.

### Gráfico de uso

![O gráfico com detalhes ao passar o mouse](docs/images/chart-pt-BR.gif)

O gráfico mostra o uso de 5 horas nas últimas 12 horas. Passe o mouse sobre ele para ver o horário, o percentual naquele momento e quanto subiu desde a leitura anterior.

O seletor **12h**, **24h** e **7d** acima do gráfico muda o período. A visão de 7 dias acompanha o limite semanal. Linhas pontilhadas marcam onde um limite recomeçou. Abaixo do gráfico, **Hoje** mostra o maior percentual que a sessão de 5 horas atingiu desde a meia-noite.

### Previsão de ritmo

- **Limite de 5 horas:** a previsão combina a média de toda a janela com a tendência recente. Longe do limite, um pico isolado não domina o resultado; perto do limite, os últimos minutos pesam mais. Ela aparece a partir de 5% de uso e 15 minutos de dados.
- **Limite semanal:** compara seu uso com um ritmo uniforme ao longo da semana. Mostra uma diferença, não uma previsão.

### Cores e temas

![Azul com grafite, verde com preto e roxo com claro](docs/images/themes-pt-BR.gif)

Em **Aparência**:

- **Cores:** azul por padrão, ou verde ou roxo. Valem para os ícones e as barras.
- **Tema:** auto por padrão, ou grafite, preto ou claro. O Auto segue o modo do Windows: claro quando ele está claro, e preto quando está escuro.

### Mascote

Por padrão, o painel mostra o mascote **Notebook** trabalhando no notebook:

<img src="assets/mascots/default.gif" alt="O mascote Notebook trabalhando no notebook" width="200">

O mascote reage ao seu uso. O Notebook usa esta animação enquanto trabalha e os desenhos em pixels nos outros estados. De cima para baixo:

![Os estados do mascote](docs/images/mascot-moods.gif)

| Estado | Quando |
|---|---|
| Dormindo | Sem sessão aberta e sem uso recente, ou ainda sem dados |
| Descansando | Há uma sessão aberta e parada, ou o último uso foi há menos de 45 minutos |
| Trabalhando no notebook | Uma sessão do Claude Code está trabalhando, ou a porcentagem subiu nos últimos 10 minutos |
| Cansado e suando | Está trabalhando e um limite está em 90% ou mais |
| Caído | Um limite chegou a 100% |
| Dormindo de touca | Igual a dormindo, de madrugada |
| Perguntando com uma interrogação | Uma sessão precisa da sua permissão ou resposta |
| Em pânico | A previsão indica que o limite acaba em menos de 30 minutos |
| Tomando café | Está trabalhando há mais de 2 horas |
| Malabarismo e musculação | Duas ou mais sessões trabalhando ao mesmo tempo. Quanto mais sessões, mais frequente |

Ele também reage ao que acontece. Quando um limite renova, finca uma bandeira quadriculada que balança ao vento, com confete caindo ao redor. Quando uma atualização termina, ele salta e se achata numa chuva de confete. Acena ou pula quando você passa o mouse, dá um pulinho de vez em quando enquanto descansa e reage ao clique. Enquanto trabalha, ele também pensa, anota, lê e se espreguiça de vez em quando. Com duas ou mais sessões trabalhando, ele faz malabarismo e levanta pesos, mais vezes quanto mais sessões houver, e em 100% às vezes fica achatado e tonto.

![Os jeitos de trabalhar: o notebook, pensar, anotar, ler e se espreguiçar](docs/images/mascot-working.gif)

![Os movimentos novos: pulo, saltar, bandeira, pesos e achatado](docs/images/mascot-moves.gif)

Em **Aparência** você escolhe o visual:

- **Notebook:** a animação original enquanto trabalha, e os desenhos em pixels nos outros estados. É o padrão.
- **Clássico:** os desenhos em pixels o tempo todo.
- **Natal**, **Fone**, **Chef**, **Cientista**, **Detetive**, **Inverno** e **Piloto:** o mesmo mascote com um outfit. O Chef diz `im cooking` enquanto trabalha, e o Detetive às vezes procura com a lupa.

![Os outfits](docs/images/mascot-outfits.gif)

Alguns outfits reagem: o Cientista mexe um frasco enquanto trabalha e os óculos piscam em vermelho no pânico, a lupa do Detetive brilha quando uma sessão espera por você, o cachecol do Inverno balança e o Piloto agita a bandeira quadriculada quando um limite renova. Clique no mascote cinco vezes seguidas e ele se derrete de amor.

![As reações dos outfits e a animação de amor](docs/images/mascot-reactions.gif)

Com **Fantasias de datas especiais** ligado, o mascote sem outfit se veste sozinho no Natal, no Halloween e no Carnaval. Quando anda, às vezes para para olhar as barras de uso se um limite está perto, ou vai procurar onde estava o seu cursor.

Quando está descansando, o mascote de vez em quando dá uma caminhada curta pelo espaço vazio do cabeçalho do painel e volta antes de chegar ao texto.

Em pânico, ele de vez em quando corre de um lado para o outro do cabeçalho.

O Piloto sai com estilo: uma bandeira quadriculada balança, um carro de Fórmula 1 chega, e ele monta, corre pelo cabeçalho e volta.

![O mascote andando pelo cabeçalho](docs/images/mascot-walks.gif)

O mascote sem outfit anda com uma bandeira quadriculada nas costas, e a maioria dos outfits tem uma caminhada própria. O Natal anda de trenó, o Fone e o Carnaval dançam, o Chef leva uma bandeja coberta, o Cientista faz um experimento que explode no fim, o Inverno patina e o Halloween voa de vassoura:

![Natal, Fone, Chef e Cientista nas suas caminhadas](docs/images/mascot-acts-1.gif)

![Inverno, Halloween e Carnaval nas suas caminhadas](docs/images/mascot-acts-2.gif)

Este é um projeto de fã, sem vínculo ou aprovação da Anthropic, e o personagem pertence aos seus donos. A licença MIT não cobre o personagem.

### Configurações

<img src="docs/images/settings-pt-BR.gif" alt="Página de Configurações / Página de Aparência" width="300">

| Opção | O que faz |
|---|---|
| Idioma | Automático, inglês ou português |
| Ícones exibidos | Os dois limites, só 5 horas ou só semanal |
| Iniciar com o Windows | Abre o app quando você entra no Windows |
| Atualizações automáticas | Instala sozinho as atualizações assinadas |
| Notificações | Alertas em 80% e 90% de um limite, e avisos quando um limite renova ou uma atualização é instalada |
| Conectado ao Claude Code | Liga ou desliga a conexão |
| Verificar atualizações | Procura uma atualização agora. O ícone gira enquanto verifica e vira um símbolo de baixar quando há uma pronta |

## Como a conexão funciona

O app lê seus limites de duas formas oficiais:

- **Comando `/usage`:** a cada 2 minutos, e ao abrir o painel, ele roda `claude -p /usage` em segundo plano, com os mesmos números que você vê no Claude Code. Isso não chama o modelo nem gasta tokens.
- **[Status line](https://code.claude.com/docs/en/statusline):** na instalação, ele adiciona uma entrada `statusLine` em `~/.claude/settings.json` e salva um backup ao lado. O Claude Code então envia números novos a cada resposta em um terminal.

Nos dois casos, o app guarda só os percentuais e os horários de reinício dos limites de 5 horas e semanal em `%LOCALAPPDATA%\ClaudeUsageTaskbar`, e descarta o resto. O widget lê esse arquivo.

Bom saber:

- **O uso de qualquer lugar conta.** claude.ai, app desktop e terminais aparecem em até 2 minutos.
- **Uma status line que você já tenha é mantida.** Ela continua rodando e mostrando o mesmo de antes. Ao desinstalar, ela é restaurada.
- **Para desconectar,** desligue **Conectado ao Claude Code** em Configurações.

## Segurança e privacidade

- O app não lê seu login, seu token nem seu arquivo de credenciais.
- Ele não guarda seus prompts, dados de sessão nem caminhos de projetos.
- Ele não tem analytics, telemetria nem conta.
- O acesso à rede do próprio app é só a verificação de atualizações assinadas no GitHub, que você pode desligar. O comando `/usage` é do Claude Code, que usa seu login sem mostrá-lo ao app.

Os detalhes estão em [docs/SECURITY.pt-BR.md](docs/SECURITY.pt-BR.md).

## Atualizações

Ao abrir e depois a cada hora, o app verifica a versão mais recente no GitHub. Ele só instala uma atualização se a assinatura digital bater com a chave embutida no app, então um arquivo adulterado é rejeitado. Você pode desligar isso em Configurações, onde **Verificar atualizações** também faz uma verificação na hora. A verificação ao abrir instala a atualização na hora, sem perguntar. As verificações seguintes esperam o painel fechar e, enquanto isso, mostram um link para instalar. Uma notificação confirma a atualização, a menos que você desligue **Notificações**.

## Desinstalação

Vá em **Configurações do Windows > Aplicativos > Aplicativos instalados > Claude Usage Taskbar > Desinstalar**. Isso remove o app, os atalhos e as configurações, e restaura sua status line do Claude Code.

## Solução de problemas

| Problema | O que fazer |
|---|---|
| "Aguardando o Claude Code" | Aguarde alguns segundos. Se continuar, rode `claude` em um terminal para confirmar que o Claude Code está instalado e com login feito. |
| "Claude Code não encontrado" | Instale o [Claude Code](https://code.claude.com/docs) e rode `claude` em um terminal para fazer login. |
| Os percentuais continuam sem aparecer | Confira se **Conectado ao Claude Code** está ligado e rode `claude -p /usage` em um terminal. Os dados existem nos planos Pro, Max e Ultra. |
| Os ícones estão escondidos | Siga o passo 4 do [passo a passo](#passo-a-passo). |
| Não foi possível ligar a conexão | O `~/.claude/settings.json` pode ter um JSON inválido. Corrija e tente de novo. |
| "Atrasado" em amarelo | As últimas leituras falharam, então os números podem estar antigos. O app tenta de novo a cada 2 minutos. |

## Compilar o código

Não é preciso instalar SDK, porque o Windows já inclui o compilador C#.

```powershell
git clone https://github.com/leotrvs/claude-usage-taskbar.git
cd claude-usage-taskbar
pwsh -File scripts/build.ps1
```

O app é gerado em `bin\ClaudeUsageTaskbar.exe`. Use `--portable` para rodar sem instalar.

Quem quiser contribuir deve ler o [CONTRIBUTING.md](CONTRIBUTING.md).

## Créditos

O painel usa a fonte [Inter](https://github.com/rsms/inter), sob a licença SIL Open Font License. Veja [assets/fonts/NOTICE.md](assets/fonts/NOTICE.md).

## Licença

[MIT](LICENSE)
