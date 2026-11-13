import type { CriarPedidoRequisicao, ProdutoCatalogo } from '../tipos/contratos.ts'
import { paraCentavos } from './formatacao.ts'

// Regras do formulário "Novo pedido" (spec §6.3). Funções puras: a validação local melhora a experiência, mas a API
// continua sendo a autoridade (spec §6.3, item 5).

/** Limites do contrato (spec §5.3, DEC-15). */
export const MAXIMO_DE_ITENS = 50
export const QUANTIDADE_MAXIMA = 10_000

/** Uma linha do formulário. `chave` é estável (identifica a linha no React); os valores são o texto dos campos. */
export interface LinhaItem {
  chave: number
  produtoId: string
  quantidade: string
}

export interface ErrosLinha {
  produto?: string
  quantidade?: string
}

export interface ErrosFormulario {
  cliente?: string
  itens?: string
  /** Erros por linha, indexados pela `chave` da linha. */
  linhas: Readonly<Record<number, ErrosLinha>>
}

export const SEM_ERROS: ErrosFormulario = { linhas: {} }

export type ResultadoValidacao =
  | { valido: true; requisicao: CriarPedidoRequisicao }
  | { valido: false; erros: ErrosFormulario }

/** Quantidade máxima aceita para o produto: min(estoque, 10.000) (spec §6.3, item 3). */
export function quantidadeMaxima(produto: ProdutoCatalogo | undefined): number {
  return produto === undefined ? QUANTIDADE_MAXIMA : Math.min(produto.estoque, QUANTIDADE_MAXIMA)
}

/** Inteiro não negativo escrito só com dígitos; qualquer outra coisa (vazio, "1.5", "-2", "1e3") é inválida. */
export function lerQuantidade(texto: string): number | undefined {
  const limpo = texto.trim()
  return /^\d{1,9}$/.test(limpo) ? Number(limpo) : undefined
}

/** Subtotal estimado em centavos inteiros: Math.round(preco * 100) * quantidade (spec §6.3, item 4). */
export function subtotalEstimadoCentavos(produto: ProdutoCatalogo | undefined, quantidade: string): number | undefined {
  const valor = lerQuantidade(quantidade)
  if (produto === undefined || valor === undefined || valor < 1) {
    return undefined
  }
  return paraCentavos(produto.preco) * valor
}

export function totalEstimadoCentavos(
  linhas: readonly LinhaItem[],
  produtosPorId: ReadonlyMap<number, ProdutoCatalogo>,
): number {
  return linhas.reduce(
    (total, linha) => total + (subtotalEstimadoCentavos(produtosPorId.get(Number(linha.produtoId)), linha.quantidade) ?? 0),
    0,
  )
}

export function validarFormulario(
  clienteId: string,
  linhas: readonly LinhaItem[],
  produtosPorId: ReadonlyMap<number, ProdutoCatalogo>,
): ResultadoValidacao {
  const erros: { cliente?: string; itens?: string; linhas: Record<number, ErrosLinha> } = { linhas: {} }

  if (clienteId === '') {
    erros.cliente = 'Selecione um cliente.'
  }
  if (linhas.length === 0) {
    erros.itens = 'Adicione ao menos um item.'
  } else if (linhas.length > MAXIMO_DE_ITENS) {
    erros.itens = `O pedido pode ter no máximo ${MAXIMO_DE_ITENS} itens.`
  }

  const produtosEscolhidos = new Set<string>()
  for (const linha of linhas) {
    const errosDaLinha = validarLinha(linha, produtosPorId, produtosEscolhidos)
    if (errosDaLinha.produto !== undefined || errosDaLinha.quantidade !== undefined) {
      erros.linhas[linha.chave] = errosDaLinha
    }
    if (linha.produtoId !== '') {
      produtosEscolhidos.add(linha.produtoId)
    }
  }

  if (erros.cliente !== undefined || erros.itens !== undefined || Object.keys(erros.linhas).length > 0) {
    return { valido: false, erros }
  }
  return {
    valido: true,
    requisicao: {
      clienteId: Number(clienteId),
      itens: linhas.map((linha) => ({ produtoId: Number(linha.produtoId), quantidade: Number(linha.quantidade.trim()) })),
    },
  }
}

