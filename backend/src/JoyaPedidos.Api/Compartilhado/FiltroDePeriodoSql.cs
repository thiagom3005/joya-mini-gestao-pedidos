using System.Data;
using Dapper;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Predicado de período sobre <c>p.DataPedido</c> (alias <c>p</c> de <c>dbo.Pedidos</c>), comum à listagem e ao ranking
/// (spec §8.2, §8.4, §9.4): intervalo UTC semiaberto, só com os limites presentes. Os fragmentos são constantes e os
/// instantes vão como parâmetros <c>datetime2</c>; um limite nulo (ex.: <c>dataFim</c> = 31/12/9999) não restringe.
/// </summary>
public static class FiltroDePeriodoSql
{
    public static void Adicionar(IntervaloUtc intervalo, ICollection<string> condicoes, DynamicParameters parametros)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        ArgumentNullException.ThrowIfNull(parametros);
        if (intervalo.InicioUtc is { } inicioUtc)
        {
            condicoes.Add("p.DataPedido >= @InicioUtc");
            parametros.Add("InicioUtc", inicioUtc, DbType.DateTime2);
        }
        if (intervalo.FimExclusivoUtc is { } fimExclusivoUtc)
        {
            condicoes.Add("p.DataPedido < @FimExclusivoUtc");
            parametros.Add("FimExclusivoUtc", fimExclusivoUtc, DbType.DateTime2);
        }
    }

    /// <summary><c>WHERE c1 AND c2 ...</c>, ou vazio sem condições.</summary>
    public static string Where(IReadOnlyCollection<string> condicoes) =>
        condicoes.Count == 0 ? string.Empty : "WHERE " + string.Join(" AND ", condicoes);
}
