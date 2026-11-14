import { URL_BASE, garantirAmbienteIsolado } from './ambiente.ts'

const PRAZO_MS = 120_000
const INTERVALO_MS = 1_000

/**
 * Antes da suíte: recusa a stack principal e espera o `GET /api/health` responder 200 pelo proxy do nginx
 * (spec §10.5). Logo depois do `up`, o nginx pode responder 502 até a API iniciar.
 */
export default async function prepararAmbiente(): Promise<void> {
  garantirAmbienteIsolado(URL_BASE)
  const limite = Date.now() + PRAZO_MS
  let ultimaFalha = ''
  while (Date.now() < limite) {
    try {
      const resposta = await fetch(`${URL_BASE}/api/health`)
      if (resposta.ok) {
        return
      }
      ultimaFalha = `HTTP ${resposta.status}`
    } catch (erro) {
      ultimaFalha = erro instanceof Error ? erro.message : String(erro)
    }
    await new Promise((resolver) => setTimeout(resolver, INTERVALO_MS))
  }
  throw new Error(`A API não respondeu em ${URL_BASE}/api/health em ${PRAZO_MS / 1000} s (${ultimaFalha}).`)
}
