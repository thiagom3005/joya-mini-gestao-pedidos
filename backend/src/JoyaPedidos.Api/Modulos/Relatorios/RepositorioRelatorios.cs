using Dapper;
using JoyaPedidos.Api.Compartilhado;
using JoyaPedidos.Api.Modulos.Relatorios.Contratos;

namespace JoyaPedidos.Api.Modulos.Relatorios;

/// <summary>Consultas de relatório (SQL explícito, Dapper, parâmetros <c>datetime2</c> via <c>sp_executesql</c>).</summary>
public sealed class RepositorioRelatorios(FabricaConexao fabricaConexao)
{
    public const int TamanhoDoRanking = 5;

    /// <summary>
    /// Os 5 clientes que mais compraram no intervalo (spec §9.4, RN-14). Valor = soma de <c>Quantidade × PrecoUnitario</c>
    /// dos itens (preço congelado), agrupado por <c>c.Id</c> (homônimos ficam separados), só clientes com compra no
    /// período (<c>INNER JOIN</c>), período em intervalo semiaberto sobre a coluna sem função (sem <c>YEAR</c>).
    /// Ordem total <c>ValorTotal DESC, c.Id</c>: empate vai para o menor id. A posição é a ordem devolvida (1 a N).
    /// </summary>
    public async Task<IReadOnlyList<ClienteRankingResposta>> TopClientesAsync(IntervaloUtc intervalo, CancellationToken cancelamento)
    {
        var condicoes = new List<string>();
        var parametros = new DynamicParameters();
        FiltroDePeriodoSql.Adicionar(intervalo, condicoes, parametros);
        var sql = $"""
            SELECT TOP ({TamanhoDoRanking})
                c.Id AS ClienteId,
                c.Nome,
                SUM(i.Quantidade * i.PrecoUnitario) AS ValorTotal
            FROM dbo.Pedidos AS p
            INNER JOIN dbo.Clientes AS c ON c.Id = p.ClienteId
            INNER JOIN dbo.ItensPedido AS i ON i.PedidoId = p.Id
            {FiltroDePeriodoSql.Where(condicoes)}
            GROUP BY c.Id, c.Nome
            ORDER BY ValorTotal DESC, c.Id;
            """;

        await using var conexao = await fabricaConexao.AbrirAsync(cancelamento);
        var linhas = await conexao.QueryAsync<LinhaDoRanking>(new CommandDefinition(sql, parametros, cancellationToken: cancelamento));
        return linhas
            .Select((linha, indice) => new ClienteRankingResposta(indice + 1, linha.ClienteId, linha.Nome, linha.ValorTotal))
            .ToList();
    }

    private sealed record LinhaDoRanking(int ClienteId, string Nome, decimal ValorTotal);
}
