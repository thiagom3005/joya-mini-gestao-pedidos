import type { FiltrosAplicados } from '../ganchos/useListagemPedidos.ts'

/** Texto dos campos de filtro como estão no formulário. Datas no formato do <input type="date">: "yyyy-MM-dd". */
export interface CamposFiltro {
  clienteId: string
  dataInicio: string
  dataFim: string
}

export const CAMPOS_FILTRO_VAZIOS: CamposFiltro = { clienteId: '', dataInicio: '', dataFim: '' }

/**
 * Valida o período antes de consultar (spec §6.2, item 2): data inicial maior que a final é erro local, sem
 * requisição. Datas "yyyy-MM-dd" comparam corretamente como texto, sem converter para Date nem depender de fuso.
 */
export function validarPeriodo(campos: CamposFiltro, dataIncompleta: boolean): string | null {
  if (dataIncompleta) {
    return 'Complete as datas no formato dd/mm/aaaa ou deixe o campo em branco.'
  }
  if (campos.dataInicio !== '' && campos.dataFim !== '' && campos.dataInicio > campos.dataFim) {
    return 'A data inicial deve ser anterior ou igual à data final.'
  }
  return null
}

export function montarFiltros(campos: CamposFiltro): FiltrosAplicados {
  const filtros: FiltrosAplicados = {}
  if (campos.clienteId !== '') {
    filtros.clienteId = Number(campos.clienteId)
  }
  if (campos.dataInicio !== '') {
    filtros.dataInicio = campos.dataInicio
  }
  if (campos.dataFim !== '') {
    filtros.dataFim = campos.dataFim
  }
  return filtros
}
