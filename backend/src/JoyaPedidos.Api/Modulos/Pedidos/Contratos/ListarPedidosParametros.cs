namespace JoyaPedidos.Api.Modulos.Pedidos.Contratos;

/// <summary>
/// Query string de <c>GET /pedidos</c> (spec §5.4), como chegou. Inteiros não numéricos falham no binding do framework
/// (400 <c>REQUISICAO_INVALIDA</c>); datas chegam como texto para o parsing estrito do validador.
/// </summary>
public sealed record ListarPedidosParametros(
    int? ClienteId, string? DataInicio, string? DataFim, int? Pagina, int? TamanhoPagina);

/// <summary>Filtro validado, com os padrões de página aplicados.</summary>
public sealed record FiltroListarPedidos(
    int? ClienteId, DateOnly? DataInicio, DateOnly? DataFim, int Pagina, int TamanhoPagina);
