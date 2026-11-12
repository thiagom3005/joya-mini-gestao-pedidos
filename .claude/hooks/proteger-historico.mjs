#!/usr/bin/env node
// Hook PreToolUse: protege o histórico Git e o arquivo .env (spec §2.3, plano E1).
//
// Bloqueia, ANTES da execução: push forçado (e variantes), rebase, pull com rebase (inclusive via config),
// commit --amend, reset que move a branch (--hard, --soft, --keep, --merge), clean/stash que apagariam o .env,
// e escrita ou exibição do .env. Permite o resto, inclusive .env.example.
//
// Contrato com o Claude Code: recebe o JSON do evento no stdin; saída 0 = permitir; saída 2 = bloquear,
// com o motivo em stderr (mostrado ao Claude). Entrada ilegível ou erro interno também bloqueiam
// (falha fechada), assim como "onFailure": "block" em .claude/settings.json.
//
// É uma rede de proteção, não uma fronteira de segurança: aliases do Git, scripts externos, APIs de arquivo
// (ex.: node -e, [IO.File]) e ofuscação deliberada escapam da análise. As regras do CLAUDE.md valem sempre.
//
// Depuração: com o arquivo .claude/hooks/.depurar presente, grava em .claude/hooks/ultima-entrada.json
// apenas os NOMES e TIPOS dos campos recebidos (nunca valores), para conferir o formato real do payload.

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const PASTA_HOOK = dirname(fileURLToPath(import.meta.url));
const PROFUNDIDADE_MAXIMA = 3;
const SAIDA_PERMITIR = 0;
const SAIDA_BLOQUEAR = 2;

const CAMPOS_DE_ARQUIVO = ['file_path', 'notebook_path', 'path'];

// Palavras que antecedem o programa sem serem ele (prefixos e palavras-chave de shell).
const PREFIXOS_DE_COMANDO = new Set(['sudo', 'command', 'exec', 'time', 'nohup', 'env', 'call', '&', '.', '!',
  'if', 'then', 'else', 'elif', 'do', 'while', 'until']);
const SHELLS_POSIX = new Set(['bash', 'sh', 'zsh', 'dash']);
const SHELLS_POWERSHELL = new Set(['pwsh', 'powershell']);
const AVALIADORES = new Set(['eval', 'invoke-expression', 'iex']);

const OPCOES_GLOBAIS_GIT_COM_VALOR = new Set(['-c', '-C', '--git-dir', '--work-tree', '--namespace', '--config-env']);
const OPCOES_COMMIT_COM_VALOR = new Set(['-m', '--message', '-F', '--file', '-C', '--reuse-message', '-c',
  '--reedit-message', '--author', '--date', '-t', '--template', '--cleanup', '--fixup', '--squash', '--trailer']);
const MODOS_DE_RESET_PROIBIDOS = new Set(['--hard', '--soft', '--keep', '--merge']);

const ESCRITA_EM_QUALQUER_ARGUMENTO = new Set(['tee', 'rm', 'mv', 'truncate', 'set-content', 'add-content',
  'out-file', 'clear-content', 'new-item', 'remove-item', 'move-item', 'rename-item', 'del', 'erase', 'ren', 'move']);
const ESCRITA_NO_DESTINO = new Set(['cp', 'copy-item', 'copy']);
const LEITURA_DE_CONTEUDO = new Set(['cat', 'type', 'more', 'less', 'head', 'tail', 'get-content', 'gc', 'bat']);

// escapaveisEmAspas: caracteres que o escape protege dentro de aspas duplas (null = qualquer caractere).
const DIALETOS = {
  bash: { escape: '\\', escapaveisEmAspas: '"\\$`', crasesSeparam: true, hereStrings: false, heredocs: true },
  powershell: { escape: '`', escapaveisEmAspas: null, crasesSeparam: false, hereStrings: true, heredocs: false },
  cmd: { escape: null, escapaveisEmAspas: '', crasesSeparam: false, hereStrings: false, heredocs: false },
};

try {
  main();
} catch (erro) {
  bloquear(`erro interno do hook (${erro?.message ?? erro})`);
}

function main() {
  let evento;
  try {
    evento = JSON.parse(readFileSync(0, 'utf8'));
  } catch {
    bloquear('entrada do hook não é um JSON válido');
  }

  registrarFormatoSeDepurando(evento);

  const motivo = avaliarEvento(evento);
  if (motivo) bloquear(motivo);
  process.exit(SAIDA_PERMITIR);
}

