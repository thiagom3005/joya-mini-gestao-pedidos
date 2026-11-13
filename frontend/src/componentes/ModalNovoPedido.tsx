import {
  Check,
  CircleAlert,
  LoaderCircle,
  Package,
  Plus,
  RefreshCw,
  ShoppingCart,
  Trash,
  TriangleAlert,
  Users,
  X,
} from 'lucide-react'
import { useEffect, useMemo, useRef, useState, type ReactNode } from 'react'
import { ErroApi, foiCancelada } from '../api/clienteHttp.ts'
import { buscarClientes, buscarProdutos, criarPedido } from '../api/endpoints.ts'
import type { ClienteCatalogo, ItemSemEstoque, Pedido, ProdutoCatalogo } from '../tipos/contratos.ts'
import { formatarCentavos, formatarMoeda, pluralizar } from '../utilitarios/formatacao.ts'
import { Controle } from './Controle.tsx'
import {
  MAXIMO_DE_ITENS,
  SEM_ERROS,
  limparErroDaLinha,
  limparErroDoCliente,
  mapearErrosDaApi,
  quantidadeMaxima,
  resumirErros,
  subtotalEstimadoCentavos,
  totalEstimadoCentavos,
  validarFormulario,
  type ErrosFormulario,
  type LinhaItem,
} from '../utilitarios/pedidoFormulario.ts'

interface Propriedades {
  aoFechar: () => void
  aoCriar: (pedido: Pedido) => void
}

/** Mensagem geral do formulário, escolhida pelo `codigo` do Problem Details (spec §6.4). */
type Aviso =
  | { tipo: 'validacao'; resumo: string[] }
  | { tipo: 'nao-encontrado'; texto: string }
  | { tipo: 'estoque'; itens: ItemSemEstoque[] }
  | { tipo: 'conflito' }
  | { tipo: 'generico'; traceId?: string }

type EstadoCatalogo = 'carregando' | 'pronto' | 'erro'

const LINHA_INICIAL: LinhaItem = { chave: 1, produtoId: '', quantidade: '1' }

/**
 * Modal "Novo pedido" (spec §6.3) com <dialog> nativo: foco preso no modal e Esc tratados pelo navegador. É montado
 * a cada abertura, então Cancelar/Esc descartam tudo. Enquanto o pedido é criado, o modal não fecha: os botões ficam
 * desabilitados, o Esc é cancelado e, se o navegador fechar o diálogo mesmo assim (ele pode ignorar um segundo Esc
 * sem nova interação), o diálogo é reaberto.
 */
