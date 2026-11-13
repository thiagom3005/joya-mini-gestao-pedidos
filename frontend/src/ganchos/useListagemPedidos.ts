import { useEffect, useState } from 'react'
import { foiCancelada } from '../api/clienteHttp.ts'
import { buscarPedidos } from '../api/endpoints.ts'
import type { FiltroPedidos, ListaPedidos } from '../tipos/contratos.ts'

/** Tamanho fixo de página da listagem (spec §6.2, item 4). */
export const TAMANHO_DA_PAGINA = 10

/** Filtros já aplicados (não o que está digitado no formulário). Datas civis "yyyy-MM-dd". */
export type FiltrosAplicados = Omit<FiltroPedidos, 'pagina' | 'tamanhoPagina'>

interface Consulta {
  filtros: FiltrosAplicados
  pagina: number
  /** Muda a cada pedido de consulta, inclusive para recarregar os mesmos filtros e página. */
  versao: number
}

export interface ListagemPedidos {
  dados: ListaPedidos | null
  carregando: boolean
  falhou: boolean
  aplicarFiltros: (filtros: FiltrosAplicados) => void
  irParaPagina: (pagina: number) => void
  recarregar: () => void
}

/**
 * Estado da listagem de pedidos (spec §6.2). Cada consulta cancela a anterior (AbortController): uma resposta antiga
 * nunca sobrescreve um filtro ou página mais recente.
 */
export function useListagemPedidos(): ListagemPedidos {
  const [consulta, definirConsulta] = useState<Consulta>({ filtros: {}, pagina: 1, versao: 0 })
  const [dados, definirDados] = useState<ListaPedidos | null>(null)
  const [carregando, definirCarregando] = useState(true)
  const [falhou, definirFalhou] = useState(false)

  useEffect(() => {
    const controlador = new AbortController()
    buscarPedidos(
      { ...consulta.filtros, pagina: consulta.pagina, tamanhoPagina: TAMANHO_DA_PAGINA },
      controlador.signal,
    ).then(
      (resposta) => {
        definirDados(resposta)
        definirFalhou(false)
        definirCarregando(false)
      },
      (erro: unknown) => {
        if (foiCancelada(erro)) {
          return
        }
        definirFalhou(true)
        definirCarregando(false)
      },
    )
    return () => {
      controlador.abort()
    }
  }, [consulta])

  function consultar(alterar: (atual: Consulta) => Omit<Consulta, 'versao'>): void {
    definirCarregando(true)
    definirFalhou(false)
    definirConsulta((atual) => ({ ...alterar(atual), versao: atual.versao + 1 }))
  }

  return {
    dados,
    carregando,
    falhou,
    aplicarFiltros: (filtros) => {
      consultar(() => ({ filtros, pagina: 1 }))
    },
    irParaPagina: (pagina) => {
      consultar((atual) => ({ filtros: atual.filtros, pagina }))
    },
    recarregar: () => {
      consultar((atual) => ({ filtros: atual.filtros, pagina: atual.pagina }))
    },
  }
}