function avaliarEvento(evento) {
  if (!evento || typeof evento !== 'object') return 'entrada do hook sem objeto JSON';

  const ferramenta = String(evento.tool_name ?? '').toLowerCase();
  const entrada = evento.tool_input && typeof evento.tool_input === 'object' ? evento.tool_input : {};

  for (const campo of CAMPOS_DE_ARQUIVO) {
    if (typeof entrada[campo] === 'string' && ehArquivoEnv(entrada[campo])) {
      return `edição do arquivo ${entrada[campo]} (o .env guarda segredos locais; edite o .env.example)`;
    }
  }

  if (typeof entrada.command === 'string') {
    const dialeto = ferramenta === 'powershell' ? 'powershell' : 'bash';
    return avaliarComando(entrada.command, dialeto, 0);
  }
  return null;
}

function avaliarComando(comando, dialeto, profundidade) {
  if (!comando || profundidade > PROFUNDIDADE_MAXIMA) return null;

  const { segmentos, substituicoes } = analisarComando(comando, dialeto);
  for (const segmento of segmentos) {
    const motivo = avaliarSegmento(segmento, dialeto, profundidade);
    if (motivo) return motivo;
  }
  for (const interno of substituicoes) {
    const motivo = avaliarComando(interno, dialeto, profundidade + 1);
    if (motivo) return motivo;
  }
  return null;
}

function avaliarSegmento(tokens, dialeto, profundidade) {
  for (let i = 0; i < tokens.length - 1; i++) {
    if (/^\d?>>?$/.test(tokens[i]) && ehArquivoEnv(tokens[i + 1])) {
      return `redirecionamento para ${tokens[i + 1]} (o .env guarda segredos locais; edite o .env.example)`;
    }
  }

  const simples = removerPrefixos(tokens);
  if (simples.length === 0) return null;

  const programa = nomeDoPrograma(simples[0]);
  const argumentos = simples.slice(1);

  return (
    avaliarAcessoAoEnv(programa, argumentos) ??
    (programa === 'git' ? avaliarGit(argumentos) : avaliarComandoAninhado(programa, argumentos, dialeto, profundidade))
  );
}

// Remove atribuições de variável (Bash `X=1 git ...`, PowerShell `$x = git ...`) e prefixos de shell.
function removerPrefixos(tokens) {
  let i = 0;
  while (i < tokens.length) {
    const token = tokens[i];
    if (PREFIXOS_DE_COMANDO.has(token.toLowerCase()) || /^[A-Za-z_][A-Za-z0-9_]*=/.test(token)) {
      i++;
    } else if (/^\$[\w:.]+$/.test(token) && /^[-+*/]?=$/.test(tokens[i + 1] ?? '')) {
      i += 2;
    } else if (/^\$[\w:.]+[-+*/]?=$/.test(token)) {
      i++;
    } else {
      const atribuicaoColada = /^\$[\w:.]+[-+*/]?=(.+)$/.exec(token);
      if (atribuicaoColada) return [atribuicaoColada[1], ...tokens.slice(i + 1)];
      break;
    }
  }
  return tokens.slice(i);
}

function avaliarComandoAninhado(programa, argumentos, dialeto, profundidade) {
  const proximo = profundidade + 1;

  if (SHELLS_POSIX.has(programa)) {
    const indice = argumentos.findIndex((a) => /^-[a-z]*c[a-z]*$/i.test(a));
    return indice >= 0 ? avaliarComando(argumentos[indice + 1] ?? '', 'bash', proximo) : null;
  }
  if (SHELLS_POWERSHELL.has(programa)) {
    const indice = argumentos.findIndex((a) => /^-(c|com|comm|comma|comman|command)$/i.test(a));
    return indice >= 0 ? avaliarComando(argumentos.slice(indice + 1).join(' '), 'powershell', proximo) : null;
  }
  if (programa === 'cmd') {
    const indice = argumentos.findIndex((a) => /^\/[ck]$/i.test(a));
    return indice >= 0 ? avaliarComando(argumentos.slice(indice + 1).join(' '), 'cmd', proximo) : null;
  }
  if (AVALIADORES.has(programa)) {
    const resto = argumentos.filter((a) => !/^-command$/i.test(a));
    return avaliarComando(resto.join(' '), dialeto, proximo);
  }
  return null;
}

