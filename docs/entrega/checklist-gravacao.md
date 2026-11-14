# Checklist de gravação do vídeo

Use junto com o [roteiro](roteiro-video.md). Marque cada item antes de começar a gravar. Os comandos são para o
**Git Bash**, salvo quando indicado.

> **Atenção.** O `docker-compose.yml` fixa `name: joya-pedidos`. Dentro do clone `joya-video`, qualquer
> `docker compose` **sem** `-p joya-video` (ou sem `COMPOSE_PROJECT_NAME=joya-video` exportado) age sobre o projeto
> **principal**. Um `docker compose down -v` sem `-p` apagaria o volume `joya-pedidos_dados-sql`: os 35 pedidos, o
> login `joya_mcp` e o banco que o MCP consulta.

## 1. Tela, fonte e áudio

- [ ] Resolução da tela em 1920 × 1080 (ou gravação só da janela nessa resolução), escala do Windows em 100% ou 125%.
- [ ] Terminais com fonte de 16 a 18 pt, janela maximizada, tema de alto contraste.
- [ ] Editor com zoom de 125% a 150%; minimapa e barras laterais desnecessárias ocultos.
- [ ] Navegador com zoom em 110% a 125%, sem barra de favoritos e sem extensões visíveis.
- [ ] Microfone testado com uma gravação de 10 s: volume estável, sem eco, sem ruído de fundo.
- [ ] Ambiente silencioso; celular no silencioso e longe do microfone.

## 2. Notificações e privacidade

- [ ] "Não perturbe" (Assistente de Foco) ligado no Windows.
- [ ] E-mail, mensageiros, agenda e qualquer app que mostre notificações fechados.
- [ ] Nenhuma janela com dados pessoais aberta (e-mail, documentos, outras sessões do Claude).
- [ ] GitHub aberto numa **janela anônima** (sem avatar, notificações ou repositórios privados à vista), depois de o
      repositório estar público.
- [ ] Terminais novos para a gravação, com `history -c` e prompt neutro (`PS1='$ '`), sem usuário e máquina no prompt.
      Não usar a seta para cima nem `Ctrl+R` durante a gravação.

## 3. Credenciais ocultas

- [ ] O `.env` (se existir) **não** será aberto, e a tela "Variáveis de ambiente" do Windows também não.
- [ ] Nenhum comando que imprima `JOYA_MCP_SENHA` ou outra variável sensível.
- [ ] Não rodar `docker compose config` nem `docker inspect`, e não abrir as abas Inspect ou Env do Docker Desktop:
      eles mostram `MSSQL_SA_PASSWORD`, `SQLCMDPASSWORD` e a string de conexão da API.
- [ ] Conferido fora da gravação que o `/mcp` do Claude Code não mostra a DSN expandida; se mostrar, não abrir o
      detalhe do servidor durante o vídeo.
- [ ] Só pode aparecer a senha padrão do `sa` de desenvolvimento, que é pública e rotulada como insegura no README.

## 4. Docker, Claude Code e terminais

- [ ] Docker Desktop no ar. Durante a gravação ficam no ar o SQL Server principal e o do `joya-video`; a suíte de
      testes (que sobe mais dois SQL Servers ao mesmo tempo) roda **antes** da gravação, nunca durante.
- [ ] Imagens base já baixadas (`docker images` mostra `mssql/server:2022-CU28-ubuntu-22.04`, `dotnet/sdk:10.0.404-noble`,
      `dotnet/aspnet:10.0.15-noble`, `node:24.22.3-alpine3.24` e `nginx:1.30.6-alpine3.24`).
- [ ] `JOYA_MCP_SENHA` persistida no ambiente do usuário, sem exibir o valor. No PowerShell, o comando abaixo deve
      responder `True`:
      `[bool][Environment]::GetEnvironmentVariable('JOYA_MCP_SENHA','User')`
      Em 13/11/2026 ele respondia `False`: a variável existia só no processo do app. Persista-a pela tela "Variáveis
      de ambiente" do Windows ou pelo comando com `Read-Host -AsSecureString` do README (seção do MCP, passo 2) e
      reinicie o Claude Code; sem isso, reiniciar o app deixa o MCP sem senha.
- [ ] Claude Code (app desktop) aberto no repositório principal **5 minutos antes**, na sessão que será gravada, com o
      `/mcp` mostrando `sqlserver-joya` conectado e a consulta do bloco D executada uma vez. Se ficar `failed`,
      reconectar em `/mcp`; se o tempo de partida passar de 30 s, definir `MCP_TIMEOUT` (README, seção do MCP) antes de
      abrir o app.
- [ ] Modo de permissão do Claude Code definido no ensaio, para que pedidos de confirmação (edição no bloco C,
      `execute_sql` no bloco D) não interrompam a gravação.
- [ ] T1: Git Bash no repositório principal, árvore limpa (`git status -sb` → `## main...origin/main`).
- [ ] T2: Git Bash no clone `joya-video` (seção 5), com `COMPOSE_PROJECT_NAME` exportado e os comandos dos blocos E e G
      prontos para colar.
- [ ] T3: saída da suíte completa (`dotnet test backend/JoyaPedidos.slnx`) executada pouco antes da gravação, com
      `total: 254` e `falhou: 0` na tela. Na publicação da E11 a suíte levou 3 min 3 s e 3 min 9 s nesta máquina.
- [ ] Abas abertas no editor: `docs/spec.md`, `docs/plan.md`, `CLAUDE.md`, `.claude/settings.json`, `.claude/rules/`,
      `.claude/agents/`, `.claude/skills/novo-endpoint/SKILL.md`, `.mcp.json`, `docker-compose.yml`, os dois arquivos de
      `database/consultas/`, `RepositorioPedidos.cs`, `ConcorrenciaPedidosTestes.cs` e o README.

