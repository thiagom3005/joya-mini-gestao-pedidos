// Contratos HTTP da API (spec §5), espelhando os records de backend/src/JoyaPedidos.Api/Modulos/*/Contratos.
// Instantes chegam em ISO 8601 UTC com "Z"; datas civis em "yyyy-MM-dd"; dinheiro como número JSON.

/** GET /clientes (spec §5.7). Ordenado por nome e id. */
export interface ClienteCatalogo {
  id: number
  nome: string
  email: string
}

/** GET /produtos (spec §5.8). Ordenado por nome e id; inclui produtos com estoque 0. */
export interface ProdutoCatalogo {
  id: number
  nome: string
  preco: number
  estoque: number
}

export interface ClienteResumo {
  id: number
  nome: string
}

/** Linha de GET /pedidos (spec §5.4). quantidadeItens = número de linhas de item, não a soma das quantidades. */
export interface PedidoResumo {
  id: number
  cliente: ClienteResumo
  dataPedido: string
  quantidadeItens: number
  valorTotal: number
}

/** Envelope paginado de GET /pedidos (spec §5.4). */
export interface ListaPedidos {
  itens: PedidoResumo[]
  pagina: number
  tamanhoPagina: number
  totalItens: number
  totalPaginas: number
}

/** Parâmetros de GET /pedidos. Datas civis "yyyy-MM-dd" (America/Sao_Paulo, inclusivas). */
export interface FiltroPedidos {
  clienteId?: number
  dataInicio?: string
  dataFim?: string
  pagina: number
  tamanhoPagina: number
}

export interface ItemSolicitado {
  produtoId: number
  quantidade: number
}

/** Corpo de POST /pedidos (spec §5.3). Preços nunca são enviados: a API usa o preço atual do produto. */
export interface CriarPedidoRequisicao {
  clienteId: number
  itens: ItemSolicitado[]
}

export interface ItemPedido {
  produtoId: number
  nomeProduto: string
  quantidade: number
  precoUnitario: number
  subtotal: number
}

/** 201 de POST /pedidos e 200 de GET /pedidos/{id} (spec §5.3, §5.5). */
export interface Pedido {
  id: number
  cliente: ClienteResumo
  dataPedido: string
  itens: ItemPedido[]
  valorTotal: number
}

/** Códigos estáveis de erro (spec §5.2). O frontend decide sempre por eles, nunca pelo texto. */
export const CODIGOS_ERRO = [
  'VALIDACAO',
  'REQUISICAO_INVALIDA',
  'RECURSO_NAO_ENCONTRADO',
  'CLIENTE_NAO_ENCONTRADO',
  'PRODUTO_NAO_ENCONTRADO',
  'ESTOQUE_INSUFICIENTE',
  'PEDIDO_NAO_ENCONTRADO',
  'CONFLITO_CONCORRENCIA',
  'ERRO_INTERNO',
] as const

export type CodigoErro = (typeof CODIGOS_ERRO)[number]

/** Extensão de 409 ESTOQUE_INSUFICIENTE (spec §5.3). */
export interface ItemSemEstoque {
  produtoId: number
  nomeProduto: string
  quantidadeSolicitada: number
  estoqueDisponivel: number
}

/** Problem Details (RFC 9457) com as extensões da spec §5.2. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  codigo?: string
  traceId?: string
  errors?: Record<string, string[]>
  produtosNaoEncontrados?: number[]
  itensSemEstoque?: ItemSemEstoque[]
}
