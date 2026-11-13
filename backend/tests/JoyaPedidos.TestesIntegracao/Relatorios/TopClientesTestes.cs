using System.Net;
using System.Text.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;

namespace JoyaPedidos.TestesIntegracao.Relatorios;

/// <summary>
/// <c>GET /relatorios/top-clientes</c> (spec §5.6, §9.4–9.5): IT-18, IT-19 e IT-20. Dados históricos arranjados via SQL
/// (DR-RANK); asserções sempre pelo endpoint.
/// </summary>
public sealed class TopClientesTestes(FabricaApi fabrica) : IAsyncLifetime
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private readonly HttpClient _cliente = fabrica.CreateClient();

    public async ValueTask InitializeAsync() => await fabrica.LimparBancoAsync(Cancelamento);

    public ValueTask DisposeAsync()
    {
        _cliente.Dispose();
        return ValueTask.CompletedTask;
    }

    private Task<DadosDeReferenciaRanking> InserirDrRankAsync() => DadosDeReferenciaRanking.InserirAsync(fabrica.Banco.StringDeConexao);

    private async Task<JsonElement> RankingAsync(string dataInicio, string dataFim)
    {
        var resposta = await _cliente.GetAsync($"/relatorios/top-clientes?dataInicio={dataInicio}&dataFim={dataFim}", Cancelamento);
        var corpo = await RespostaHttp.LerJsonAsync(resposta);
        Assert.True(resposta.StatusCode == HttpStatusCode.OK, $"Esperado 200, obtido {(int)resposta.StatusCode}: {corpo}");
        Assert.Equal("application/json", resposta.Content.Headers.ContentType?.MediaType);
        return corpo;
    }

    // (posicao, clienteId, valorTotal como no JSON)
    private static (int Posicao, int ClienteId, string ValorTotal)[] Linhas(JsonElement ranking) =>
        ranking.GetProperty("clientes").EnumerateArray()
            .Select(cliente => (
                cliente.GetProperty("posicao").GetInt32(),
                cliente.GetProperty("clienteId").GetInt32(),
                cliente.GetProperty("valorTotal").GetRawText()))
            .ToArray();

    private static int[] ClienteIds(JsonElement ranking) => Linhas(ranking).Select(linha => linha.ClienteId).ToArray();

    // IT-18: posições 1 a 5 exatamente como na §9.5.
    [Fact]
    public async Task TopClientes_DrRankEm2026_RetornaAsPosicoes1A5DaSpec()
    {
        var dr = await InserirDrRankAsync();

        var ranking = await RankingAsync("2026-01-01", "2026-12-31");

        Assert.Equal(["dataInicio", "dataFim", "clientes"], ranking.EnumerateObject().Select(propriedade => propriedade.Name));
        Assert.Equal("2026-01-01", ranking.GetProperty("dataInicio").GetString());
        Assert.Equal("2026-12-31", ranking.GetProperty("dataFim").GetString());
        Assert.Equal(
            [(1, dr.Bruno, "500.00"), (2, dr.Elisa, "450.00"), (3, dr.Carla, "400.00"), (4, dr.Diego, "400.00"), (5, dr.Ana1, "300.00")],
            Linhas(ranking));
        var primeiro = ranking.GetProperty("clientes")[0];
        Assert.Equal(["posicao", "clienteId", "nome", "valorTotal"], primeiro.EnumerateObject().Select(propriedade => propriedade.Name));
        Assert.Equal("Bruno Lima", primeiro.GetProperty("nome").GetString());
    }

    // IT-18 (homônimos): as duas "Ana Souza" ficam em linhas separadas, cada uma com o próprio total (nunca 550,00).
    [Fact]
    public async Task TopClientes_HomonimosNoPeriodo_FicamSeparadosPorClienteId()
    {
        var dr = await InserirDrRankAsync();

        var ranking = await RankingAsync("2026-03-01", "2026-04-30");

        Assert.Equal([(1, dr.Ana1, "300.00"), (2, dr.Ana2, "250.00")], Linhas(ranking));
        Assert.All(ranking.GetProperty("clientes").EnumerateArray(),
            cliente => Assert.Equal("Ana Souza", cliente.GetProperty("nome").GetString()));
    }

    // IT-18 (desempate): Carla e Diego empatam em 400,00; Carla tem o menor id e vem antes.
    [Fact]
    public async Task TopClientes_EmpateDeValor_DesempataPeloMenorClienteId()
    {
        var dr = await InserirDrRankAsync();

        var ranking = await RankingAsync("2026-01-01", "2026-12-31");

        var linhas = Linhas(ranking);
        Assert.True(dr.Carla < dr.Diego);
        Assert.Equal((3, dr.Carla, "400.00"), linhas[2]);
        Assert.Equal((4, dr.Diego, "400.00"), linhas[3]);
    }

    // IT-19: fronteiras do ano civil de São Paulo, com asserção explícita por cliente.
    [Fact]
    public async Task TopClientes_FronteirasDoAno2026_IncluiDiegoEElisaEExcluiFabioEOSegundoPedidoDeBruno()
    {
        var dr = await InserirDrRankAsync();

        var ranking = await RankingAsync("2026-01-01", "2026-12-31");

        var ids = ClienteIds(ranking);
        Assert.DoesNotContain(dr.Fabio, ids); // 31/12/2025 23:59:59 local, já 2026 em UTC
        Assert.Contains(dr.Elisa, ids); // 31/12/2026 23:59:59 local, já 2027 em UTC
        Assert.Contains(dr.Diego, ids); // 01/01/2026 00:00:00 local, exatamente o início
        Assert.Contains((1, dr.Bruno, "500.00"), Linhas(ranking)); // sem o pedido de 01/01/2027 00:00:00 (999,00)
        Assert.DoesNotContain(dr.SemPedidos, ids);
    }

    // IT-19 (lados de fora das fronteiras): cada pedido excluído de 2026 aparece no dia civil a que pertence.
    [Theory]
    [InlineData("2025-12-31", "Fabio", "1000.00")]
    [InlineData("2027-01-01", "Bruno", "999.00")]
    public async Task TopClientes_DiaVizinhoDeFronteira_ContemSoOPedidoDaqueleDiaCivil(string dia, string cliente, string valorTotal)
    {
        var dr = await InserirDrRankAsync();
        var clienteId = cliente == "Fabio" ? dr.Fabio : dr.Bruno;

        var ranking = await RankingAsync(dia, dia);

        Assert.Equal([(1, clienteId, valorTotal)], Linhas(ranking));
    }

    // IT-20 (três compradores): a lista tem 3 elementos, com posições 1 a 3 sem lacunas.
    [Fact]
    public async Task TopClientes_TresCompradoresNoPeriodo_RetornaTresElementos()
    {
        var dr = await InserirDrRankAsync();

        var ranking = await RankingAsync("2026-03-01", "2026-05-31");

        Assert.Equal([(1, dr.Carla, "400.00"), (2, dr.Ana1, "300.00"), (3, dr.Ana2, "250.00")], Linhas(ranking));
    }

    // IT-20 (período sem pedidos)
    [Fact]
    public async Task TopClientes_PeriodoSemPedidos_RetornaClientesVazio()
    {
        await InserirDrRankAsync();

        var ranking = await RankingAsync("2026-08-01", "2026-08-31");

        Assert.Empty(ranking.GetProperty("clientes").EnumerateArray());
        Assert.Equal("2026-08-01", ranking.GetProperty("dataInicio").GetString());
    }

    // dataFim no último dia representável não tem dia seguinte e não limita o período (sem lista vazia indevida).
    [Fact]
    public async Task TopClientes_DataFimNoUltimoDiaRepresentavel_NaoLimitaOPeriodo()
    {
        var dr = await InserirDrRankAsync();

        var ranking = await RankingAsync("2027-01-01", "9999-12-31");

        Assert.Equal([(1, dr.Bruno, "999.00")], Linhas(ranking));
    }

    // IT-20 (sem datas) e demais entradas inválidas: 400 VALIDACAO com as chaves dos campos.
    [Theory]
    [InlineData("", new[] { "dataFim", "dataInicio" })]
    [InlineData("?dataInicio=2026-01-01", new[] { "dataFim" })]
    [InlineData("?dataFim=2026-12-31", new[] { "dataInicio" })]
    [InlineData("?dataInicio=2026-12-31&dataFim=2026-01-01", new[] { "dataInicio" })]
    [InlineData("?dataInicio=2026-13-01&dataFim=2026-12-31", new[] { "dataInicio" })]
    [InlineData("?dataInicio=2026-01-01&dataFim=31/12/2026", new[] { "dataFim" })]
    public async Task TopClientes_DatasAusentesOuInvalidas_Retorna400ValidacaoNasChaves(string consulta, string[] chaves)
    {
        var resposta = await _cliente.GetAsync($"/relatorios/top-clientes{consulta}", Cancelamento);

        var problema = await RespostaHttp.LerProblemaAsync(resposta, HttpStatusCode.BadRequest, "VALIDACAO");
        Assert.Equal(chaves, problema.GetProperty("errors").EnumerateObject().Select(erro => erro.Name).Order());
    }
}
