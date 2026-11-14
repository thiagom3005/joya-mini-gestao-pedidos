# Checklist de publicação e entrega

Os itens marcados como **ação do candidato** não são executados pelo Claude: aprovar os commits, tornar o repositório
público, publicar o vídeo e enviar a candidatura dependem de decisão e de conta do candidato. Commit e push só com
pedido explícito do candidato (`CLAUDE.md`). Os comandos são para o **Git Bash** (no PowerShell 5.1, `&&` não existe
e as aspas mudam).

Prazo do enunciado registrado no plano (§1): **15/11/2026, 16h (Brasília)**. Limites internos (plano §8): código
congelado até 15/11, 10h; README, vídeo e envio até 15/11, 14h.

## Sequência sugerida

1. Aprovar a preparação da E13 e pedir os commits e o push (ação do candidato).
2. Tornar o repositório público e conferir numa janela anônima (seção 7).
3. Véspera ou manhã de 15/11: testes, vulnerabilidades e clone limpo (seção 4).
4. Clonar o `joya-video`, ensaiar e gravar ([checklist de gravação](checklist-gravacao.md)).
5. Publicar o vídeo até as 14h e conferir 720p ou 1080p numa janela anônima (seção 6).
6. Conferências finais (seções 1 e 2) e envio até as 16h (seções 8 e 9).

## 1. Repositório e histórico

- [ ] Árvore limpa e sincronizada:
  ```bash
  git fetch origin && git status -sb
  ```
  Esperado: `## main...origin/main`, sem arquivos modificados ou não rastreados.
- [ ] Nenhum merge (deve imprimir 0):
  ```bash
  git log --merges --oneline | wc -l
  ```
- [ ] Nenhum commit do reflog fora da `main` (não deve imprimir nada além da última linha):
  ```bash
  git reflog --format=%H | sort -u | while read c; do git merge-base --is-ancestor "$c" main || echo "fora da main: $c"; done; echo "reflog conferido"
  ```
