#!/usr/bin/env node
// Verifica os hooks do Claude Code sem executar nenhuma operação perigosa: cada fixture em
// .claude/hooks/fixtures/*.json é enviada ao hook pelo stdin, e o código de saída e a mensagem são conferidos.
// Também valida .claude/settings.json (JSON, eventos, matchers e scripts referenciados).
//
// Uso: node .claude/hooks/verificar-hooks.mjs        (Windows, Linux ou macOS; só Node, sem dependências)
//
// Fixtures podem declarar "requer": ["dotnet"]; sem o comando no PATH (ex.: container só com Node), o caso é
// pulado e contado à parte. A string "${RAIZ}" nas fixtures é trocada pela raiz do repositório.

import { readFileSync, readdirSync, existsSync, writeFileSync, unlinkSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';

const PASTA_HOOKS = dirname(fileURLToPath(import.meta.url));
const RAIZ_PROJETO = resolve(PASTA_HOOKS, '..', '..');
const PASTA_FIXTURES = join(PASTA_HOOKS, 'fixtures');
const ARQUIVO_SETTINGS = join(RAIZ_PROJETO, '.claude', 'settings.json');

const falhas = [];
let verificacoes = 0;
let pulados = 0;
const disponibilidade = new Map();

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

  verificarGrupo(settings, 'PreToolUse', 'proteger-historico.mjs', ['Bash', 'PowerShell', 'Edit', 'Write', 'MultiEdit'], (hook) => {
    if (hook.onFailure !== 'block') falhar('settings.json: hook de proteção deve usar "onFailure": "block" (falha fechada)');
  });
  verificarGrupo(settings, 'PostToolUse', 'pos-edicao.mjs', ['Edit', 'Write', 'MultiEdit'], (hook) => {
    if (!(hook.timeout >= 60)) falhar('settings.json: hook pós-edição precisa de timeout explícito (≥ 60 s)');
  });
}

function verificarGrupo(settings, evento, script, ferramentas, regraExtra) {
  const grupos = settings?.hooks?.[evento];
  const grupo = Array.isArray(grupos)
    ? grupos.find((g) => (g.hooks ?? []).some((h) => (h.args ?? []).some((a) => a.endsWith(script))))
    : undefined;
  verificacoes++;
  if (!grupo) {
    falhar(`settings.json: nenhum ${evento} aponta para ${script}`);
    return;
  }

  const cobertas = String(grupo.matcher ?? '').split('|');
  for (const ferramenta of ferramentas) {
    verificacoes++;
    if (!cobertas.includes(ferramenta)) falhar(`settings.json: matcher de ${evento} (${script}) não cobre ${ferramenta}`);
  }

  for (const hook of grupo.hooks) {
    verificacoes++;
    if (hook.type !== 'command' || hook.command !== 'node') falhar(`settings.json: ${script} deve usar a forma exec com "node"`);
    regraExtra(hook);
    for (const argumento of hook.args ?? []) {
      if (!existsSync(argumento.replace('${CLAUDE_PROJECT_DIR}', RAIZ_PROJETO))) falhar(`settings.json: script inexistente: ${argumento}`);
    }
  }
}

function substituirRaiz(valor) {
  if (typeof valor === 'string') return valor.replaceAll('${RAIZ}', RAIZ_PROJETO);
  if (Array.isArray(valor)) return valor.map(substituirRaiz);
  if (valor && typeof valor === 'object') return Object.fromEntries(Object.entries(valor).map(([k, v]) => [k, substituirRaiz(v)]));
  return valor;
}

function montarEntrada(arquivo, caso) {
  if (typeof caso.entradaBruta === 'string') return caso.entradaBruta;
  if (caso.entrada) return JSON.stringify({ ...arquivo.base, ...caso.entrada });
  if (caso.arquivo !== undefined) {
    return JSON.stringify({ ...arquivo.base, tool_input: { file_path: caso.arquivo, old_string: 'a', new_string: 'b', replace_all: false } });
  }
  return JSON.stringify({ ...arquivo.base, tool_input: { command: caso.comando, description: `fixture: ${caso.descricao}` } });
}

