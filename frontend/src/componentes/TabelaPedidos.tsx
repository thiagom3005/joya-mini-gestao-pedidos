import type { PedidoResumo } from '../tipos/contratos.ts'
import { formatarDataHora, formatarMoeda, iniciais } from '../utilitarios/formatacao.ts'

interface Propriedades {
  pedidos: readonly PedidoResumo[]
  ocupada: boolean
}

/**
 * Tabela de pedidos (spec §6.2, item 3), na ordem devolvida pela API (mais recentes primeiro). Em telas estreitas,
 * cada linha vira um cartão; o rótulo de cada célula vem de `data-rotulo`.
 */
export function TabelaPedidos({ pedidos, ocupada }: Propriedades) {
  return (
    <div className="tabela-container">
      <table className="tabela" aria-busy={ocupada}>
        <caption className="visualmente-oculto">Pedidos</caption>
        <thead>
          <tr>
            <th scope="col" className="tabela__coluna-numero">
              Nº
            </th>
            <th scope="col">Cliente</th>
            <th scope="col">Data</th>
            <th scope="col" className="numero">
              Itens
            </th>
            <th scope="col" className="numero">
              Valor total
            </th>
          </tr>
        </thead>
        <tbody>
          {pedidos.map((pedido) => (
            <tr key={pedido.id}>
              <td data-rotulo="Nº">
                <span className="tabela__numero">{pedido.id}</span>
              </td>
              <td data-rotulo="Cliente">
                <span className="cliente">
                  <span className="cliente__avatar" aria-hidden="true">
                    {iniciais(pedido.cliente.nome)}
                  </span>
                  <span className="cliente__nome">{pedido.cliente.nome}</span>
                </span>
              </td>
              <td data-rotulo="Data">
                <DataHora instanteUtc={pedido.dataPedido} />
              </td>
              <td data-rotulo="Itens" className="numero">
                <span className="etiqueta">{pedido.quantidadeItens}</span>
              </td>
              <td data-rotulo="Valor total" className="numero tabela__valor">
                {formatarMoeda(pedido.valorTotal)}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/**
 * Data e hora no formato da spec §6.5 ("31/10/2026, 23:59"), com a hora em tom secundário. Só a apresentação muda:
 * o texto é o mesmo de `formatarDataHora`, e o leitor de tela recebe a frase inteira.
 */
function DataHora({ instanteUtc }: { instanteUtc: string }) {
  const texto = formatarDataHora(instanteUtc)
  const separador = texto.indexOf(', ')
  return (
    <time className="tabela__data" dateTime={instanteUtc}>
      {separador < 0 ? (
        texto
      ) : (
        <>
          {texto.slice(0, separador + 1)} <span className="tabela__hora">{texto.slice(separador + 2)}</span>
        </>
      )}
    </time>
  )
}
