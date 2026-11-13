import {
  CircleAlert,
  CircleCheck,
  ClipboardList,
  LayoutList,
  LoaderCircle,
  Plus,
  RefreshCw,
  SearchX,
  ShoppingCart,
  X,
} from 'lucide-react'
import { useEffect, useRef, useState } from 'react'
import { foiCancelada } from './api/clienteHttp.ts'
import { buscarClientes } from './api/endpoints.ts'
import { ModalNovoPedido } from './componentes/ModalNovoPedido.tsx'
import { Paginacao } from './componentes/Paginacao.tsx'
import { PainelFiltros } from './componentes/PainelFiltros.tsx'
import { TabelaPedidos } from './componentes/TabelaPedidos.tsx'
import { useListagemPedidos } from './ganchos/useListagemPedidos.ts'
import type { ClienteCatalogo, Pedido } from './tipos/contratos.ts'
import { CAMPOS_FILTRO_VAZIOS, montarFiltros, validarPeriodo, type CamposFiltro } from './utilitarios/filtros.ts'

/** Página única "Gestão de Pedidos" (spec §6.2): listagem com filtros e paginação, e o modal de criação (§6.3). */
export function App() {
  const listagem = useListagemPedidos()
  const [campos, definirCampos] = useState<CamposFiltro>(CAMPOS_FILTRO_VAZIOS)
  const [erroDatas, definirErroDatas] = useState<string | null>(null)
  const [versaoLimpeza, definirVersaoLimpeza] = useState(0)
  const [clientes, definirClientes] = useState<ClienteCatalogo[]>([])
  const [falhaClientes, definirFalhaClientes] = useState(false)
  const [versaoClientes, definirVersaoClientes] = useState(0)
  const [modalAberto, definirModalAberto] = useState(false)
  const [mensagemSucesso, definirMensagemSucesso] = useState<string | null>(null)
  const refFocoDaConsulta = useRef<HTMLElement | null>(null)
  const refTitulo = useRef<HTMLHeadingElement>(null)
  const refTentarNovamente = useRef<HTMLButtonElement>(null)
  const refPaginacao = useRef<HTMLElement>(null)
  const { dados, carregando, falhou } = listagem
  const semLinhas = dados === null || dados.itens.length === 0

  // Filtrar, Limpar, Anterior e Próxima ficam desabilitados durante o carregamento (spec §6.2, item 5) e, desabilitados,
  // perdem o foco. Ao terminar, se o foco ficou perdido, ele volta ao controle que disparou a consulta. Quando esse
  // controle não pode mais recebê-lo (Próxima desabilitada na última página, "Tentar novamente" que saiu da tela), vai
  // para o botão de paginação ainda habilitado, para o "Tentar novamente" de uma nova falha ou para o título da seção.
  useEffect(() => {
    const controle = refFocoDaConsulta.current
    if (carregando || controle === null) {
      return
    }
    refFocoDaConsulta.current = null
    if (document.activeElement !== document.body) {
      return
    }
    const alvo = podeReceberFoco(controle)
      ? controle
      : falhou
        ? refTentarNovamente.current
        : (botaoHabilitadoDaPaginacao(refPaginacao.current, controle) ?? refTitulo.current)
    alvo?.focus()
  }, [carregando, falhou])

  function lembrarFoco(): void {
    refFocoDaConsulta.current = document.activeElement instanceof HTMLElement ? document.activeElement : null
  }

  // Clientes do filtro (spec §6.2, item 1).
  useEffect(() => {
    const controlador = new AbortController()
    buscarClientes(controlador.signal).then(
      (resposta) => {
        definirClientes(resposta)
        definirFalhaClientes(false)
      },
      (erro: unknown) => {
        if (!foiCancelada(erro)) {
          definirFalhaClientes(true)
        }
      },
    )
    return () => {
      controlador.abort()
    }
  }, [versaoClientes])

  function filtrar(dataIncompleta: boolean): void {
    const erro = validarPeriodo(campos, dataIncompleta)
    definirErroDatas(erro)
    if (erro === null) {
      lembrarFoco()
      listagem.aplicarFiltros(montarFiltros(campos))
    }
  }

  function limparFiltros(): void {
    lembrarFoco()
    definirCampos(CAMPOS_FILTRO_VAZIOS)
    definirErroDatas(null)
    definirVersaoLimpeza((versao) => versao + 1)
    listagem.aplicarFiltros({})
  }

  function tentarNovamente(): void {
    lembrarFoco()
    listagem.recarregar()
    if (falhaClientes) {
      definirVersaoClientes((versao) => versao + 1)
    }
  }

  function abrirModal(): void {
    definirMensagemSucesso(null)
    definirModalAberto(true)
  }

  function fecharModal(): void {
    definirModalAberto(false)
  }

  // O botão some junto com a mensagem; o foco vai para o título da seção, em vez de cair no início da página.
  function dispensarMensagem(): void {
    definirMensagemSucesso(null)
    refTitulo.current?.focus()
  }

  // 201 (spec §6.4): fecha o modal, avisa, limpa os filtros e volta à página 1, onde o pedido aparece no topo.
  function pedidoCriado(pedido: Pedido): void {
    definirModalAberto(false)
    definirMensagemSucesso(`Pedido nº ${pedido.id} criado com sucesso.`)
    limparFiltros()
  }

  function mudarDePagina(pagina: number): void {
    lembrarFoco()
    listagem.irParaPagina(pagina)
  }

  return (
    <>
      <header className="cabecalho">
        <div className="cabecalho__conteudo">
          <div className="marca">
            <span className="marca__simbolo" aria-hidden="true">
              <ShoppingCart size={24} />
            </span>
            <div>
              <h1 className="marca__titulo">Gestão de Pedidos</h1>
              <p className="marca__subtitulo">Consulte, filtre e cadastre pedidos de clientes.</p>
            </div>
          </div>
          <button type="button" className="botao botao--destaque botao--grande" onClick={abrirModal}>
            <Plus />
            Novo pedido
          </button>
        </div>
      </header>

      <main className="conteudo">
        <section className="painel" aria-labelledby="titulo-pedidos">
          <div className="painel__topo">
            <div>
              <h2 id="titulo-pedidos" ref={refTitulo} className="painel__titulo" tabIndex={-1}>
                Pedidos
              </h2>
              <p className="painel__descricao">Mais recentes primeiro.</p>
            </div>
            {/* Só números devolvidos pela API; durante a carga, os anteriores ficam esmaecidos. */}
            {!falhou && (
              <dl className="resumo" aria-busy={carregando}>
                <div className="resumo__item">
                  <dt>
                    <span className="resumo__icone">
                      <ClipboardList />
                    </span>
                    Pedidos encontrados
                  </dt>
                  <dd>{dados === null ? '—' : dados.totalItens.toLocaleString('pt-BR')}</dd>
                </div>
                <div className="resumo__item resumo__item--pagina">
                  <dt>
                    <span className="resumo__icone">
                      <LayoutList />
                    </span>
                    Exibidos nesta página
                  </dt>
                  <dd>{dados === null ? '—' : dados.itens.length.toLocaleString('pt-BR')}</dd>
                </div>
              </dl>
            )}
          </div>

          <PainelFiltros
            campos={campos}
            clientes={clientes}
            falhaClientes={falhaClientes}
            erroDatas={erroDatas}
            desabilitado={carregando}
            aoAlterar={(novosCampos) => {
              definirCampos(novosCampos)
              if (erroDatas !== null) {
                definirErroDatas(null)
              }
            }}
            aoFiltrar={filtrar}
            aoLimpar={limparFiltros}
            versaoLimpeza={versaoLimpeza}
          />

          {/* A região aria-live existe sempre e só contém a mensagem; o botão de dispensar fica fora do anúncio. */}
          <div className={mensagemSucesso === null ? undefined : 'aviso aviso--sucesso'}>
            <div className="regiao-sucesso" aria-live="polite">
              {mensagemSucesso !== null && (
                <>
                  <span className="aviso__selo">
                    <CircleCheck />
                  </span>
                  <p className="aviso__titulo">{mensagemSucesso}</p>
                </>
              )}
            </div>
            {mensagemSucesso !== null && (
              <button type="button" className="botao-icone" aria-label="Dispensar mensagem" onClick={dispensarMensagem}>
                <X />
              </button>
            )}
          </div>

          <div className="painel__dados">
            {carregando && (
              <p className={semLinhas ? 'estado estado--carregando' : 'estado-flutuante'} role="status">
                <LoaderCircle className="girando" size={semLinhas ? 24 : 16} />
                Carregando pedidos…
              </p>
            )}

            {!carregando && falhou && (
              <div className="estado estado--erro" role="alert">
                <span className="estado__icone">
                  <CircleAlert size={24} />
                </span>
                <p className="estado__titulo">Não foi possível carregar os pedidos.</p>
                <button
                  type="button"
                  ref={refTentarNovamente}
                  className="botao botao--primario"
                  onClick={tentarNovamente}
                >
                  <RefreshCw />
                  Tentar novamente
                </button>
              </div>
            )}

            {!carregando && !falhou && dados !== null && dados.itens.length === 0 && (
              <div className="estado">
                <span className="estado__icone">
                  <SearchX size={24} />
                </span>
                <p className="estado__titulo">Nenhum pedido encontrado para os filtros informados.</p>
              </div>
            )}

            {!falhou && dados !== null && dados.itens.length > 0 && (
              <TabelaPedidos pedidos={dados.itens} ocupada={carregando} />
            )}
          </div>

          {!falhou && dados !== null && (
            <Paginacao
              ref={refPaginacao}
              pagina={dados.pagina}
              totalPaginas={dados.totalPaginas}
              totalItens={dados.totalItens}
              desabilitada={carregando}
              aoMudarDePagina={mudarDePagina}
            />
          )}
        </section>
      </main>

      {modalAberto && <ModalNovoPedido aoFechar={fecharModal} aoCriar={pedidoCriado} />}
    </>
  )
}

function podeReceberFoco(elemento: HTMLElement): boolean {
  return elemento.isConnected && !elemento.matches(':disabled')
}

/** Se o controle que perdeu o foco é da paginação, o outro botão dela, quando habilitado. */
function botaoHabilitadoDaPaginacao(paginacao: HTMLElement | null, controle: HTMLElement): HTMLButtonElement | null {
  return paginacao?.contains(controle) ? paginacao.querySelector<HTMLButtonElement>('button:not(:disabled)') : null
}
