using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Pedidos.Contratos;

namespace JoyaPedidos.Api.Modulos.Pedidos;

/// <summary>
/// Validação de <c>GET /pedidos</c> (spec §5.4, §8.1; RN-15). Função pura: devolve os erros por campo ou, sem erros, o
/// filtro com os padrões aplicados (página 1, tamanho 10). Valores fora da faixa são rejeitados, nunca ajustados.
/// </summary>
public static class ValidadorListarPedidos
{
    public const int PaginaPadrao = 1;
    public const int TamanhoPaginaPadrao = 10;
    public const int TamanhoPaginaMaximo = 100;

    public static (IReadOnlyDictionary<string, string[]> Erros, FiltroListarPedidos? Filtro) Validar(ListarPedidosParametros parametros)
    {
        ArgumentNullException.ThrowIfNull(parametros);
        var erros = new Dictionary<string, string[]>();

        if (parametros.ClienteId <= 0)
        {
            erros["clienteId"] = ["O cliente deve ser um número inteiro positivo."];
        }
        var (dataInicio, dataFim) = ValidacaoDePeriodo.Ler(parametros.DataInicio, parametros.DataFim, obrigatorio: false, erros);
        var pagina = parametros.Pagina ?? PaginaPadrao;
        if (pagina < 1)
        {
            erros["pagina"] = ["A página deve ser maior ou igual a 1."];
        }
        var tamanhoPagina = parametros.TamanhoPagina ?? TamanhoPaginaPadrao;
        if (tamanhoPagina is < 1 or > TamanhoPaginaMaximo)
        {
            erros["tamanhoPagina"] = [$"O tamanho da página deve estar entre 1 e {TamanhoPaginaMaximo}."];
        }

        return erros.Count > 0
            ? (erros, null)
            : (erros, new FiltroListarPedidos(parametros.ClienteId, dataInicio, dataFim, pagina, tamanhoPagina));
    }
}
