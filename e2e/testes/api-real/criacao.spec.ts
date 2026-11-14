import { expect, test, type Page, type Response } from '@playwright/test'
import {
  buscarClientes,
  buscarProduto,
  criarPedidoPelaApi,
  formatarMoeda,
  formatarNumero,
  listarPedidos,
  produtosComMaisEstoque,
  type PedidoCriado,
} from '../../apoio/api.ts'
import {
  abrirListagem,
  abrirModalNovoPedido,
  campoCliente,
  campoProduto,
  campoQuantidade,
  celula,
  esperarListagem,
  escolherCliente,
  linhasDaTabela,
  preencherItem,
} from '../../apoio/tela.ts'

const ehCriacao = (resposta: Response): boolean =>
  resposta.request().method() === 'POST' && new URL(resposta.url()).pathname === '/api/pedidos'

const esperarCriacao = (page: Page): Promise<Response> => page.waitForResponse(ehCriacao)

const centavos = (valor: number): number => Math.round(valor * 100)

// API real: o pedido é gravado de verdade no banco do ambiente isolado. Clientes e produtos vêm do seed pela API
// (spec §10.5); os produtos escolhidos são os de estoque alto, e cada execução consome poucas unidades.
test.describe('Criação de pedidos com a API real', () => {
  test('E2E-01: cria um pedido com dois produtos e o mostra no topo da listagem', async ({ page, request }) => {
    const [cliente] = await buscarClientes(request)
    const [produtoA, produtoB] = await produtosComMaisEstoque(request, 2)
    const totalCentavos = centavos(produtoA!.preco) * 2 + centavos(produtoB!.preco) * 1
    const totalFormatado = formatarMoeda(totalCentavos / 100)

    await abrirListagem(page)
    const modal = await abrirModalNovoPedido(page)
    await escolherCliente(modal, cliente!)
    await preencherItem(modal, 1, produtoA!.id, 2)
    await modal.getByRole('button', { name: 'Adicionar item' }).click()
    await preencherItem(modal, 2, produtoB!.id, 1)
    // Total estimado no rodapé do modal (os subtotais dos itens têm outros valores).
    await expect(modal.getByText(totalFormatado, { exact: true })).toBeVisible()

    const criacao = esperarCriacao(page)
    const listagem = esperarListagem(page)
    await modal.getByRole('button', { name: 'Criar pedido' }).click()
    const resposta = await criacao
    expect(resposta.status()).toBe(201)
    const pedido = (await resposta.json()) as PedidoCriado
    await listagem

    await expect(modal).toBeHidden()
    await expect(page.getByText(`Pedido nº ${pedido.id} criado com sucesso.`)).toBeVisible()
    const primeira = linhasDaTabela(page).first()
    await expect(celula(primeira, 'numero')).toHaveText(String(pedido.id))
    await expect(celula(primeira, 'cliente')).toHaveAccessibleName(cliente!.nome)
    await expect(celula(primeira, 'itens')).toHaveText('2')
    await expect(celula(primeira, 'valorTotal')).toHaveText(totalFormatado)
    // O cliente é homônimo de outro no seed: a identidade é conferida pelo id gravado, não só pelo nome.
    expect(pedido.cliente.id).toBe(cliente!.id)
    expect(centavos(pedido.valorTotal)).toBe(totalCentavos)
    expect(pedido.itens.map((item) => [item.produtoId, item.quantidade])).toEqual(
      [
        [produtoA!.id, 2],
        [produtoB!.id, 1],
      ].toSorted((a, b) => a[0]! - b[0]!),
    )
  })

  test('E2E-03: estoque consumido por outro pedido antes do envio mostra o disponível atualizado', async ({
    page,
    request,
  }) => {
    const [clienteDaTela, clienteDaApi] = await buscarClientes(request)
    const [produto] = await produtosComMaisEstoque(request, 1)

    await abrirListagem(page)
    const modal = await abrirModalNovoPedido(page)
    // Sem escrita entre a abertura do modal e esta leitura: é o estoque que o modal carregou.
    const estoqueNaTela = (await buscarProduto(request, produto!.id)).estoque
    await escolherCliente(modal, clienteDaTela!)
    await preencherItem(modal, 1, produto!.id, estoqueNaTela)

    // Outro pedido consome 1 unidade antes do envio (spec §10.5, E2E-03).
    await criarPedidoPelaApi(request, clienteDaApi!.id, [{ produtoId: produto!.id, quantidade: 1 }])
    const disponivel = estoqueNaTela - 1
    const pedidosAntes = (await listarPedidos(request)).totalItens

    const criacao = esperarCriacao(page)
    await modal.getByRole('button', { name: 'Criar pedido' }).click()
    expect((await criacao).status()).toBe(409)

    await expect(modal.getByRole('alert').filter({ hasText: 'Estoque insuficiente' })).toContainText(
      `${produto!.nome}: solicitado ${formatarNumero(estoqueNaTela)}, disponível ${formatarNumero(disponivel)}`,
    )
    await expect(campoQuantidade(modal, 1)).toHaveAccessibleDescription(
      `Quantidade: Disponível agora: ${formatarNumero(disponivel)}.`,
    )
    await expect(modal).toBeVisible()
    await expect(campoCliente(modal)).toHaveValue(String(clienteDaTela!.id))
    // Nada do pedido recusado foi gravado.
    expect((await buscarProduto(request, produto!.id)).estoque).toBe(disponivel)
    expect((await listarPedidos(request)).totalItens).toBe(pedidosAntes)
  })

  // Spec §6.3 e §6.4 (validação antes do envio, erros junto aos campos; CA-17).
  test('valida campos inválidos no navegador e só o envio válido chega à API', async ({ page, request }) => {
    const envios: string[] = []
    page.on('request', (requisicao) => {
      if (requisicao.method() === 'POST' && new URL(requisicao.url()).pathname === '/api/pedidos') {
        envios.push(requisicao.url())
      }
    })
    const [cliente] = await buscarClientes(request)
    const [produto] = await produtosComMaisEstoque(request, 1)

    await abrirListagem(page)
    const modal = await abrirModalNovoPedido(page)
    const enviar = modal.getByRole('button', { name: 'Criar pedido' })

    await enviar.click()
    await expect(modal.getByRole('alert').filter({ hasText: 'Corrija os campos indicados' })).toBeVisible()
    await expect(campoCliente(modal)).toHaveAttribute('aria-invalid', 'true')
    await expect(campoCliente(modal)).toHaveAccessibleDescription('Selecione um cliente.')
    await expect(campoProduto(modal, 1)).toHaveAccessibleDescription('Produto: Selecione um produto.')

    await escolherCliente(modal, cliente!)
    await preencherItem(modal, 1, produto!.id, 0)
    await enviar.click()
    await expect(campoQuantidade(modal, 1)).toHaveAccessibleDescription(
      'Quantidade: Informe uma quantidade inteira a partir de 1.',
    )

    await campoQuantidade(modal, 1).fill('1')
    const criacao = esperarCriacao(page)
    await enviar.click()
    expect((await criacao).status()).toBe(201)
    await expect(modal).toBeHidden()
    // Os dois envios inválidos foram barrados no navegador: só o válido virou requisição.
    expect(envios).toHaveLength(1)
  })
})