export function ModalNovoPedido({ aoFechar, aoCriar }: Propriedades) {
  const refDialogo = useRef<HTMLDialogElement>(null)
  const refCliente = useRef<HTMLSelectElement>(null)
  const proximaChave = useRef(LINHA_INICIAL.chave + 1)
  const refEnviando = useRef(false)
  const refUltimaRecarga = useRef(0)
  const refBotaoEnviar = useRef<HTMLButtonElement>(null)
  const refDevolverFoco = useRef(false)

  const [clientes, definirClientes] = useState<ClienteCatalogo[]>([])
  const [produtos, definirProdutos] = useState<ProdutoCatalogo[]>([])
  const [estadoCatalogo, definirEstadoCatalogo] = useState<EstadoCatalogo>('carregando')
  const [versaoCatalogo, definirVersaoCatalogo] = useState(0)
  const [clienteId, definirClienteId] = useState('')
  const [linhas, definirLinhas] = useState<LinhaItem[]>([LINHA_INICIAL])
  const [erros, definirErros] = useState<ErrosFormulario>(SEM_ERROS)
  const [aviso, definirAviso] = useState<Aviso | null>(null)
  const [criando, definirCriando] = useState(false)

  const produtosPorId = useMemo(() => new Map(produtos.map((produto) => [produto.id, produto])), [produtos])

  // Último estado renderizado do formulário, lido depois de esperas assíncronas (recarga do catálogo após 409/422).
  const refFormulario = useRef({ clienteId, linhas })
  useEffect(() => {
    refFormulario.current = { clienteId, linhas }
  }, [clienteId, linhas])

  useEffect(() => {
    const dialogo = refDialogo.current
    if (dialogo !== null && !dialogo.open) {
      dialogo.showModal()
    }
    refCliente.current?.focus()
  }, [])

  // Desabilitado durante o envio (spec §6.3, item 6), o botão perde o foco. Se o envio falhar, o foco volta a ele assim
  // que estiver habilitado de novo, em vez de ficar perdido no início da página.
  useEffect(() => {
    if (criando || !refDevolverFoco.current) {
      return
    }
    refDevolverFoco.current = false
    const ativo = document.activeElement
    if (ativo === null || ativo === document.body || ativo === refDialogo.current) {
      refBotaoEnviar.current?.focus()
    }
  }, [criando])

  // Ao abrir (e em "Tentar novamente"): clientes e produtos atuais, para mostrar o estoque de agora (spec §6.3, item 1).
  useEffect(() => {
    const controlador = new AbortController()
    Promise.all([buscarClientes(controlador.signal), buscarProdutos(controlador.signal)]).then(
      ([novosClientes, novosProdutos]) => {
        definirClientes(novosClientes)
        definirProdutos(novosProdutos)
        definirEstadoCatalogo('pronto')
      },
      (erro: unknown) => {
        if (!foiCancelada(erro)) {
          definirEstadoCatalogo('erro')
        }
      },
    )
    return () => {
      controlador.abort()
    }
  }, [versaoCatalogo])

  function tentarCarregarDeNovo(): void {
    definirEstadoCatalogo('carregando')
    definirVersaoCatalogo((versao) => versao + 1)
  }

  function fechar(): void {
    if (!refEnviando.current && !criando) {
      refDialogo.current?.close()
    }
  }

  function alterarLinha(chave: number, campo: 'produtoId' | 'quantidade', valor: string): void {
    definirLinhas((atuais) => atuais.map((linha) => (linha.chave === chave ? { ...linha, [campo]: valor } : linha)))
    definirErros((atuais) => limparErroDaLinha(atuais, chave, campo === 'produtoId' ? 'produto' : 'quantidade'))
  }

  function adicionarLinha(): void {
    const chave = proximaChave.current
    proximaChave.current += 1
    definirLinhas((atuais) => [...atuais, { chave, produtoId: '', quantidade: '1' }])
  }

  function removerLinha(chave: number): void {
    definirLinhas((atuais) => atuais.filter((linha) => linha.chave !== chave))
  }

  /**
   * Recarrega o catálogo depois de 409/422 e desfaz seleções que deixaram de existir, preservando o resto. Só a
   * recarga mais recente é aplicada: uma resposta antiga, que chegue depois, é descartada.
   */
  async function recarregarCatalogo(incluirClientes: boolean): Promise<void> {
    refUltimaRecarga.current += 1
    const numeroDaRecarga = refUltimaRecarga.current
    let novosClientes: ClienteCatalogo[]
    let novosProdutos: ProdutoCatalogo[]
    try {
      ;[novosClientes, novosProdutos] = await Promise.all([
        incluirClientes ? buscarClientes() : Promise.resolve(clientes),
        buscarProdutos(),
      ])
    } catch {
      return // Mantém o catálogo anterior; o aviso do envio já está na tela.
    }
    if (numeroDaRecarga !== refUltimaRecarga.current) {
      return
    }
    definirClientes(novosClientes)
    definirProdutos(novosProdutos)

    const formulario = refFormulario.current
    const clientesValidos = new Set(novosClientes.map((cliente) => String(cliente.id)))
    const produtosValidos = new Set(novosProdutos.map((produto) => String(produto.id)))
    const produtoSumiu = (produtoId: string): boolean => produtoId !== '' && !produtosValidos.has(produtoId)
    const clienteSumiu = formulario.clienteId !== '' && !clientesValidos.has(formulario.clienteId)
    const chavesSemProduto = formulario.linhas.filter((linha) => produtoSumiu(linha.produtoId)).map((linha) => linha.chave)

    // Atualizações funcionais: partem do estado mais recente, sem desfazer o que o usuário mudou durante a recarga.
    definirClienteId((atual) => (atual !== '' && !clientesValidos.has(atual) ? '' : atual))
    definirLinhas((atuais) =>
      atuais.map((linha) => (produtoSumiu(linha.produtoId) ? { ...linha, produtoId: '' } : linha)),
    )
    definirErros((atuais) => {
      const linhasComErro = { ...atuais.linhas }
      for (const chave of chavesSemProduto) {
        linhasComErro[chave] = { ...linhasComErro[chave], produto: 'Este produto não existe mais. Escolha outro.' }
      }
      return clienteSumiu
        ? { ...atuais, cliente: 'O cliente selecionado não existe mais. Escolha outro.', linhas: linhasComErro }
        : { ...atuais, linhas: linhasComErro }
    })
  }

  function tratarFalha(erro: unknown, linhasEnviadas: readonly LinhaItem[]): void {
    if (!(erro instanceof ErroApi)) {
      definirAviso({ tipo: 'generico' })
      return
    }
    const problema = erro.problema
    switch (erro.codigo) {
      case 'VALIDACAO': {
        const mapeados = mapearErrosDaApi(problema?.errors ?? {}, linhasEnviadas)
        definirErros(mapeados)
        definirAviso({ tipo: 'validacao', resumo: resumirErros(mapeados, linhasEnviadas) })
        return
      }
      case 'CLIENTE_NAO_ENCONTRADO':
      case 'PRODUTO_NAO_ENCONTRADO':
        definirAviso({
          tipo: 'nao-encontrado',
          texto: problema?.detail ?? 'O cliente ou algum produto do pedido não existe mais.',
        })
        void recarregarCatalogo(true)
        return
      case 'ESTOQUE_INSUFICIENTE': {
        const itens = problema?.itensSemEstoque ?? []
        definirAviso({ tipo: 'estoque', itens })
        definirErros((atuais) => {
          const linhasComErro = { ...atuais.linhas }
          for (const item of itens) {
            const linha = linhasEnviadas.find((candidata) => candidata.produtoId === String(item.produtoId))
            if (linha !== undefined) {
              linhasComErro[linha.chave] = {
                ...linhasComErro[linha.chave],
                quantidade: `Disponível agora: ${item.estoqueDisponivel.toLocaleString('pt-BR')}.`,
              }
            }
          }
          return { ...atuais, linhas: linhasComErro }
        })
        void recarregarCatalogo(false)
        return
      }
      case 'CONFLITO_CONCORRENCIA':
        definirAviso({ tipo: 'conflito' })
        return
      default: {
        const traceId = problema?.traceId
        definirAviso(traceId === undefined ? { tipo: 'generico' } : { tipo: 'generico', traceId })
      }
    }
  }

  async function enviar(): Promise<void> {
    // A ref barra um segundo envio disparado antes da re-renderização que desabilita o botão (dois cliques na mesma
    // tarefa, como em scripts e testes automatizados); o estado `criando` só muda depois dela.
    if (refEnviando.current || criando || estadoCatalogo !== 'pronto') {
      return
    }
    const resultado = validarFormulario(clienteId, linhas, produtosPorId)
    if (!resultado.valido) {
      definirErros(resultado.erros)
      definirAviso({ tipo: 'validacao', resumo: resumirErros(resultado.erros, linhas) })
      return
    }
    const linhasEnviadas = linhas
    refEnviando.current = true
    definirCriando(true)
    definirErros(SEM_ERROS)
    definirAviso(null)
    let pedido: Pedido
    try {
      pedido = await criarPedido(resultado.requisicao)
    } catch (erro) {
      refEnviando.current = false
      refDevolverFoco.current = true
      definirCriando(false)
      tratarFalha(erro, linhasEnviadas)
      return
    }
    refEnviando.current = false
    // close() do <dialog> devolve o foco a quem abriu o modal (botão "Novo pedido"); só então a lista é avisada.
    refDialogo.current?.close()
    aoCriar(pedido)
  }

  function aoFecharDialogo(): void {
    if (refEnviando.current) {
      refDialogo.current?.showModal()
      return
    }
    aoFechar()
  }

  const totalCentavos = totalEstimadoCentavos(linhas, produtosPorId)
  const catalogoPronto = estadoCatalogo === 'pronto'

  return (
    <dialog
      ref={refDialogo}
      className="modal"
      aria-labelledby="modal-titulo"
      onClose={aoFecharDialogo}
      onCancel={(evento) => {
        if (refEnviando.current || criando) {
          evento.preventDefault()
        }
      }}
    >
      <form
        className="modal__formulario"
        noValidate
        onSubmit={(evento) => {
          evento.preventDefault()
          void enviar()
        }}
      >
        <header className="modal__cabecalho">
          <span className="modal__simbolo" aria-hidden="true">
            <ShoppingCart size={24} />
          </span>
          <div className="modal__titulos">
            <h2 id="modal-titulo">Novo pedido</h2>
            <p className="modal__subtitulo">Escolha o cliente, adicione os produtos e confira o total estimado.</p>
          </div>
          <button type="button" className="botao-icone" aria-label="Fechar" disabled={criando} onClick={fechar}>
            <X />
          </button>
        </header>

        <div className="modal__corpo">
          {estadoCatalogo === 'carregando' && (
            <p className="estado-linha" role="status">
              <LoaderCircle className="girando" />
              Carregando clientes e produtos…
            </p>
          )}
          {estadoCatalogo === 'erro' && (
            <CaixaDeAviso tom="erro">
              <p className="aviso__titulo">Não foi possível carregar clientes e produtos.</p>
              <div>
                <button type="button" className="botao botao--secundario" onClick={tentarCarregarDeNovo}>
                  <RefreshCw />
                  Tentar novamente
                </button>
              </div>
            </CaixaDeAviso>
          )}
          {aviso !== null && <AvisoDoFormulario aviso={aviso} />}

          <div className="secao campo">
            <label htmlFor="pedido-cliente" className="secao__titulo">
              <span className="secao__icone">
                <Users size={16} />
              </span>
              Cliente
            </label>
            <Controle selecao>
              <select
                id="pedido-cliente"
                ref={refCliente}
                value={clienteId}
                required
                disabled={criando}
                aria-invalid={erros.cliente !== undefined}
                aria-describedby={erros.cliente === undefined ? undefined : 'pedido-cliente-erro'}
                onChange={(evento) => {
                  definirClienteId(evento.target.value)
                  definirErros(limparErroDoCliente)
                }}
              >
                <option value="">Selecione um cliente</option>
                {clientes.map((cliente) => (
                  <option key={cliente.id} value={String(cliente.id)}>
                    {`${cliente.nome} (${cliente.email})`}
                  </option>
                ))}
              </select>
            </Controle>
            {erros.cliente !== undefined && (
              <p id="pedido-cliente-erro" className="campo__erro" role="alert">
                <CircleAlert size={16} />
                {erros.cliente}
              </p>
            )}
          </div>

          <fieldset className="secao itens" disabled={criando}>
            <legend className="secao__titulo">
              <span className="secao__icone">
                <Package size={16} />
              </span>
              Itens
            </legend>
            {erros.itens !== undefined && (
              <p className="campo__erro" role="alert">
                <CircleAlert size={16} />
                {erros.itens}
              </p>
            )}
            <ol className="itens__lista">
              {linhas.map((linha, indice) => {
                const numero = indice + 1
                const produto = linha.produtoId === '' ? undefined : produtosPorId.get(Number(linha.produtoId))
                const errosDaLinha = erros.linhas[linha.chave]
                const subtotal = subtotalEstimadoCentavos(produto, linha.quantidade)
                const idProduto = `item-${linha.chave}-produto`
                const idQuantidade = `item-${linha.chave}-quantidade`
                return (
                  <li key={linha.chave} className="item">
                    <span className="item__numero" aria-hidden="true">
                      {numero}
                    </span>
                    <div className="campo item__produto">
                      <label htmlFor={idProduto}>
                        Produto <span className="visualmente-oculto">do item {numero}</span>
                      </label>
                      <Controle selecao>
                        <select
                          id={idProduto}
                          value={linha.produtoId}
                          required
                          aria-invalid={errosDaLinha?.produto !== undefined}
                          aria-describedby={errosDaLinha?.produto === undefined ? undefined : `${idProduto}-erro`}
                          onChange={(evento) => {
                            alterarLinha(linha.chave, 'produtoId', evento.target.value)
                          }}
                        >
                          <option value="">Selecione um produto</option>
                          {produtos.map((opcao) => {
                            const emOutraLinha = linhas.some(
                              (outra) => outra.chave !== linha.chave && outra.produtoId === String(opcao.id),
                            )
                            return (
                              <option
                                key={opcao.id}
                                value={String(opcao.id)}
                                disabled={opcao.estoque === 0 || emOutraLinha}
                              >
                                {`${opcao.nome} — ${formatarMoeda(opcao.preco)} — estoque ${opcao.estoque.toLocaleString('pt-BR')}`}
                              </option>
                            )
                          })}
                        </select>
                      </Controle>
                    </div>

                    <div className="campo item__quantidade">
                      <label htmlFor={idQuantidade}>
                        Quantidade <span className="visualmente-oculto">do item {numero}</span>
                      </label>
                      <input
                        id={idQuantidade}
                        type="number"
                        inputMode="numeric"
                        min={1}
                        max={produto !== undefined && produto.estoque > 0 ? quantidadeMaxima(produto) : undefined}
                        step={1}
                        required
                        value={linha.quantidade}
                        aria-invalid={errosDaLinha?.quantidade !== undefined}
                        aria-describedby={
                          errosDaLinha?.quantidade === undefined ? undefined : `${idQuantidade}-erro`
                        }
                        onChange={(evento) => {
                          alterarLinha(linha.chave, 'quantidade', evento.target.value)
                        }}
                      />
                    </div>

                    <div className="item__subtotal">
                      <span className="item__rotulo">Subtotal estimado</span>
                      <span className="item__valor">{subtotal === undefined ? '—' : formatarCentavos(subtotal)}</span>
                    </div>

                    <button
                      type="button"
                      className="botao-icone botao-icone--perigo item__remover"
                      aria-label={`Remover item ${numero}`}
                      title={`Remover item ${numero}`}
                      disabled={linhas.length === 1}
                      onClick={() => {
                        removerLinha(linha.chave)
                      }}
                    >
                      <Trash />
                    </button>

                    {/* Erros da linha numa faixa abaixo dos campos, com a largura da linha toda. */}
                    {(errosDaLinha?.produto !== undefined || errosDaLinha?.quantidade !== undefined) && (
                      <div className="item__erros">
                        {errosDaLinha.produto !== undefined && (
                          <p id={`${idProduto}-erro`} className="campo__erro" role="alert">
                            <CircleAlert size={16} />
                            <span>
                              <strong>Produto:</strong> {errosDaLinha.produto}
                            </span>
                          </p>
                        )}
                        {errosDaLinha.quantidade !== undefined && (
                          <p id={`${idQuantidade}-erro`} className="campo__erro" role="alert">
                            <CircleAlert size={16} />
                            <span>
                              <strong>Quantidade:</strong> {errosDaLinha.quantidade}
                            </span>
                          </p>
                        )}
                      </div>
                    )}
                  </li>
                )
              })}
            </ol>
            <div className="itens__rodape">
              <button
                type="button"
                className="botao botao--tracejado"
                disabled={linhas.length >= MAXIMO_DE_ITENS}
                onClick={adicionarLinha}
              >
                <Plus />
                Adicionar item
              </button>
              <span className="itens__contador">
                {pluralizar(linhas.length, 'item', 'itens')} de {MAXIMO_DE_ITENS}
              </span>
            </div>
          </fieldset>
        </div>

        {/* Rodapé fixo, fora da área que rola: o total fica sempre visível e nunca cobre um campo. */}
        <footer className="modal__rodape">
          <div className="total">
            <div className="total__texto">
              <span className="total__rotulo">Total estimado</span>
              <p className="total__nota">
                Estimativa com os preços atuais. O valor oficial é calculado pela API ao criar o pedido.
              </p>
            </div>
            <strong className="total__valor">{formatarCentavos(totalCentavos)}</strong>
          </div>
          <div className="modal__acoes">
            <button type="button" className="botao botao--secundario" disabled={criando} onClick={fechar}>
              Cancelar
            </button>
            <button
              type="submit"
              ref={refBotaoEnviar}
              className="botao botao--destaque modal__enviar"
              disabled={criando || !catalogoPronto}
              aria-busy={criando}
            >
              {criando ? <LoaderCircle className="girando" /> : <Check />}
              {criando ? 'Criando…' : 'Criar pedido'}
            </button>
          </div>
        </footer>
      </form>
    </dialog>
  )
}

