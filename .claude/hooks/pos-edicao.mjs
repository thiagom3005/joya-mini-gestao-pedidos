#!/usr/bin/env node
// Hook PostToolUse: verificação de compilação logo após uma edição (spec §2.3, plano E2).
//
// Roda DEPOIS da edição: não a impede nem a desfaz. Se a compilação falhar, devolve as linhas de erro ao Claude
// (saída 2 + stderr) para que ele corrija em seguida; a edição continua no arquivo.
//
//   .cs / .csproj      → `dotnet build` incremental APENAS do projeto mais próximo do arquivo (não da solução).
//   .props / .targets  → `dotnet build` da solução backend/JoyaPedidos.slnx (afetam todos os projetos; raro).
//   .ts / .tsx         → `npm run typecheck` em frontend/, só se frontend/package.json tiver o script e
//                        frontend/node_modules existir. Arquivos fora de frontend/ (ex.: e2e/) são ignorados.
//   demais arquivos    → nada.
//
// Builds concorrentes são evitados com uma trava em arquivo no diretório temporário do sistema: uma única trava
// para todos os builds dotnet deste repositório (projetos compartilham obj/) e outra para o typecheck. A trava
// guarda o PID; se o processo dono não existir mais (hook interrompido), ela é descartada.
// Build de outro processo usando os mesmos arquivos (ex.: `dotnet test` em andamento) não é tratado como erro.
// Saídas: 0 = ok ou nada a fazer; 2 = erro de compilação (mostrado ao Claude); 1 = problema do próprio hook
// (ex.: dotnet ausente, tempo esgotado), mostrado ao usuário sem bloquear.

