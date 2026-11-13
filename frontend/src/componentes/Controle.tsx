import { ChevronDown, type LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

interface Propriedades {
  /** Ícone decorativo à esquerda do campo. */
  icone?: LucideIcon
  /** Seletor: mostra a seta própria, porque a nativa é escondida para manter o mesmo visual em todos os navegadores. */
  selecao?: boolean
  children: ReactNode
}

/** Moldura visual de um campo de formulário. Não altera o comportamento do campo que envolve. */
export function Controle({ icone: Icone, selecao = false, children }: Propriedades) {
  return (
    <div className={Icone === undefined ? 'controle' : 'controle controle--com-icone'}>
      {Icone !== undefined && <Icone className="controle__icone" />}
      {children}
      {selecao && <ChevronDown className="controle__seta" />}
    </div>
  )
}