/** Caixa de aviso do modal: ícone decorativo e conteúdo; `role="alert"` para ser anunciada ao aparecer. */
function CaixaDeAviso({ tom, children }: { tom: 'erro' | 'alerta'; children: ReactNode }) {
  const Icone = tom === 'erro' ? CircleAlert : TriangleAlert
  return (
    <div className={`aviso aviso--${tom}`} role="alert">
      <Icone className="aviso__icone" />
      <div className="aviso__conteudo">{children}</div>
    </div>
  )
}

function AvisoDoFormulario({ aviso }: { aviso: Aviso }) {
  switch (aviso.tipo) {
    case 'validacao':
      return (
        <CaixaDeAviso tom="erro">
          <p className="aviso__titulo">Não foi possível criar o pedido. Corrija os campos indicados:</p>
          <ul>
            {aviso.resumo.map((mensagem) => (
              <li key={mensagem}>{mensagem}</li>
            ))}
          </ul>
        </CaixaDeAviso>
      )
    case 'nao-encontrado':
      return (
        <CaixaDeAviso tom="erro">
          <p className="aviso__titulo">{aviso.texto}</p>
          <p>As listas de clientes e produtos foram atualizadas. Revise o pedido e tente de novo.</p>
        </CaixaDeAviso>
      )
    case 'estoque':
      return (
        <CaixaDeAviso tom="erro">
          <p className="aviso__titulo">Estoque insuficiente. Os estoques foram atualizados:</p>
          <ul>
            {aviso.itens.map((item) => (
              <li key={item.produtoId}>
                {`${item.nomeProduto}: solicitado ${item.quantidadeSolicitada.toLocaleString('pt-BR')}, disponível ${item.estoqueDisponivel.toLocaleString('pt-BR')}`}
              </li>
            ))}
          </ul>
        </CaixaDeAviso>
      )
    case 'conflito':
      return (
        <CaixaDeAviso tom="alerta">
          <p className="aviso__titulo">Conflito momentâneo. Tente novamente.</p>
        </CaixaDeAviso>
      )
    case 'generico':
      return (
        <CaixaDeAviso tom="erro">
          <p className="aviso__titulo">Não foi possível criar o pedido agora.</p>
          <p>Seus dados foram mantidos. Tente novamente em instantes.</p>
          {aviso.traceId !== undefined && <p className="aviso__rastreio">Código para suporte: {aviso.traceId}</p>}
        </CaixaDeAviso>
      )
  }
}
