#!/usr/bin/env node
// Verifica os hooks do Claude Code sem executar nenhuma operação perigosa: cada fixture em
// .claude/hooks/fixtures/ é enviada ao hook pelo stdin, e o código de saída e a mensagem são conferidos.
// Também valida .claude/settings.json (JSON, eventos, matchers e scripts referenciados).
//
// Uso: node .claude/hooks/verificar-hooks.mjs        (Windows, Linux ou macOS; só Node, sem dependências)

import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';

const PASTA_HOOKS = dirname(fileURLToPath(import.meta.url));
const RAIZ_PROJETO = resolve(PASTA_HOOKS, '..', '..');
const PASTA_FIXTURES = join(PASTA_HOOKS, 'fixtures');
const ARQUIVO_SETTINGS = join(RAIZ_PROJETO, '.claude', 'settings.json');

const falhas = [];
let verificacoes = 0;

function falhar(mensagem) {
  falhas.push(mensagem);
}

function verificarSettings() {
  let settings;
  try {
    settings = JSON.parse(readFileSync(ARQUIVO_SETTINGS, 'utf8'));
  } catch (erro) {
    falhar(`settings.json inválido: ${erro.message}`);
    return;
  }
  verificacoes++;

  const grupos = settings?.hooks?.PreToolUse;
  if (!Array.isArray(grupos) || grupos.length === 0) {
    falhar('settings.json: hooks.PreToolUse ausente');
    return;
  }

  const protecao = grupos.find((g) => (g.hooks ?? []).some((h) => (h.args ?? []).some((a) => a.endsWith('proteger-historico.mjs'))));
  if (!protecao) {
    falhar('settings.json: nenhum PreToolUse aponta para proteger-historico.mjs');
    return;
  }

  const ferramentasCobertas = String(protecao.matcher ?? '').split('|');
  for (const ferramenta of ['Bash', 'PowerShell', 'Edit', 'Write', 'MultiEdit']) {
    verificacoes++;
    if (!ferramentasCobertas.includes(ferramenta)) falhar(`settings.json: matcher do hook de proteção não cobre ${ferramenta}`);
  }

  for (const hook of protecao.hooks) {
    verificacoes++;
    if (hook.type !== 'command' || hook.command !== 'node') falhar('settings.json: hook de proteção deve usar a forma exec com "node"');
    if (hook.onFailure !== 'block') falhar('settings.json: hook de proteção deve usar "onFailure": "block" (falha fechada)');
    for (const argumento of hook.args ?? []) {
      const caminho = argumento.replace('${CLAUDE_PROJECT_DIR}', RAIZ_PROJETO);
      if (!existsSync(caminho)) falhar(`settings.json: script inexistente: ${argumento}`);
    }
  }
}

function montarEntrada(arquivo, caso) {
  if (typeof caso.entradaBruta === 'string') return caso.entradaBruta;
  if (caso.entrada) return JSON.stringify({ ...arquivo.base, ...caso.entrada });
  const toolInput = { command: caso.comando, description: `fixture: ${caso.descricao}` };
  return JSON.stringify({ ...arquivo.base, tool_input: toolInput });
}

function verificarFixtures() {
  const arquivos = readdirSync(PASTA_FIXTURES).filter((nome) => nome.endsWith('.json')).sort();
  if (arquivos.length === 0) falhar('nenhuma fixture encontrada');

  for (const nomeArquivo of arquivos) {
    const arquivo = JSON.parse(readFileSync(join(PASTA_FIXTURES, nomeArquivo), 'utf8'));
    const script = join(PASTA_HOOKS, arquivo.hook);
    let bloqueados = 0;
    let permitidos = 0;

    for (const caso of arquivo.casos) {
      verificacoes++;
      const execucao = spawnSync(process.execPath, [script], {
        input: montarEntrada(arquivo, caso),
        encoding: 'utf8',
        timeout: 15000,
      });
      const rotulo = `${nomeArquivo} › ${caso.descricao}`;

      if (execucao.error) {
        falhar(`${rotulo}: erro ao executar o hook (${execucao.error.message})`);
        continue;
      }
      if (execucao.status !== caso.saida) {
        falhar(`${rotulo}: saída ${execucao.status}, esperada ${caso.saida}. stderr: ${execucao.stderr.trim() || '(vazio)'}`);
        continue;
      }
      if (caso.saida === 2 && !execucao.stderr.includes('Operação bloqueada')) {
        falhar(`${rotulo}: bloqueio sem mensagem padronizada em stderr`);
      }
      if (caso.saida === 0 && execucao.stderr.trim() !== '') {
        falhar(`${rotulo}: caso permitido não deveria escrever em stderr (${execucao.stderr.trim()})`);
      }
      if (caso.mensagemContem && !execucao.stderr.includes(caso.mensagemContem)) {
        falhar(`${rotulo}: mensagem não contém "${caso.mensagemContem}". stderr: ${execucao.stderr.trim()}`);
      }
      if (caso.saida === 2) bloqueados++;
      else permitidos++;
    }
    console.log(`  ${nomeArquivo}: ${arquivo.casos.length} casos (${bloqueados} bloqueados, ${permitidos} permitidos)`);
  }
}

console.log(`Verificando hooks em ${PASTA_HOOKS} (Node ${process.version}, ${process.platform})`);
verificarSettings();
verificarFixtures();

if (falhas.length > 0) {
  console.error(`\n${falhas.length} falha(s) em ${verificacoes} verificações:`);
  for (const falha of falhas) console.error(`  ✗ ${falha}`);
  process.exit(1);
}
console.log(`\nOK: ${verificacoes} verificações aprovadas.`);
