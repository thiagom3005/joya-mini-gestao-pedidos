import { expect, test } from '@playwright/test'
import { clientesHomonimos, criarPedidoPelaApi, dataEmSaoPaulo, listarPedidos, produtosComMaisEstoque } from '../../apoio/api.ts'
import {
  abrirListagem,
  comecandoPelosNumeros,
  esperarListagem,
  linhasDaTabela,
  paginacao,
  rotuloDoCliente,
  textoDaPaginacao,
} from '../../apoio/tela.ts'

// API real: navegador → nginx → API → SQL Server do ambiente isolado. Nenhuma requisição é interceptada; a tela é
// comparada com o que a própria API devolve no mesmo momento, sem depender de ids ou totais fixos.
test.describe('Listagem com a API real', () => {
  // Spec §6.2, itens 3 e 4 (tabela e paginação com os dados da API; CA-16).
  test('abre a aplicação com os pedidos carregados do backend', async ({ page, request }) => {
    await abrirListagem(page)
    const esperado = await listarPedidos(request)

    expect(esperado.itens.length).toBeGreaterThan(0)
    await expect(paginacao(page)).toContainText(textoDaPaginacao(1, esperado.totalPaginas, esperado.totalItens))
    await expect(linhasDaTabela(page)).toHaveText(comecandoPelosNumeros(esperado.itens.map((pedido) => pedido.id)))
  })

  // Spec §6.2, item 4 (Anterior e Próxima, limites desabilitados; CA-16).
  test('navega para a próxima página e volta', async ({ page, request }) => {
    await abrirListagem(page)
    const primeira = await listarPedidos(request)
    const segunda = await listarPedidos(request, { pagina: 2 })
    expect(primeira.totalPaginas, 'o seed tem mais de 10 pedidos').toBeGreaterThanOrEqual(2)
    const anterior = paginacao(page).getByRole('button', { name: 'Anterior' })
    const proxima = paginacao(page).getByRole('button', { name: 'Próxima' })
    await expect(anterior).toBeDisabled()

    const paraSegunda = esperarListagem(page)
    await proxima.click()
    expect(new URL((await paraSegunda).url()).searchParams.get('pagina')).toBe('2')
    await expect(paginacao(page)).toContainText(textoDaPaginacao(2, segunda.totalPaginas, segunda.totalItens))
    await expect(linhasDaTabela(page)).toHaveText(comecandoPelosNumeros(segunda.itens.map((pedido) => pedido.id)))
    await expect(anterior).toBeEnabled()

    const paraPrimeira = esperarListagem(page)
    await anterior.click()
    expect(new URL((await paraPrimeira).url()).searchParams.get('pagina')).toBe('1')
    await expect(linhasDaTabela(page)).toHaveText(comecandoPelosNumeros(primeira.itens.map((pedido) => pedido.id)))
  })

  test('E2E-02: filtra por cliente e por período, e a paginação reflete o total', async ({ page, request }) => {
    // Arranjo: um pedido do cliente e outro do homônimo dele, no mesmo dia, criados agora pela API.
    const [cliente, homonimo] = await clientesHomonimos(request)
    const [produto] = await produtosComMaisEstoque(request, 1)
    const doCliente = await criarPedidoPelaApi(request, cliente.id, [{ produtoId: produto!.id, quantidade: 1 }])
    const doHomonimo = await criarPedidoPelaApi(request, homonimo.id, [{ produtoId: produto!.id, quantidade: 1 }])
    const dia = dataEmSaoPaulo(doCliente.dataPedido)
    const esperado = await listarPedidos(request, { clienteId: cliente.id, dataInicio: dia, dataFim: dia })
    // Só pedidos compatíveis: o do cliente entra; o do homônimo, mesmo nome e mesmo dia, não.
    expect(esperado.itens.map((pedido) => pedido.id)).toContain(doCliente.id)
    expect(esperado.itens.map((pedido) => pedido.id)).not.toContain(doHomonimo.id)
    for (const pedido of esperado.itens) {
      expect(pedido.cliente.id).toBe(cliente.id)
      expect(dataEmSaoPaulo(pedido.dataPedido)).toBe(dia)
    }

    await abrirListagem(page)
    const filtros = page.getByRole('form', { name: 'Filtros de pedidos' })
    const campoCliente = filtros.getByLabel('Cliente', { exact: true })
    await campoCliente.selectOption(String(cliente.id))
    // Homônimos se distinguem pelo e-mail (spec §6.2).
    await expect(campoCliente.locator('option:checked')).toHaveText(rotuloDoCliente(cliente))
    await filtros.getByLabel('Data inicial').fill(dia)
    await filtros.getByLabel('Data final').fill(dia)
    const filtrada = esperarListagem(page)
    await filtros.getByRole('button', { name: 'Filtrar' }).click()

    const parametros = new URL((await filtrada).url()).searchParams
    expect([parametros.get('clienteId'), parametros.get('dataInicio'), parametros.get('dataFim')]).toEqual([
      String(cliente.id),
      dia,
      dia,
    ])
    await expect(paginacao(page)).toContainText(textoDaPaginacao(1, esperado.totalPaginas, esperado.totalItens))
    await expect(linhasDaTabela(page)).toHaveText(comecandoPelosNumeros(esperado.itens.map((pedido) => pedido.id)))
  })

  // Spec §6.2, item 2 (data inicial maior que a final é erro local, sem consulta; CA-16).
  test('período invertido mostra o erro sem consultar a API', async ({ page }) => {
    await abrirListagem(page)
    const consultas: string[] = []
    page.on('request', (requisicao) => {
      if (requisicao.method() === 'GET' && new URL(requisicao.url()).pathname === '/api/pedidos') {
        consultas.push(requisicao.url())
      }
    })
    const filtros = page.getByRole('form', { name: 'Filtros de pedidos' })
    const dataInicial = filtros.getByLabel('Data inicial')
    await filtros.getByLabel('Data final').fill('2026-01-01')
    await dataInicial.fill('2026-12-31')
    await filtros.getByRole('button', { name: 'Filtrar' }).click()

    await expect(filtros.getByRole('alert')).toHaveText('A data inicial deve ser anterior ou igual à data final.')
    await expect(dataInicial).toHaveAttribute('aria-invalid', 'true')
    await expect(dataInicial).toHaveAccessibleDescription('A data inicial deve ser anterior ou igual à data final.')

    // "Limpar" sempre consulta: quando a resposta dela chega, qualquer consulta anterior já teria sido registrada.
    const limpeza = esperarListagem(page)
    await filtros.getByRole('button', { name: 'Limpar' }).click()
    await limpeza
    expect(consultas, 'só a consulta do "Limpar"').toHaveLength(1)
    await expect(filtros.getByRole('alert')).toHaveCount(0)
  })
})