function avaliarGit(argumentos) {
  const configuracoes = [];
  let i = 0;
  while (i < argumentos.length && argumentos[i].startsWith('-')) {
    const opcao = argumentos[i];
    if (OPCOES_GLOBAIS_GIT_COM_VALOR.has(opcao)) {
      if (opcao === '-c') configuracoes.push(argumentos[i + 1] ?? '');
      i += 2;
    } else {
      i += 1;
    }
  }

  const subcomando = (argumentos[i] ?? '').toLowerCase();
  const resto = argumentos.slice(i + 1);

  switch (subcomando) {
    case 'push':
      return avaliarPush(resto);
    case 'rebase':
      return resto.some((a) => a === '--abort' || a === '--quit')
        ? null
        : '`git rebase` reescreve o histórico; o projeto proíbe rebase';
    case 'pull':
      return avaliarPull(resto, configuracoes);
    case 'config':
      return avaliarConfig(resto);
    case 'commit':
      return avaliarCommit(resto);
    case 'reset': {
      const modo = resto.find((a) => MODOS_DE_RESET_PROIBIDOS.has(a));
      return modo ? `\`git reset ${modo}\` move a branch e reescreve o histórico local; faça um commit novo` : null;
    }
    case 'clean':
      return resto.some((a) => /^-[a-zA-Z]*[xX]/.test(a))
        ? '`git clean -x/-X` apagaria arquivos ignorados, inclusive o .env; limpe as pastas pelo nome'
        : null;
    case 'stash':
      return resto.some((a) => a === '-a' || a === '--all')
        ? '`git stash --all` retiraria arquivos ignorados, inclusive o .env'
        : null;
    default:
      return null;
  }
}

function avaliarPush(argumentos) {
  for (const argumento of argumentos) {
    if (argumento === '--force' || argumento === '--mirror' || argumento.startsWith('--force-with-lease')) {
      return `\`git push ${argumento}\` sobrescreve o histórico remoto; o projeto proíbe push forçado`;
    }
    if (/^-[a-zA-Z]+$/.test(argumento) && argumento.includes('f')) {
      return `\`git push ${argumento}\` força o push; o projeto proíbe push forçado`;
    }
    if (argumento.startsWith('+') && argumento.length > 1) {
      return `refspec \`${argumento}\` força o push; o projeto proíbe push forçado`;
    }
  }
  return null;
}

function avaliarPull(argumentos, configuracoes) {
  for (const argumento of argumentos) {
    if (argumento === '--rebase' || argumento === '-r') return '`git pull --rebase` reescreve commits locais; use merge';
    if (argumento.startsWith('--rebase=') && argumento !== '--rebase=false') {
      return `\`git pull ${argumento}\` reescreve commits locais; use merge`;
    }
    if (/^-[a-zA-Z]+$/.test(argumento) && argumento.includes('r')) {
      return `\`git pull ${argumento}\` inclui rebase; use merge`;
    }
  }
  if (!argumentos.includes('--no-rebase')) {
    const rebaseViaConfig = configuracoes.find((c) => /^pull\.rebase=(?!false$).+/i.test(c));
    if (rebaseViaConfig) return `\`git -c ${rebaseViaConfig} pull\` faz rebase; use merge`;
  }
  return null;
}

function avaliarConfig(argumentos) {
  const indice = argumentos.findIndex((a) => /^(pull\.rebase|branch\..+\.rebase)$/i.test(a));
  if (indice < 0) return null;
  const valor = argumentos[indice + 1];
  if (valor === undefined || valor.startsWith('-') || /^false$/i.test(valor)) return null;
  return `\`git config ${argumentos[indice]} ${valor}\` faria os próximos pulls usarem rebase; use merge`;
}

function avaliarCommit(argumentos) {
  for (let i = 0; i < argumentos.length; i++) {
    const argumento = argumentos[i];
    if (argumento === '--amend') return '`git commit --amend` reescreve o último commit; faça um commit novo';
    if (OPCOES_COMMIT_COM_VALOR.has(argumento)) i += 1;
  }
  return null;
}

