import { execFileSync } from 'node:child_process'

/**
 * Endereço do frontend do ambiente isolado de E2E: projeto Compose `joya-e12`, web na porta 3012 do host
 * (README, "Diferenciais"). A API é usada pelo mesmo proxy do nginx, em `/api`.
 */
export const URL_BASE = process.env['E2E_URL_BASE'] ?? 'http://localhost:3012'

/** Projeto Compose do ambiente isolado. O banco do projeto principal (`joya-pedidos`) nunca recebe os testes. */
export const PROJETO_E2E = 'joya-e12'

const HOSTS_LOCAIS = new Set(['localhost', '127.0.0.1'])

/**
 * Recusa qualquer alvo que não seja o frontend do projeto `joya-e12`. Olhar só a porta não basta: um servidor do Vite
 * (5173 ou 4173) ou a stack principal recriada noutra porta encaminhariam `/api` para o banco principal. Por isso a
 * porta precisa ser publicada por um container desse projeto, conferido no Docker; o nginx dele só alcança a API e o
 * banco do mesmo projeto.
 */
export function garantirAmbienteIsolado(url: string): void {
  const endereco = new URL(url)
  if (!HOSTS_LOCAIS.has(endereco.hostname)) {
    throw new Error(`E2E recusado: ${url} não é local; só o ambiente isolado ${PROJETO_E2E} pode ser usado.`)
  }
  const porta = endereco.port === '' ? '80' : endereco.port
  const conteineres = execFileSync(
    'docker',
    ['ps', '-q', '--filter', `label=com.docker.compose.project=${PROJETO_E2E}`, '--filter', `publish=${porta}`],
    { encoding: 'utf8' },
  ).trim()
  if (conteineres === '') {
    throw new Error(
      `E2E recusado: a porta ${porta} não é publicada por um container do projeto ${PROJETO_E2E}. Suba o ambiente ` +
        `isolado com \`docker compose -p ${PROJETO_E2E}\` (WEB_PORTA=3012) e aponte E2E_URL_BASE para ele.`,
    )
  }
}