function comandoDisponivel(comando) {
  if (!disponibilidade.has(comando)) {
    const execucao =
      process.platform === 'win32'
        ? spawnSync(`${comando} --version`, { shell: true, encoding: 'utf8' })
        : spawnSync(comando, ['--version'], { encoding: 'utf8' });
    disponibilidade.set(comando, !execucao.error && execucao.status === 0);
  }
  return disponibilidade.get(comando);
}

// Mesmo algoritmo de pos-edicao.mjs: uma trava por repositório e tipo ("dotnet" ou "typecheck").
function caminhoDaTrava(tipo) {
  const repositorio = createHash('sha1').update(RAIZ_PROJETO.toLowerCase()).digest('hex').slice(0, 10);
  return join(tmpdir(), `joya-pos-edicao-${repositorio}-${tipo}.lock`);
}

// "pidDaTrava": "vivo" grava o PID deste processo (dono ativo); "orfao" grava um PID inexistente.
function executarCaso(arquivo, caso, script) {
  const trava = caso.preparar?.trava ? caminhoDaTrava(caso.preparar.trava) : null;
  if (trava) writeFileSync(trava, caso.preparar.pidDaTrava === 'orfao' ? '999999' : String(process.pid));
  try {
    return spawnSync(process.execPath, [script], {
      input: montarEntrada(arquivo, caso),
      encoding: 'utf8',
      timeout: 130_000,
      cwd: RAIZ_PROJETO,
    });
  } finally {
    if (trava && existsSync(trava)) unlinkSync(trava);
  }
}

function verificarFixtures() {
  const arquivos = readdirSync(PASTA_FIXTURES).filter((nome) => nome.endsWith('.json')).sort();
  if (arquivos.length === 0) falhar('nenhuma fixture encontrada');

  for (const nomeArquivo of arquivos) {
    const arquivo = substituirRaiz(JSON.parse(readFileSync(join(PASTA_FIXTURES, nomeArquivo), 'utf8')));
    const script = join(PASTA_HOOKS, arquivo.hook);
    const mensagemPadrao = arquivo.mensagemPadrao ?? 'Operação bloqueada';
    const contagem = { 0: 0, 1: 0, 2: 0, pulados: 0 };

    for (const caso of arquivo.casos) {
      const rotulo = `${nomeArquivo} › ${caso.descricao}`;
      if ((caso.requer ?? []).some((comando) => !comandoDisponivel(comando))) {
        pulados++;
        contagem.pulados++;
        continue;
      }

      verificacoes++;
      const execucao = executarCaso(arquivo, caso, script);
      const stderr = (execucao.stderr ?? '').trim();
      const esperadas = [caso.mensagemContem ?? []].flat();

      if (execucao.error) {
        falhar(`${rotulo}: erro ao executar o hook (${execucao.error.message})`);
        continue;
      }
      if (execucao.status !== caso.saida) {
        falhar(`${rotulo}: saída ${execucao.status}, esperada ${caso.saida}. stderr: ${stderr || '(vazio)'}`);
        continue;
      }
      if (caso.saida === 2 && !stderr.includes(mensagemPadrao)) falhar(`${rotulo}: saída 2 sem a mensagem padrão "${mensagemPadrao}"`);
      if (caso.saida === 0 && esperadas.length === 0 && stderr !== '') falhar(`${rotulo}: caso sem mensagem esperada escreveu em stderr (${stderr})`);
      for (const trecho of esperadas) {
        if (!stderr.includes(trecho)) falhar(`${rotulo}: mensagem não contém "${trecho}". stderr: ${stderr}`);
      }
      contagem[caso.saida]++;
    }
    const resumo = `${contagem[2]} saída 2, ${contagem[1]} saída 1, ${contagem[0]} saída 0` + (contagem.pulados ? `, ${contagem.pulados} pulados` : '');
    console.log(`  ${nomeArquivo}: ${arquivo.casos.length} casos (${resumo})`);
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
const aviso = pulados > 0 ? ` (${pulados} caso(s) pulados por falta de dependência no ambiente)` : '';
console.log(`\nOK: ${verificacoes} verificações aprovadas${aviso}.`);
