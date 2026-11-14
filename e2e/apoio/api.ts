import { expect, type APIRequestContext } from '@playwright/test'

/** Contratos da spec §5, só com os campos usados pelos testes. */
export interface Cliente {
  id: number
  nome: string
  email: string
}

export interface Produto {
  id: number
  nome: string
  preco: number
  estoque: number
}

export interface PedidoCriado {
  id: number
  cliente: { id: number; nome: string }
  dataPedido: string
  itens: { produtoId: number; quantidade: number; precoUnitario: number }[]
  valorTotal: number
}

export interface ListaPedidos {
  itens: { id: number; cliente: { id: number; nome: string }; dataPedido: string; valorTotal: number }[]
  pagina: number
  tamanhoPagina: number
  totalItens: number
  totalPaginas: number
}

export interface FiltroPedidos {
  clienteId?: number
  dataInicio?: string
  dataFim?: string
  pagina?: number
}

async function obter<T>(request: APIRequestContext, caminho: string): Promise<T> {
  const resposta = await request.get(caminho)
  expect(resposta.status(), `GET ${caminho}`).toBe(200)
  return (await resposta.json()) as T
}

export const buscarClientes = (request: APIRequestContext): Promise<Cliente[]> => obter(request, '/api/clientes')

export const buscarProdutos = (request: APIRequestContext): Promise<Produto[]> => obter(request, '/api/produtos')

/** Dois clientes com o mesmo nome, em ordem de id (o seed tem dois "Ana Souza", spec §4.7). */
export async function clientesHomonimos(request: APIRequestContext): Promise<[Cliente, Cliente]> {
  const clientes = (await buscarClientes(request)).toSorted((a, b) => a.id - b.id)
  for (const cliente of clientes) {
    const outro = clientes.find((candidato) => candidato.id > cliente.id && candidato.nome === cliente.nome)
    if (outro !== undefined) {
      return [cliente, outro]
    }
  }
  throw new Error('O seed deveria ter clientes homônimos (spec §4.7).')
}

export async function buscarProduto(request: APIRequestContext, produtoId: number): Promise<Produto> {
  const produto = (await buscarProdutos(request)).find((candidato) => candidato.id === produtoId)
  expect(produto, `produto ${produtoId} no catálogo`).toBeDefined()
  return produto as Produto
}

/**
 * Produtos de estoque alto, 1.000 ou mais (spec §4.7 e §10.5): os testes consomem poucas unidades e podem repetir
 * no mesmo stack.
 */
export async function produtosComMaisEstoque(request: APIRequestContext, quantidade: number): Promise<Produto[]> {
  const ordenados = (await buscarProdutos(request)).toSorted((a, b) => b.estoque - a.estoque || a.id - b.id)
  const escolhidos = ordenados.slice(0, quantidade)
  expect(escolhidos.every((produto) => produto.estoque >= 1000), 'produtos com estoque alto no seed').toBe(true)
  return escolhidos
}

export async function listarPedidos(request: APIRequestContext, filtro: FiltroPedidos = {}): Promise<ListaPedidos> {
  const parametros = new URLSearchParams({ pagina: String(filtro.pagina ?? 1), tamanhoPagina: '10' })
  if (filtro.clienteId !== undefined) parametros.set('clienteId', String(filtro.clienteId))
  if (filtro.dataInicio !== undefined) parametros.set('dataInicio', filtro.dataInicio)
  if (filtro.dataFim !== undefined) parametros.set('dataFim', filtro.dataFim)
  return obter(request, `/api/pedidos?${parametros.toString()}`)
}

export async function criarPedidoPelaApi(
  request: APIRequestContext,
  clienteId: number,
  itens: { produtoId: number; quantidade: number }[],
): Promise<PedidoCriado> {
  const resposta = await request.post('/api/pedidos', { data: { clienteId, itens } })
  expect(resposta.status(), 'POST /api/pedidos').toBe(201)
  return (await resposta.json()) as PedidoCriado
}

/** Data civil de São Paulo (yyyy-MM-dd) de um instante UTC, como a API interpreta os filtros (spec §8.2). */
export function dataEmSaoPaulo(instanteUtc: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'America/Sao_Paulo' }).format(new Date(instanteUtc))
}

/** Valor em reais como a interface mostra (spec §6.5). */
export const formatarMoeda = (valor: number): string =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(valor)

export const formatarNumero = (valor: number): string => valor.toLocaleString('pt-BR')
