import type {
  ClienteCatalogo,
  CriarPedidoRequisicao,
  FiltroPedidos,
  ListaPedidos,
  Pedido,
  ProdutoCatalogo,
} from '../tipos/contratos.ts'
import { obterJson, postarJson } from './clienteHttp.ts'

// Endpoints usados pela interface (spec §5.3, §5.4, §5.7, §5.8).

export function buscarClientes(sinal?: AbortSignal): Promise<ClienteCatalogo[]> {
  return obterJson<ClienteCatalogo[]>('/clientes', undefined, sinal)
}

export function buscarProdutos(sinal?: AbortSignal): Promise<ProdutoCatalogo[]> {
  return obterJson<ProdutoCatalogo[]>('/produtos', undefined, sinal)
}

export function buscarPedidos(filtro: FiltroPedidos, sinal?: AbortSignal): Promise<ListaPedidos> {
  return obterJson<ListaPedidos>(
    '/pedidos',
    {
      clienteId: filtro.clienteId,
      dataInicio: filtro.dataInicio,
      dataFim: filtro.dataFim,
      pagina: filtro.pagina,
      tamanhoPagina: filtro.tamanhoPagina,
    },
    sinal,
  )
}

export function criarPedido(requisicao: CriarPedidoRequisicao, sinal?: AbortSignal): Promise<Pedido> {
  return postarJson<Pedido>('/pedidos', requisicao, sinal)
}
