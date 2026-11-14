import { expect, test, type Route } from '@playwright/test'
import { buscarClientes, produtosComMaisEstoque, type Cliente, type Produto } from '../../apoio/api.ts'
import { abrirListagem, abrirModalNovoPedido, campoCliente, campoQuantidade, escolherCliente, preencherItem } from '../../apoio/tela.ts'

// SIMULADO: listagem, clientes e produtos vêm da API real, mas o POST /api/pedidos é interceptado com page.route e
// respondido pelo teste. Prova comportamentos do frontend (trava contra envio duplicado, reenvio depois de falha) que
// dependem de segurar ou falhar a resposta. Nenhum pedido é gravado; não é teste de integração com a API.

const ehCaminhoDePedidos = (url: URL): boolean => url.pathname === '/api/pedidos'

const ID_SIMULADO = 987_654

function pedidoSimulado(cliente: Cliente, produto: Produto): string {
  return JSON.stringify({
    id: ID_SIMULADO,
    cliente: { id: cliente.id, nome: cliente.nome },
    dataPedido: new Date().toISOString().replace(/\.\d{3}Z$/, 'Z'),
    itens: [
      { produtoId: produto.id, nomeProduto: produto.nome, quantidade: 1, precoUnitario: produto.preco, subtotal: produto.preco },
    ],
    valorTotal: produto.preco,
  })
}

test.describe('Simulado (page.route): envio do pedido', () => {
  // Spec §6.3, item 6 (botão desabilitado durante o envio) e RSK-14; cenário pendente da E11 para automatizar.
  test('quatro acionamentos seguidos geram um único POST', async ({ page, request }) => {
    const [cliente] = await buscarClientes(request)
    const [produto] = await produtosComMaisEstoque(request, 1)
    const segurados: Route[] = []
    await page.route(ehCaminhoDePedidos, async (rota) => {
      if (rota.request().method() !== 'POST') {
        await rota.fallback() // a listagem (GET) segue para a API real
        return
      }
      segurados.push(rota) // a resposta fica pendente até o teste liberar
    })

    await abrirListagem(page)
    const modal = await abrirModalNovoPedido(page)
    await escolherCliente(modal, cliente!)
    await preencherItem(modal, 1, produto!.id, 1)

    // Três cliques e um requestSubmit na mesma tarefa, antes de o React re-renderizar e desabilitar o botão: o clique do
    // Playwright esperaria o botão habilitado e não reproduziria a rajada (defeito real corrigido na E10).
    await modal.getByRole('button', { name: 'Criar pedido' }).evaluate((botao: HTMLButtonElement) => {
      if (botao.form === null) {
        throw new Error('O botão de envio deveria pertencer ao formulário do modal.')
      }
      botao.click()
      botao.click()
      botao.click()
      botao.form.requestSubmit()
    })

    const criando = modal.getByRole('button', { name: 'Criando…' })
    await expect(criando).toBeDisabled()
    await expect(criando).toHaveAttribute('aria-busy', 'true')
    await expect.poll(() => segurados.length).toBe(1)

    await segurados[0]!.fulfill({ status: 201, contentType: 'application/json', body: pedidoSimulado(cliente!, produto!) })
    await expect(modal).toBeHidden()
    await expect(page.getByText(`Pedido nº ${ID_SIMULADO} criado com sucesso.`)).toBeVisible()
    expect(segurados, 'um único POST para os quatro acionamentos').toHaveLength(1)
  })

  // Spec §6.4 (503 CONFLITO_CONCORRENCIA: dados mantidos, nova tentativa pelo usuário).
  test('depois de uma falha, um novo envio gera um segundo POST com os dados mantidos', async ({ page, request }) => {
    const [cliente] = await buscarClientes(request)
    const [produto] = await produtosComMaisEstoque(request, 1)
    let envios = 0
    await page.route(ehCaminhoDePedidos, async (rota) => {
      if (rota.request().method() !== 'POST') {
        await rota.fallback()
        return
      }
      envios += 1
      if (envios === 1) {
        await rota.fulfill({
          status: 503,
          contentType: 'application/problem+json',
          body: JSON.stringify({
            title: 'Conflito de concorrência',
            status: 503,
            detail: 'Conflito momentâneo de concorrência. Tente novamente.',
            codigo: 'CONFLITO_CONCORRENCIA',
            traceId: '00-simulado-00',
          }),
        })
        return
      }
      await rota.fulfill({ status: 201, contentType: 'application/json', body: pedidoSimulado(cliente!, produto!) })
    })

    await abrirListagem(page)
    const modal = await abrirModalNovoPedido(page)
    await escolherCliente(modal, cliente!)
    await preencherItem(modal, 1, produto!.id, 1)
    const enviar = modal.getByRole('button', { name: 'Criar pedido' })

    await enviar.click()
    await expect(modal.getByRole('alert').filter({ hasText: 'Conflito momentâneo. Tente novamente.' })).toBeVisible()
    await expect(campoCliente(modal)).toHaveValue(String(cliente!.id))
    await expect(campoQuantidade(modal, 1)).toHaveValue('1')
    await expect(enviar).toBeEnabled()
    expect(envios).toBe(1)

    await enviar.click()
    await expect(modal).toBeHidden()
    await expect(page.getByText(`Pedido nº ${ID_SIMULADO} criado com sucesso.`)).toBeVisible()
    expect(envios).toBe(2)
  })
})