## 5. Ambiente isolado para a demonstração

Toda operação que cria pedidos (blocos F e G) usa o projeto Compose descartável `joya-video`, num clone limpo. O
banco principal (`joya-pedidos`, 35 pedidos em 13/11/2026) **não** recebe nenhuma escrita.

**No T1 (repositório principal)**, pare só a API e o frontend principais. O `sqlserver` principal continua no ar, com
a porta do MCP:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml stop api web
```

**No T2**, numa pasta temporária fora do repositório, depois que o repositório estiver publicado:

```bash
git clone https://github.com/thiagom3005/joya-mini-gestao-pedidos.git joya-video && cd joya-video
```

```bash
export COMPOSE_PROJECT_NAME=joya-video
```

```bash
ls -a
```

```bash
docker compose -p joya-video up --build -d
```

Grave esses quatro comandos e a subida (cerca de 75 s) para o trecho acelerado do bloco E. Espere a API ficar no ar:

```bash
docker compose -p joya-video ps -a
```

`db-init` com `Exited (0)` e `api` e `web` `Up`.

**Conferência de segurança antes dos blocos F e G** (no T2):

```bash
curl -s "http://localhost:5080/pedidos?pagina=1&tamanhoPagina=1" | grep -o '"totalItens":[0-9]*'
```

O resultado precisa ser `"totalItens":33` (seed limpo). Se aparecer 35, a porta está apontando para o banco principal:
**não** crie pedidos e revise os passos anteriores.

**Para refazer o ambiente depois do ensaio** (no T2, sempre com `-p`): confira primeiro o alvo, que deve listar só
containers `joya-video-*`, e só então recrie:

```bash
docker compose -p joya-video ps -a
```

```bash
docker compose -p joya-video down -v
```

```bash
docker compose -p joya-video up --build -d
```

**Depois da gravação**, no T2 (apaga só o volume `joya-video_dados-sql` e as imagens locais `joya-video-*`):

```bash
docker compose -p joya-video ps -a
```

```bash
docker compose -p joya-video down -v --rmi local
```

No T1, religue a API e o frontend principais com os dois arquivos de compose:

```bash
docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --no-deps api web
```

Quando `docker compose ps -a` (no T1) mostrar `api` `Up`, confira que o banco principal continua com 35 pedidos:

```bash
curl -s "http://localhost:5080/pedidos?pagina=1&tamanhoPagina=1" | grep -o '"totalItens":[0-9]*'
```

Por fim, apague a pasta do clone `joya-video`.

## 6. Navegador e links

- [ ] Aba 1: http://localhost:3000 (ambiente `joya-video`), na página 1, sem filtros.
- [ ] Aba 2 (janela anônima): https://github.com/thiagom3005/joya-mini-gestao-pedidos (README e página de commits).
- [ ] Links do README conferidos (sumário e arquivos) depois da publicação.

## 7. Ensaio dos comandos

Execute cada passo do roteiro uma vez, fora da gravação, e confira o resultado esperado. Depois do ensaio, refaça o
ambiente `joya-video` (seção 5) para gravar com o seed limpo.

- [ ] `git log --oneline --reverse | head -3` (spec e plano antes do código).
- [ ] Demonstração do hook no Claude Code e `git status --short` vazio no final.
- [ ] `git log --oneline --no-walk 29068b6 54489f7 6d64220 a3772de` e, se for usar, o `grep` da transcrição da E6.
- [ ] Consultas do MCP do bloco D (identidade e índices).
- [ ] `docker compose -p joya-video ps -a` e o `curl` do `/health` com `HTTP 200`.
- [ ] Fluxos dos blocos F e G no ambiente `joya-video`, inclusive o 409 com o `curl` concorrente.
- [ ] `dotnet test backend/JoyaPedidos.slnx` com 254 testes e 0 falhas, e o tempo que levou.
- [ ] `dotnet test --project backend/tests/JoyaPedidos.TestesUnitarios --no-build` com 114 aprovados.
- [ ] `git log --oneline | head -15`, `git log --oneline | wc -l` e `git status -sb`.

## 8. Tempo da apresentação

- [ ] Ensaio completo cronometrado bloco a bloco: planejado 8:40, limite de 10:00. Redistribua o tempo se algum
      bloco estourar.
- [ ] Cronômetro visível só para você (fora da área gravada).
- [ ] Marcos: bloco E até 3:15, bloco H até 6:15, fim do bloco I até 8:15. Passou de 9:15 no fim do I, encerre o J
      só com a frase final.

## 9. Verificação do arquivo de vídeo

- [ ] Duração **menor ou igual a 10:00** conferida no player.
- [ ] Áudio audível do começo ao fim e sincronizado com a imagem.
- [ ] Texto dos terminais e do código legível em tela cheia e em 720p.
- [ ] Assistido do começo ao fim procurando senha, `.env`, variável de ambiente, saída de `docker inspect`, e-mail ou
      notificação na tela. Se aparecer, regravar o trecho ou cortar na edição.
- [ ] O trecho acelerado do bloco E está rotulado como "gravado antes".
- [ ] Exportado em MP4 (H.264 + AAC).
- [ ] Publicado com acesso por link (por exemplo, YouTube "Não listado" ou Google Drive com "Qualquer pessoa com o
      link") e aberto numa janela anônima para confirmar que não pede login. No YouTube, a versão em HD pode demorar
      a ficar disponível: publique com folga e confira a qualidade antes de enviar o link.
