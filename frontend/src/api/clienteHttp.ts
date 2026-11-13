import { CODIGOS_ERRO, type CodigoErro, type ProblemDetails } from '../tipos/contratos.ts'

// Cliente HTTP tipado (spec §6.1, §6.6). Caminhos relativos a /api: no Docker o nginx encaminha para a API removendo
// o prefixo; em desenvolvimento, o proxy do Vite faz o mesmo. Mesma origem, sem CORS.
const PREFIXO_API = '/api'

function ehCodigoConhecido(codigo: unknown): codigo is CodigoErro {
  return typeof codigo === 'string' && (CODIGOS_ERRO as readonly string[]).includes(codigo)
}

/**
 * Por que a chamada falhou:
 * - `rede`: não houve resposta (servidor fora do ar, conexão recusada);
 * - `resposta-invalida`: houve resposta, mas não no formato esperado (ex.: 502 em HTML do proxy);
 * - `problema`: a API respondeu com Problem Details; a decisão usa o `codigo`, nunca o texto.
 */
export type FalhaApi =
  | { tipo: 'rede' }
  | { tipo: 'resposta-invalida'; status: number }
  | { tipo: 'problema'; status: number; codigo: CodigoErro | undefined; problema: ProblemDetails }

export class ErroApi extends Error {
  readonly falha: FalhaApi

  constructor(falha: FalhaApi) {
    super(falha.tipo === 'problema' ? `HTTP ${falha.status} ${falha.codigo ?? ''}` : falha.tipo)
    this.name = 'ErroApi'
    this.falha = falha
  }

  get codigo(): CodigoErro | undefined {
    return this.falha.tipo === 'problema' ? this.falha.codigo : undefined
  }

  get problema(): ProblemDetails | undefined {
    return this.falha.tipo === 'problema' ? this.falha.problema : undefined
  }
}

/** Requisição cancelada por AbortSignal (filtro ou página trocados antes da resposta): deve ser ignorada. */
export function foiCancelada(erro: unknown): boolean {
  return erro instanceof DOMException && erro.name === 'AbortError'
}

type ValorDeConsulta = string | number | undefined

export async function obterJson<T>(
  caminho: string,
  consulta?: Readonly<Record<string, ValorDeConsulta>>,
  sinal?: AbortSignal,
): Promise<T> {
  return enviar<T>(montarUrl(caminho, consulta), { method: 'GET', headers: { Accept: 'application/json' } }, sinal)
}

export async function postarJson<T>(caminho: string, corpo: unknown, sinal?: AbortSignal): Promise<T> {
  return enviar<T>(
    montarUrl(caminho),
    {
      method: 'POST',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify(corpo),
    },
    sinal,
  )
}

function montarUrl(caminho: string, consulta?: Readonly<Record<string, ValorDeConsulta>>): string {
  const parametros = new URLSearchParams()
  for (const [nome, valor] of Object.entries(consulta ?? {})) {
    if (valor !== undefined) {
      parametros.append(nome, String(valor))
    }
  }
  const textoConsulta = parametros.toString()
  return `${PREFIXO_API}${caminho}${textoConsulta === '' ? '' : `?${textoConsulta}`}`
}

async function enviar<T>(url: string, opcoes: RequestInit, sinal?: AbortSignal): Promise<T> {
  let resposta: Response
  try {
    resposta = await fetch(url, { ...opcoes, signal: sinal ?? null })
  } catch (erro) {
    if (foiCancelada(erro)) {
      throw erro
    }
    throw new ErroApi({ tipo: 'rede' })
  }

  const tipoConteudo = resposta.headers.get('Content-Type') ?? ''
  const ehJson = /application\/(problem\+)?json/i.test(tipoConteudo)

  if (resposta.ok) {
    if (!ehJson) {
      throw new ErroApi({ tipo: 'resposta-invalida', status: resposta.status })
    }
    return (await resposta.json()) as T
  }

  if (!ehJson) {
    throw new ErroApi({ tipo: 'resposta-invalida', status: resposta.status })
  }
  let problema: ProblemDetails
  try {
    problema = (await resposta.json()) as ProblemDetails
  } catch (erro) {
    if (foiCancelada(erro)) {
      throw erro
    }
    throw new ErroApi({ tipo: 'resposta-invalida', status: resposta.status })
  }
  const codigo = ehCodigoConhecido(problema.codigo) ? problema.codigo : undefined
  throw new ErroApi({ tipo: 'problema', status: resposta.status, codigo, problema })
}
