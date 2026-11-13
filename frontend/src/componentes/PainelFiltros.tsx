import { CircleAlert, FunnelX, Search, Users } from 'lucide-react'
import { useEffect, useRef } from 'react'
import type { ClienteCatalogo } from '../tipos/contratos.ts'
import type { CamposFiltro } from '../utilitarios/filtros.ts'
import { Controle } from './Controle.tsx'

interface Propriedades {
  campos: CamposFiltro
  clientes: readonly ClienteCatalogo[]
  falhaClientes: boolean
  erroDatas: string | null
  desabilitado: boolean
  aoAlterar: (campos: CamposFiltro) => void
  /** `dataIncompleta`: algum <input type="date"> tem texto que não forma uma data (o valor dele fica vazio). */
  aoFiltrar: (dataIncompleta: boolean) => void
  aoLimpar: () => void
  /** Muda a cada limpeza (Limpar ou pedido criado): apaga também uma data digitada pela metade. */
  versaoLimpeza: number
}

/** Filtros da listagem (spec §6.2, item 2). Os homônimos se distinguem pelo e-mail: "Nome (email)". */
export function PainelFiltros({
  campos,
  clientes,
  falhaClientes,
  erroDatas,
  desabilitado,
  aoAlterar,
  aoFiltrar,
  aoLimpar,
  versaoLimpeza,
}: Propriedades) {
  const refFormulario = useRef<HTMLFormElement>(null)

  // Uma data incompleta ("15/mm/aaaa") tem valor vazio, igual ao estado: o React não reescreve o campo ao limpar.
  useEffect(() => {
    for (const campo of refFormulario.current?.querySelectorAll<HTMLInputElement>('input[type="date"]') ?? []) {
      if (campo.validity.badInput) {
        campo.value = ''
      }
    }
  }, [versaoLimpeza])

  return (
    <form
      ref={refFormulario}
      className="filtros"
      aria-label="Filtros de pedidos"
      noValidate
      onSubmit={(evento) => {
        evento.preventDefault()
        const datas = evento.currentTarget.querySelectorAll<HTMLInputElement>('input[type="date"]')
        aoFiltrar([...datas].some((campo) => campo.validity.badInput))
      }}
    >
      <div className="campo">
        <label htmlFor="filtro-cliente">Cliente</label>
        <Controle icone={Users} selecao>
          <select
            id="filtro-cliente"
            value={campos.clienteId}
            disabled={desabilitado}
            onChange={(evento) => {
              aoAlterar({ ...campos, clienteId: evento.target.value })
            }}
          >
            <option value="">Todos os clientes</option>
            {clientes.map((cliente) => (
              <option key={cliente.id} value={String(cliente.id)}>
                {`${cliente.nome} (${cliente.email})`}
              </option>
            ))}
          </select>
        </Controle>
        {falhaClientes && (
          <p className="campo__erro" role="alert">
            <CircleAlert size={16} />
            Não foi possível carregar a lista de clientes.
          </p>
        )}
      </div>

      <div className="campo">
        <label htmlFor="filtro-data-inicio">Data inicial</label>
        {/* Sem ícone próprio: o seletor nativo do campo de data já mostra o calendário. */}
        <input
          id="filtro-data-inicio"
          type="date"
          value={campos.dataInicio}
          max={campos.dataFim === '' ? undefined : campos.dataFim}
          disabled={desabilitado}
          aria-invalid={erroDatas !== null}
          aria-describedby={erroDatas === null ? undefined : 'filtro-erro-datas'}
          onChange={(evento) => {
            aoAlterar({ ...campos, dataInicio: evento.target.value })
          }}
        />
      </div>

      <div className="campo">
        <label htmlFor="filtro-data-fim">Data final</label>
        <input
          id="filtro-data-fim"
          type="date"
          value={campos.dataFim}
          min={campos.dataInicio === '' ? undefined : campos.dataInicio}
          disabled={desabilitado}
          aria-invalid={erroDatas !== null}
          aria-describedby={erroDatas === null ? undefined : 'filtro-erro-datas'}
          onChange={(evento) => {
            aoAlterar({ ...campos, dataFim: evento.target.value })
          }}
        />
      </div>

      <div className="filtros__acoes">
        <button type="submit" className="botao botao--primario" disabled={desabilitado}>
          <Search />
          Filtrar
        </button>
        <button type="button" className="botao botao--secundario" disabled={desabilitado} onClick={aoLimpar}>
          <FunnelX />
          Limpar
        </button>
      </div>

      {/* O erro vale para o período (as duas datas) e fica logo abaixo delas. */}
      {erroDatas !== null && (
        <p id="filtro-erro-datas" className="campo__erro filtros__erro" role="alert">
          <CircleAlert size={16} />
          {erroDatas}
        </p>
      )}
    </form>
  )
}