import { readFileSync, writeFileSync, existsSync, readdirSync, statSync, openSync, closeSync, unlinkSync } from 'node:fs';
import { dirname, join, resolve, relative, extname, isAbsolute, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { spawnSync } from 'node:child_process';
import { createHash } from 'node:crypto';
import { tmpdir } from 'node:os';

const PASTA_HOOK = dirname(fileURLToPath(import.meta.url));
const RAIZ = resolve(PASTA_HOOK, '..', '..');
const SOLUCAO = join(RAIZ, 'backend', 'JoyaPedidos.slnx');
const PASTA_FRONTEND = join(RAIZ, 'frontend');

const TEMPO_LIMITE_MS = 110_000; // abaixo do timeout de 120 s configurado em settings.json
const TRAVA_EXPIRA_MS = 150_000;
const MAXIMO_LINHAS_DE_ERRO = 30;
const ERROS_DE_ARQUIVO_EM_USO = /\b(MSB3021|MSB3026|MSB3027|CS2012)\b/;

const SAIDA_OK = 0;
const SAIDA_PROBLEMA_DO_HOOK = 1;
const SAIDA_ERRO_DE_COMPILACAO = 2;

try {
  process.exit(main());
} catch (erro) {
  process.stderr.write(`[pos-edicao] Falha interna do hook (a edição não foi afetada): ${erro?.message ?? erro}\n`);
  process.exit(SAIDA_PROBLEMA_DO_HOOK);
}

function main() {
  const evento = JSON.parse(readFileSync(0, 'utf8'));
  registrarFormatoSeDepurando(evento);

  const entrada = evento?.tool_input ?? {};
  const caminhoInformado = entrada.file_path;
  if (typeof caminhoInformado !== 'string' || caminhoInformado === '') return SAIDA_OK;

  const base = typeof evento.cwd === 'string' && evento.cwd !== '' && existsSync(evento.cwd) ? evento.cwd : RAIZ;
  const arquivo = isAbsolute(caminhoInformado) ? resolve(caminhoInformado) : resolve(base, caminhoInformado);
  const relativo = relative(RAIZ, arquivo);
  if (relativo.startsWith('..') || isAbsolute(relativo)) return SAIDA_OK; // fora do repositório

  const alvo = escolherAlvo(arquivo, relativo, extname(arquivo).toLowerCase());
  if (!alvo) return SAIDA_OK;

  return executarComTrava(alvo, () => verificar(alvo, relativo));
}

function escolherAlvo(arquivo, relativo, extensao) {
  if (extensao === '.cs' || extensao === '.csproj') {
    const projeto = extensao === '.csproj' ? arquivo : projetoMaisProximo(dirname(arquivo));
    return projeto ? { tipo: 'dotnet', caminho: projeto } : null;
  }
  if (extensao === '.props' || extensao === '.targets') {
    return relativo.split(sep)[0] === 'backend' && existsSync(SOLUCAO) ? { tipo: 'dotnet', caminho: SOLUCAO } : null;
  }
  if (extensao === '.ts' || extensao === '.tsx') {
    const partes = relativo.split(sep);
    if (partes[0] !== 'frontend' || partes.includes('node_modules')) return null;
    return frontendPronto() ? { tipo: 'typecheck', caminho: PASTA_FRONTEND } : null;
  }
  return null;
}

// Sobe a partir da pasta do arquivo até a raiz do repositório procurando a pasta com exatamente um .csproj.
function projetoMaisProximo(pasta) {
  let atual = pasta;
  for (;;) {
    const relativoARaiz = relative(RAIZ, atual);
    if (relativoARaiz.startsWith('..') || isAbsolute(relativoARaiz)) return null;
    if (existsSync(atual)) {
      const projetos = readdirSync(atual).filter((nome) => nome.endsWith('.csproj'));
      if (projetos.length === 1) return join(atual, projetos[0]);
      if (projetos.length > 1) return null;
    }
    const pai = dirname(atual);
    if (relativoARaiz === '' || pai === atual) return null;
    atual = pai;
  }
}

function frontendPronto() {
  const pacote = join(PASTA_FRONTEND, 'package.json');
  if (!existsSync(pacote) || !existsSync(join(PASTA_FRONTEND, 'node_modules'))) return false;
  try {
    return Boolean(JSON.parse(readFileSync(pacote, 'utf8'))?.scripts?.typecheck);
  } catch {
    return false;
  }
}

function verificar(alvo, relativo) {
  const execucao =
    alvo.tipo === 'dotnet'
      ? spawnSync('dotnet', ['build', alvo.caminho, '-nologo', '-v', 'q', '-clp:NoSummary'], opcoesDeExecucao(RAIZ, false))
      : executarTypecheck(alvo.caminho);

  const nomeDoAlvo = relative(RAIZ, alvo.caminho) || alvo.caminho;

  if (execucao.error) {
    const motivo = execucao.error.code === 'ETIMEDOUT' ? `tempo limite de ${TEMPO_LIMITE_MS / 1000} s esgotado` : execucao.error.message;
    process.stderr.write(`[pos-edicao] Não foi possível verificar ${nomeDoAlvo}: ${motivo}. A edição foi mantida.\n`);
    return SAIDA_PROBLEMA_DO_HOOK;
  }
  if (execucao.status === 0) return SAIDA_OK;

  const saida = `${execucao.stdout ?? ''}\n${execucao.stderr ?? ''}`;
  const linhasDeErro = [...new Set(saida.split(/\r?\n/).map((l) => l.trim()).filter((l) => /\berror\b/i.test(l)))];

  if (linhasDeErro.length > 0 && linhasDeErro.every((l) => ERROS_DE_ARQUIVO_EM_USO.test(l))) {
    process.stderr.write(`[pos-edicao] ${nomeDoAlvo} não foi verificado: arquivos de saída em uso (outro processo rodando).\n`);
    return SAIDA_OK;
  }

  const listadas = linhasDeErro.slice(0, MAXIMO_LINHAS_DE_ERRO);
  const corte = linhasDeErro.length > listadas.length ? `\n(+${linhasDeErro.length - listadas.length} linha(s) de erro omitidas)` : '';
  const detalhes = listadas.length > 0 ? listadas.join('\n') : saida.trim().split(/\r?\n/).slice(-MAXIMO_LINHAS_DE_ERRO).join('\n');
  process.stderr.write(
    `[pos-edicao] A verificação de ${nomeDoAlvo} falhou após a edição de ${relativo}. ` +
      `A edição foi mantida no arquivo; corrija os erros abaixo:\n${detalhes}${corte}\n`,
  );
  return SAIDA_ERRO_DE_COMPILACAO;
}

// No Windows o npm é um .cmd e exige shell; o comando é um texto constante, sem nenhum dado do evento.
function executarTypecheck(pasta) {
  return process.platform === 'win32'
    ? spawnSync('npm run typecheck --silent', opcoesDeExecucao(pasta, true))
    : spawnSync('npm', ['run', 'typecheck', '--silent'], opcoesDeExecucao(pasta, false));
}

function opcoesDeExecucao(cwd, shell) {
  return { cwd, shell, encoding: 'utf8', timeout: TEMPO_LIMITE_MS, windowsHide: true, maxBuffer: 16 * 1024 * 1024 };
}

function executarComTrava(alvo, acao) {
  const trava = caminhoDaTrava(alvo.tipo);
  if (!adquirirTrava(trava)) {
    process.stderr.write(`[pos-edicao] Outra verificação ${alvo.tipo} já está em andamento; esta edição não disparou outra.\n`);
    return SAIDA_OK;
  }
  try {
    return acao();
  } finally {
    try {
      unlinkSync(trava);
    } catch {
      // trava já removida
    }
  }
}

function adquirirTrava(trava) {
  for (let tentativa = 0; tentativa < 2; tentativa++) {
    try {
      const descritor = openSync(trava, 'wx');
      writeFileSync(descritor, String(process.pid));
      closeSync(descritor);
      return true;
    } catch {
      if (!travaOrfaOuExpirada(trava)) return false;
      try {
        unlinkSync(trava);
      } catch {
        // outro processo já removeu
      }
    }
  }
  return false;
}

function travaOrfaOuExpirada(trava) {
  try {
    if (Date.now() - statSync(trava).mtimeMs > TRAVA_EXPIRA_MS) return true;
    const pid = Number.parseInt(readFileSync(trava, 'utf8'), 10);
    if (!Number.isInteger(pid) || pid <= 0) return false; // trava recém-criada, PID ainda não gravado
    process.kill(pid, 0); // lança se o processo não existe
    return false;
  } catch (erro) {
    return erro?.code === 'ESRCH';
  }
}

// Chave por repositório e por tipo de verificação: builds dotnet compartilham obj/, então uma trava só.
function caminhoDaTrava(tipo) {
  const repositorio = createHash('sha1').update(RAIZ.toLowerCase()).digest('hex').slice(0, 10);
  return join(tmpdir(), `joya-pos-edicao-${repositorio}-${tipo}.lock`);
}

function registrarFormatoSeDepurando(evento) {
  try {
    if (!existsSync(join(PASTA_HOOK, '.depurar'))) return;
    const descrever = (objeto) =>
      Object.fromEntries(Object.entries(objeto ?? {}).map(([chave, valor]) => [chave, Array.isArray(valor) ? 'array' : typeof valor]));
    const formato = {
      registradoEm: new Date().toISOString(),
      hook_event_name: evento?.hook_event_name ?? null,
      tool_name: evento?.tool_name ?? null,
      campos_raiz: descrever(evento),
      campos_tool_input: descrever(evento?.tool_input),
      campos_tool_response: descrever(evento?.tool_response),
    };
    writeFileSync(join(PASTA_HOOK, 'ultima-entrada-pos.json'), JSON.stringify(formato, null, 2) + '\n');
  } catch {
    // Depuração nunca interfere no hook.
  }
}