function avaliarAcessoAoEnv(programa, argumentos) {
  const alvo = argumentos.find(ehArquivoEnv);
  if (!alvo) return null;
  const posicionais = argumentos.filter((a) => !a.startsWith('-') && !/^\d?>>?$/.test(a));

  if (ESCRITA_EM_QUALQUER_ARGUMENTO.has(programa)) {
    return `\`${programa}\` alteraria o arquivo ${alvo}; o .env não pode ser editado pelo Claude`;
  }
  if (ESCRITA_NO_DESTINO.has(programa) && posicionais.length >= 2 && ehArquivoEnv(posicionais.at(-1))) {
    return `\`${programa}\` sobrescreveria o arquivo ${posicionais.at(-1)}; o .env não pode ser editado pelo Claude`;
  }
  if (programa === 'sed' && argumentos.some((a) => /^-[a-zA-Z]*i/.test(a) || a.startsWith('--in-place'))) {
    return `\`sed -i\` alteraria o arquivo ${alvo}; o .env não pode ser editado pelo Claude`;
  }
  if (LEITURA_DE_CONTEUDO.has(programa)) {
    return `\`${programa}\` exibiria o conteúdo de ${alvo}; não exponha segredos locais`;
  }
  return null;
}

// Tokenização simplificada de linha de comando, por dialeto. Divide em segmentos (separados por
// ; && || | & quebra de linha, subshells e blocos) e cada segmento em tokens, respeitando aspas, escapes,
// continuação de linha, heredocs (Bash) e here-strings (PowerShell). Redirecionamentos viram tokens próprios.
// Comandos dentro de $(...) entre aspas duplas são devolvidos em `substituicoes` para análise recursiva.
function analisarComando(comando, nomeDialeto) {
  const dialeto = DIALETOS[nomeDialeto];
  const segmentos = [];
  const substituicoes = [];
  const heredocsPendentes = [];
  let tokens = [];
  let atual = '';
  let temToken = false;
  let aspas = null;

  const fecharToken = () => {
    if (temToken) tokens.push(atual);
    atual = '';
    temToken = false;
  };
  const fecharSegmento = () => {
    fecharToken();
    if (tokens.length > 0) segmentos.push(tokens);
    tokens = [];
  };
  const ehQuebra = (j) => comando[j] === '\n' || (comando[j] === '\r' && comando[j + 1] === '\n');

  for (let i = 0; i < comando.length; i++) {
    const c = comando[i];
    const proximo = comando[i + 1];

    if (aspas === "'") {
      if (c !== "'") atual += c;
      else if (nomeDialeto === 'powershell' && proximo === "'") (atual += "'"), i++;
      else aspas = null;
      continue;
    }

    if (aspas === '"') {
      if (c === '$' && proximo === '(') {
        const { conteudo, fim } = extrairSubstituicao(comando, i + 2);
        substituicoes.push(conteudo);
        i = fim;
      } else if (
        dialeto.escape && c === dialeto.escape && proximo !== undefined &&
        (dialeto.escapaveisEmAspas === null || dialeto.escapaveisEmAspas.includes(proximo))
      ) {
        atual += proximo;
        i++;
      } else if (c === '"' && nomeDialeto === 'powershell' && proximo === '"') {
        atual += '"';
        i++;
      } else if (c === '"') {
        aspas = null;
      } else {
        atual += c;
      }
      continue;
    }

    if (dialeto.hereStrings && c === '@' && (proximo === "'" || proximo === '"') && ehQuebra(i + 2)) {
      const terminador = `\n${proximo}@`;
      const fim = comando.indexOf(terminador, i + 2);
      atual += fim < 0 ? comando.slice(i + 2) : comando.slice(i + 2, fim);
      temToken = true;
      i = fim < 0 ? comando.length : fim + terminador.length - 1;
      continue;
    }

    if (dialeto.escape && c === dialeto.escape && proximo !== undefined) {
      if (ehQuebra(i + 1)) {
        fecharToken(); // continuação de linha: equivale a espaço
        i += comando[i + 1] === '\r' ? 2 : 1;
      } else {
        atual += proximo;
        temToken = true;
        i++;
      }
      continue;
    }

    if (dialeto.heredocs && c === '<' && proximo === '<' && comando[i + 2] !== '<') {
      const heredoc = /^<<(-?)\s*(['"]?)\\?([A-Za-z_][A-Za-z0-9_]*)\2/.exec(comando.slice(i));
      if (heredoc) {
        fecharToken();
        heredocsPendentes.push({ delimitador: heredoc[3], removerTabs: heredoc[1] === '-' });
        i += heredoc[0].length - 1;
        continue;
      }
    }

    if (c === "'" || c === '"') {
      aspas = c;
      temToken = true;
    } else if (c === ' ' || c === '\t') {
      fecharToken();
    } else if (c === '\n' || c === '\r') {
      fecharSegmento();
      if (c === '\n' && heredocsPendentes.length > 0) i = pularCorposDeHeredoc(comando, i + 1, heredocsPendentes) - 1;
    } else if (';|&(){}'.includes(c) || (dialeto.crasesSeparam && c === '`') || (c === '$' && proximo === '(')) {
      if (c === '&' && !temToken && tokens.length === 0 && proximo !== '&') {
        tokens.push('&'); // operador de chamada do PowerShell: prefixo do comando, não separador
        continue;
      }
      fecharSegmento();
      if (c === '$') i++;
    } else if (c === '>') {
      const descritor = /^\d$/.test(atual) ? atual : '';
      if (descritor) (atual = ''), (temToken = false);
      else fecharToken();
      const operador = proximo === '>' ? '>>' : '>';
      if (operador === '>>') i++;
      tokens.push(descritor + operador);
    } else {
      atual += c;
      temToken = true;
    }
  }
  fecharSegmento();
  return { segmentos, substituicoes };
}

// Lê o conteúdo de $( ... ) a partir de `inicio` (logo após o parêntese), respeitando aninhamento, aspas e
// corpos de heredoc (que podem conter apóstrofos e parênteses, como nas mensagens de commit multilinha).
function extrairSubstituicao(comando, inicio) {
  let profundidade = 1;
  let aspas = null;
  const heredocsPendentes = [];
  for (let i = inicio; i < comando.length; i++) {
    const c = comando[i];
    if (aspas) {
      if (c === aspas) aspas = null;
      continue;
    }
    const heredoc = c === '<' ? /^<<(-?)\s*(['"]?)\\?([A-Za-z_][A-Za-z0-9_]*)\2/.exec(comando.slice(i)) : null;
    if (heredoc) {
      heredocsPendentes.push({ delimitador: heredoc[3], removerTabs: heredoc[1] === '-' });
      i += heredoc[0].length - 1;
    } else if (c === '\n' && heredocsPendentes.length > 0) {
      i = pularCorposDeHeredoc(comando, i + 1, heredocsPendentes) - 1;
    } else if (c === "'" || c === '"') {
      aspas = c;
    } else if (c === '(') {
      profundidade++;
    } else if (c === ')' && --profundidade === 0) {
      return { conteudo: comando.slice(inicio, i), fim: i };
    }
  }
  return { conteudo: comando.slice(inicio), fim: comando.length - 1 };
}

// Avança sobre os corpos dos heredocs pendentes; devolve o índice da primeira linha após o último delimitador.
function pularCorposDeHeredoc(comando, inicio, pendentes) {
  let posicao = inicio;
  while (pendentes.length > 0 && posicao < comando.length) {
    const { delimitador, removerTabs } = pendentes[0];
    const fimDaLinha = comando.indexOf('\n', posicao);
    const linha = comando.slice(posicao, fimDaLinha < 0 ? comando.length : fimDaLinha).replace(/\r$/, '');
    posicao = fimDaLinha < 0 ? comando.length : fimDaLinha + 1;
    if ((removerTabs ? linha.replace(/^\t+/, '') : linha) === delimitador) pendentes.shift();
  }
  pendentes.length = 0;
  return posicao;
}

function nomeDoPrograma(token) {
  const base = token.split(/[\\/]/).pop() ?? '';
  return base.toLowerCase().replace(/\.(exe|cmd|bat|ps1)$/, '');
}

function ehArquivoEnv(caminho) {
  const base = String(caminho).trim().split(/[\\/]/).pop() ?? '';
  return base.toLowerCase() === '.env';
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
    };
    writeFileSync(join(PASTA_HOOK, 'ultima-entrada.json'), JSON.stringify(formato, null, 2) + '\n');
  } catch {
    // Depuração nunca interfere na decisão do hook.
  }
}

function bloquear(motivo) {
  process.stderr.write(
    `[proteger-historico] Operação bloqueada: ${motivo}.\n` +
      'Política do projeto (CLAUDE.md): sem reescrita de histórico Git e sem edição ou exibição do .env. ' +
      'Se a operação for realmente necessária, peça ao candidato para executá-la manualmente.\n',
  );
  process.exit(SAIDA_BLOQUEAR);
}