function validarLinha(
  linha: LinhaItem,
  produtosPorId: ReadonlyMap<number, ProdutoCatalogo>,
  produtosEscolhidos: ReadonlySet<string>,
): ErrosLinha {
  const erros: ErrosLinha = {}
  const produto = linha.produtoId === '' ? undefined : produtosPorId.get(Number(linha.produtoId))

  if (linha.produtoId === '') {
    erros.produto = 'Selecione um produto.'
  } else if (produto === undefined) {
    erros.produto = 'Este produto não está mais disponível. Escolha outro.'
  } else if (produtosEscolhidos.has(linha.produtoId)) {
    erros.produto = 'Este produto já está em outro item.'
  } else if (produto.estoque === 0) {
    erros.produto = 'Produto sem estoque.'
  }

  const quantidade = lerQuantidade(linha.quantidade)
  if (quantidade === undefined || quantidade < 1) {
    erros.quantidade = 'Informe uma quantidade inteira a partir de 1.'
  } else if (produto !== undefined && produto.estoque > 0 && quantidade > quantidadeMaxima(produto)) {
    erros.quantidade = produto.estoque < QUANTIDADE_MAXIMA
      ? `Máximo de ${produto.estoque.toLocaleString('pt-BR')} (estoque disponível).`
      : `Máximo de ${QUANTIDADE_MAXIMA.toLocaleString('pt-BR')} unidades por item.`
  }
  return erros
}

const CHAVE_DE_ITEM = /^itens\[(\d+)\](?:\.(produtoId|quantidade))?$/

/**
 * Converte `errors` de um 400 VALIDACAO (chaves no caminho JSON, índice a partir de 0, spec §5.3) em erros do
 * formulário: `clienteId` → cliente; `itens` → itens; `itens[i].produtoId` / `itens[i].quantidade` → linha i.
 * Chaves desconhecidas vão para o erro geral dos itens, para nada se perder.
 */
export function mapearErrosDaApi(
  errors: Readonly<Record<string, readonly string[]>>,
  linhas: readonly LinhaItem[],
): ErrosFormulario {
  const resultado: { cliente?: string; itens?: string; linhas: Record<number, ErrosLinha> } = { linhas: {} }
  const gerais: string[] = []

  for (const [chave, mensagens] of Object.entries(errors)) {
    const texto = mensagens.join(' ')
    if (chave === 'clienteId') {
      resultado.cliente = texto
      continue
    }
    if (chave === 'itens') {
      gerais.push(texto)
      continue
    }
    const correspondencia = CHAVE_DE_ITEM.exec(chave)
    const linha = correspondencia === null ? undefined : linhas[Number(correspondencia[1])]
    if (correspondencia === null || linha === undefined) {
      gerais.push(texto)
      continue
    }
    const errosDaLinha: ErrosLinha = { ...resultado.linhas[linha.chave] }
    if (correspondencia[2] === 'quantidade') {
      errosDaLinha.quantidade = texto
    } else {
      errosDaLinha.produto = texto
    }
    resultado.linhas[linha.chave] = errosDaLinha
  }

  if (gerais.length > 0) {
    resultado.itens = gerais.join(' ')
  }
  return resultado
}

/** Remove o erro de um campo de uma linha (quando o usuário altera o campo). */
export function limparErroDaLinha(erros: ErrosFormulario, chave: number, campo: keyof ErrosLinha): ErrosFormulario {
  const atual = erros.linhas[chave]
  if (atual?.[campo] === undefined) {
    return erros
  }
  const restante: ErrosLinha = {}
  if (campo !== 'produto' && atual.produto !== undefined) {
    restante.produto = atual.produto
  }
  if (campo !== 'quantidade' && atual.quantidade !== undefined) {
    restante.quantidade = atual.quantidade
  }
  return { ...erros, linhas: { ...erros.linhas, [chave]: restante } }
}

/** Remove o erro do cliente (quando o usuário escolhe outro). */
export function limparErroDoCliente(erros: ErrosFormulario): ErrosFormulario {
  if (erros.cliente === undefined) {
    return erros
  }
  return erros.itens === undefined ? { linhas: erros.linhas } : { itens: erros.itens, linhas: erros.linhas }
}

/** Lista legível dos erros, para o resumo acessível do topo do formulário. */
export function resumirErros(erros: ErrosFormulario, linhas: readonly LinhaItem[]): string[] {
  const resumo: string[] = []
  if (erros.cliente !== undefined) {
    resumo.push(`Cliente: ${erros.cliente}`)
  }
  if (erros.itens !== undefined) {
    resumo.push(`Itens: ${erros.itens}`)
  }
  linhas.forEach((linha, indice) => {
    const errosDaLinha = erros.linhas[linha.chave]
    if (errosDaLinha?.produto !== undefined) {
      resumo.push(`Item ${indice + 1}, produto: ${errosDaLinha.produto}`)
    }
    if (errosDaLinha?.quantidade !== undefined) {
      resumo.push(`Item ${indice + 1}, quantidade: ${errosDaLinha.quantidade}`)
    }
  })
  return resumo
}
