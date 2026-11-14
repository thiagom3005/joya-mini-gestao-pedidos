import { expect, type Locator, type Page, type Response } from '@playwright/test'
import type { Cliente } from './api.ts'

/** Espera a próxima resposta da listagem (`GET /api/pedidos?...`); crie a promessa antes da ação que a dispara. */
export function esperarListagem(page: Page): Promise<Response> {
  return page.waitForResponse(
    (resposta) => resposta.request().method() === 'GET' && new URL(resposta.url()).pathname === '/api/pedidos',
  )
}

/** Abre a página e espera a primeira consulta da listagem terminar com 200. */
export async function abrirListagem(page: Page): Promise<void> {
  const listagem = esperarListagem(page)
  await page.goto('/')
  expect((await listagem).status()).toBe(200)
  await expect(page.getByRole('heading', { name: 'Gestão de Pedidos' })).toBeVisible()
  await expect(paginacao(page)).toBeVisible()
}

export const paginacao = (page: Page): Locator => page.getByRole('navigation', { name: 'Paginação' })

/** Linhas de dados da tabela "Pedidos": as que têm células (a do cabeçalho só tem cabeçalhos de coluna). */
export const linhasDaTabela = (page: Page): Locator =>
  page.getByRole('table', { name: 'Pedidos' }).getByRole('row').filter({ has: page.getByRole('cell') })

/** Colunas da tabela, na ordem da spec §6.2. */
const COLUNAS = { numero: 0, cliente: 1, data: 2, itens: 3, valorTotal: 4 } as const

export const celula = (linha: Locator, coluna: keyof typeof COLUNAS): Locator =>
  linha.getByRole('cell').nth(COLUNAS[coluna])

/**
 * Padrões para `toHaveText` sobre `linhasDaTabela`: cada linha começa pelo número do pedido (coluna "Nº"). Compara a
 * lista inteira, na ordem, e espera a tela estabilizar.
 */
export const comecandoPelosNumeros = (numeros: readonly number[]): RegExp[] =>
  numeros.map((numero) => new RegExp(`^${numero}(?!\\d)`))

/** Texto do rodapé da paginação, como a spec §6.2 descreve: "Página X de Y · N pedidos". */
export function textoDaPaginacao(pagina: number, totalPaginas: number, totalItens: number): string {
  const pedidos = `${totalItens.toLocaleString('pt-BR')} ${totalItens === 1 ? 'pedido' : 'pedidos'}`
  return `Página ${pagina} de ${Math.max(totalPaginas, 1)} · ${pedidos}`
}

export const rotuloDoCliente = (cliente: Cliente): string => `${cliente.nome} (${cliente.email})`

/** Abre o modal "Novo pedido" e espera clientes e produtos carregarem da API. */
export async function abrirModalNovoPedido(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: 'Novo pedido' }).click()
  const modal = page.getByRole('dialog', { name: 'Novo pedido' })
  await expect(modal).toBeVisible()
  await expect(modal.getByRole('status').filter({ hasText: 'Carregando clientes e produtos' })).toHaveCount(0)
  await expect(modal.getByRole('button', { name: 'Criar pedido' })).toBeEnabled()
  return modal
}

export const campoCliente = (modal: Locator): Locator => modal.getByLabel('Cliente', { exact: true })

export const campoProduto = (modal: Locator, numero: number): Locator =>
  modal.getByLabel(`Produto do item ${numero}`, { exact: true })

export const campoQuantidade = (modal: Locator, numero: number): Locator =>
  modal.getByLabel(`Quantidade do item ${numero}`, { exact: true })

export async function escolherCliente(modal: Locator, cliente: Cliente): Promise<void> {
  await campoCliente(modal).selectOption(String(cliente.id))
}

/** Preenche o item `numero` (1, 2, ...). A linha precisa existir: use "Adicionar item" antes do segundo. */
export async function preencherItem(modal: Locator, numero: number, produtoId: number, quantidade: number): Promise<void> {
  await campoProduto(modal, numero).selectOption(String(produtoId))
  await campoQuantidade(modal, numero).fill(String(quantidade))
}
