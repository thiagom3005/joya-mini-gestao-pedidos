// Formatação para exibição (spec §6.5). O fuso é explícito: a data mostrada não depende do fuso do navegador e é
// coerente com os filtros, que a API interpreta em America/Sao_Paulo (RN-15).

const formatoMoeda = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

const formatoDataHora = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  dateStyle: 'short',
  timeStyle: 'short',
})

export function formatarMoeda(valor: number): string {
  return formatoMoeda.format(valor)
}

/** Valor em centavos inteiros (usado nas estimativas do modal) formatado em reais. */
export function formatarCentavos(centavos: number): string {
  return formatoMoeda.format(centavos / 100)
}

/** Preço em reais para centavos inteiros: evita somar frações binárias (spec §6.3, item 4). */
export function paraCentavos(preco: number): number {
  return Math.round(preco * 100)
}

/** Instante ISO 8601 em UTC (sempre com "Z", spec §5.1) exibido no horário de São Paulo. */
export function formatarDataHora(instanteUtc: string): string {
  return formatoDataHora.format(new Date(instanteUtc))
}

export function pluralizar(quantidade: number, singular: string, plural: string): string {
  return `${quantidade.toLocaleString('pt-BR')} ${quantidade === 1 ? singular : plural}`
}

/** Iniciais do primeiro e do último nome ("Ana Souza" → "AS"), para o avatar decorativo da tabela. */
export function iniciais(nome: string): string {
  const partes = nome.trim().split(/\s+/)
  const primeira = partes[0]?.charAt(0) ?? ''
  const ultima = partes.length > 1 ? (partes.at(-1)?.charAt(0) ?? '') : ''
  return (primeira + ultima).toLocaleUpperCase('pt-BR')
}
