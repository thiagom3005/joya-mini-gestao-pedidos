namespace JoyaPedidos.Api.Modulos.Pedidos.Contratos;

/// <summary>Envelope de <c>GET /pedidos</c> (spec §5.4). Com <c>totalItens = 0</c>, <c>totalPaginas = 0</c>.</summary>
public sealed record ListaPedidosResposta(
    IReadOnlyList<PedidoResumoResposta> Itens, int Pagina, int TamanhoPagina, int TotalItens, int TotalPaginas)
{
    public static ListaPedidosResposta De(IReadOnlyList<PedidoResumoResposta> itens, int pagina, int tamanhoPagina, int totalItens) =>
        new(itens, pagina, tamanhoPagina, totalItens, TotalDePaginas(totalItens, tamanhoPagina));

    // ceil(totalItens / tamanhoPagina) em aritmética inteira, sem estouro perto de int.MaxValue.
    private static int TotalDePaginas(int totalItens, int tamanhoPagina) =>
        totalItens / tamanhoPagina + (totalItens % tamanhoPagina == 0 ? 0 : 1);
}

/// <summary>
/// Linha da listagem. <c>QuantidadeItens</c> conta linhas de item (produtos distintos), não unidades; <c>ValorTotal</c>
/// é derivado dos itens com o preço congelado (RN-08, RN-11).
/// </summary>
public sealed record PedidoResumoResposta(
    int Id, ClienteResposta Cliente, DateTime DataPedido, int QuantidadeItens, decimal ValorTotal);