- [ ] O GitHub mostra o mesmo último commit que `git log -1`, e a página de atividade do repositório
      (https://github.com/thiagom3005/joya-mini-gestao-pedidos/activity) não mostra push forçado.
- [ ] Mensagens em Conventional Commits, em português e sem acentos.

## 2. Segredos

- [ ] `.env` fora do Git: `git ls-files .env` sem saída e `git check-ignore -v .env` apontando o `.gitignore`.
- [ ] Busca por padrões de segredo na árvore atual:
  ```bash
  git grep -nIiE "(password|pwd|senha|secret|api[_-]?key|access[_-]?token)[\"']?[[:space:]]*[=:][[:space:]]*[\"']?[A-Za-z0-9_@#%+-]{6,}|BEGIN [A-Z ]*PRIVATE KEY|ghp_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{20,}" -- . ":(exclude)frontend/package-lock.json"
  ```
  Esperado, depois do commit deste checklist: as duas linhas do `docker-compose.yml` com o padrão
  `${MSSQL_SA_PASSWORD:-Joya_Dev_2026_Inseguro}` e a linha deste arquivo que descreve esse padrão. Qualquer outra linha
  precisa ser revisada antes do envio.
- [ ] A mesma busca em **todo o histórico**, porque o repositório público expõe todos os commits:
  ```bash
  git grep -nIiE "(password|pwd|senha|secret|api[_-]?key|access[_-]?token)[\"']?[[:space:]]*[=:][[:space:]]*[\"']?[A-Za-z0-9_@#%+-]{6,}|BEGIN [A-Z ]*PRIVATE KEY|ghp_[A-Za-z0-9]{20,}|sk-[A-Za-z0-9]{20,}" $(git rev-list --all) -- . ":(exclude)frontend/package-lock.json" | cut -d: -f2- | sort -u
  ```
  Em 13/11/2026, só apareceram as duas linhas do `docker-compose.yml`.
- [ ] Senhas versionadas esperadas, ambas públicas e rotuladas como inseguras: a padrão do `sa` de desenvolvimento
      (`Joya_Dev_2026_Inseguro`, em `.env.example`, `CLAUDE.md`, `README.md`, `docker-compose.yml` e neste checklist)
      e a do container efêmero dos testes de integração (`Joya_Teste_2026_Inseguro`, em `SqlServerDeTeste.cs` e neste
      checklist).
- [ ] `.mcp.json` só com `${JOYA_MCP_SENHA}` e `${SQL_PORTA:-1433}`, sem valor de senha.
- [ ] Nenhum `-P` com senha literal em comandos `sqlcmd` documentados. A única ocorrência de `-P` é a da spec §11.2,
      por variável (`"$$MSSQL_SA_PASSWORD"`).

## 3. Documentação

- [ ] README definitivo: Docker, testes, decisões, uso do Claude (o que funcionou e o que foi corrigido), MCP,
      Parte 4 com medições reais e limitações (CA-13, CA-23).
- [ ] `docs/entrega/` publicado no mesmo push do README (o README aponta para essa pasta).
- [ ] Links do README conferidos no GitHub (âncoras do sumário e caminhos de arquivos).
- [ ] `docs/spec.md` inalterada desde a aprovação: `git log --oneline -- docs/spec.md` mostra só `f8e7549`.
- [ ] `docs/plan.md` com o registro da E13, a matriz final de aceite e dois pontos do vídeo: o desvio em relação ao
      plano (os clipes por entrega não foram gravados; o vídeo é gravado de uma vez, pelo roteiro) e a plataforma
      escolhida para publicá-lo (decisão em aberto no plano §11).

## 4. Testes, build, vulnerabilidades e Docker (véspera: Q2, Q4 e Q6 do plano)

- [ ] Backend:
  ```bash
  dotnet build backend/JoyaPedidos.slnx
  ```
  ```bash
  dotnet test backend/JoyaPedidos.slnx
  ```
  Esperado: 0 avisos e 254 testes com 0 falhas.
- [ ] Frontend:
  ```bash
  npm --prefix frontend ci && npm --prefix frontend run lint && npm --prefix frontend run typecheck && npm --prefix frontend run build
  ```
- [ ] Verificador dos hooks sem falhas: `node .claude/hooks/verificar-hooks.mjs`.
- [ ] Vulnerabilidades de produção:
  ```bash
  dotnet list backend/JoyaPedidos.slnx package --vulnerable --include-transitive
  ```
  ```bash
  npm --prefix frontend audit --omit=dev
  ```
  Esperado: nenhum pacote NuGet vulnerável e 0 vulnerabilidades npm de produção. As 9 moderadas de desenvolvimento
  (`minimatch`, via eslint) já estão documentadas no README.
- [ ] MCP (Q4): o `.mcp.json` continua fixando `@bytebase/dbhub@1.6.1`, e não há advisory novo para o pacote além
      dos três citados no README (GHSA-mwwr-p57h-56pf, GHSA-fm8p-53ww-hf6w e GHSA-j656-3hf2-fvjc). Pelo MCP, só
      leitura e sem tentativa de escrita: `SELECT * FROM fn_my_permissions(NULL, 'DATABASE');` lista só `CONNECT`,
      `SELECT`, `VIEW DATABASE PERFORMANCE STATE` e as duas `VIEW ANY COLUMN ... KEY DEFINITION` do `public`.
- [ ] Clone limpo (Q6, CA-01 a CA-03), com projeto próprio `joya-q6` em **todos** os comandos:
  1. No repositório principal (T1): `docker compose -f docker-compose.yml -f docker-compose.dev.yml stop api web`.
  2. Numa pasta temporária: `git clone https://github.com/thiagom3005/joya-mini-gestao-pedidos.git joya-q6 && cd joya-q6`
     e `export COMPOSE_PROJECT_NAME=joya-q6`.
  3. `docker compose -p joya-q6 up --build -d`; depois `docker compose -p joya-q6 ps -a` até `db-init` com
     `Exited (0)` e `api` e `web` `Up`. O `sqlserver` deve mostrar só `1433/tcp`, sem endereço do host (CA-03).
  4. `curl -s http://localhost:5080/health` responde `Healthy`, e http://localhost:3000 mostra os 33 pedidos do seed.
  5. `docker compose -p joya-q6 ps -a` (só containers `joya-q6-*`), depois `docker compose -p joya-q6 down -v --rmi local`
     e apagar a pasta.
  6. No T1: `docker compose -f docker-compose.yml -f docker-compose.dev.yml up -d --no-deps api web`.
- [ ] Banco principal preservado (35 pedidos em 13/11/2026), com a API principal de volta:
  ```bash
  curl -s "http://localhost:5080/pedidos?pagina=1&tamanhoPagina=1" | grep -o '"totalItens":[0-9]*'
  ```

## 5. Evidências do Claude Code

- [ ] `.claude/` versionado: `settings.json`, hooks, fixtures, rules, agentes e skill.
- [ ] README com a seção "Como usei o Claude Code" e o registro de execução do plano com revisões e decisões.
- [ ] Vídeo mostrando o hook em ação, a evidência de uso da skill, os agentes e uma consulta real pelo MCP (CA-20,
      CA-21, CA-22).

## 6. Vídeo (ação do candidato)

- [ ] Gravado conforme o [roteiro](roteiro-video.md) e o [checklist de gravação](checklist-gravacao.md).
- [ ] Duração **menor ou igual a 10:00**.
- [ ] Publicado com acesso por link, sem exigir login; link testado numa janela anônima, em 720p ou mais.
- [ ] Link do vídeo anotado para o envio.

## 7. Repositório público (ação do candidato)

- [ ] Em 13/11/2026 o repositório `thiagom3005/joya-mini-gestao-pedidos` estava **privado**. Tornar público em
      GitHub → Settings → General → Danger Zone → Change repository visibility.
- [ ] Abrir o link do repositório numa janela anônima: README, código e histórico visíveis sem login.

## 8. Instruções de submissão do enunciado (ação do candidato)

O texto do e-mail original do teste **não está no repositório**. Antes de enviar, reler o e-mail e conferir:

- [ ] Destinatário, assunto e formato pedidos.
- [ ] O que deve ir na mensagem. Pelo que a spec registra (RNF-15, CA-26): o link do repositório público no GitHub e
      o link do vídeo de até 10 minutos.
- [ ] Prazo (registrado no plano como 15/11/2026, 16h de Brasília).
- [ ] Qualquer item pedido no e-mail que não esteja nesta lista.

## 9. Envio (ação do candidato)

- [ ] E-mail enviado pelo candidato, com os links conferidos.
- [ ] CA-26 marcado como concluído no plano só depois do envio. Se isso exigir um commit posterior, que seja só de
      documentação, antes das 16h, com build e testes verdes.
- [ ] Fora isso, nenhum push na `main` depois do envio: o avaliador vê o estado atual do repositório.
