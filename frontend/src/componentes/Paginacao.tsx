import { ChevronLeft, ChevronRight } from 'lucide-react'
import type { Ref } from 'react'
import { pluralizar } from '../utilitarios/formatacao.ts'

interface Propriedades {
  pagina: number
  totalPaginas: number
  totalItens: number
  desabilitada: boolean
  aoMudarDePagina: (pagina: number) => void
  /** Usada pela página para levar o foco ao botão ainda habilitado quando o outro fica desabilitado no limite. */
  ref?: Ref<HTMLElement>
}

/** Paginação (spec §6.2, item 4): "Página X de Y · N pedidos", com Anterior/Próxima desabilitados nos limites. */
export function Paginacao({ pagina, totalPaginas, totalItens, desabilitada, aoMudarDePagina, ref }: Propriedades) {
  const ultimaPagina = Math.max(totalPaginas, 1)
  return (
    <nav ref={ref} className="paginacao" aria-label="Paginação">
      <p className="paginacao__situacao">
        Página <strong>{pagina}</strong> de <strong>{ultimaPagina}</strong> ·{' '}
        {pluralizar(totalItens, 'pedido', 'pedidos')}
      </p>
      <div className="paginacao__botoes">
        <button
          type="button"
          className="botao botao--secundario"
          disabled={desabilitada || pagina <= 1}
          onClick={() => {
            aoMudarDePagina(pagina - 1)
          }}
        >
          <ChevronLeft />
          Anterior
        </button>
        <button
          type="button"
          className="botao botao--secundario"
          disabled={desabilitada || pagina >= totalPaginas}
          onClick={() => {
            aoMudarDePagina(pagina + 1)
          }}
        >
          Próxima
          <ChevronRight />
        </button>
      </div>
    </nav>
  )
}
