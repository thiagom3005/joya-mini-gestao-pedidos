using System.Globalization;
using JoyaPedidos.TestesIntegracao.Infraestrutura;
using Microsoft.Data.SqlClient;

namespace JoyaPedidos.TestesIntegracao.Relatorios;

/// <summary>
/// Parte 4 (spec §9.1–9.5): IT-23 executa <c>database/consultas/ranking-2026-corrigida.sql</c> e IT-24 executa
/// <c>database/consultas/ranking-2026-original.sql</c>, ambos sem alteração, sobre o DR-RANK em banco limpo.
/// </summary>
public sealed class RankingSqlTestes(FabricaApi fabrica) : IAsyncLifetime
{
    private static CancellationToken Cancelamento => TestContext.Current.CancellationToken;

    private const string ArquivoCorrigido = "ranking-2026-corrigida.sql";
    private const string ArquivoOriginal = "ranking-2026-original.sql";

    public async ValueTask InitializeAsync() => await fabrica.LimparBancoAsync(Cancelamento);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private Task<DadosDeReferenciaRanking> InserirDrRankAsync() => DadosDeReferenciaRanking.InserirAsync(fabrica.Banco.StringDeConexao);

    private sealed record Resultado(IReadOnlyList<string> Colunas, IReadOnlyList<object[]> Linhas);

    private static string LerConsulta(string arquivo) =>
        File.ReadAllText(Path.Combine(SqlServerDeTeste.LocalizarPastaDatabase(), "consultas", arquivo));

    /// <summary>Executa o arquivo de consulta exatamente como está no repositório.</summary>
    private async Task<Resultado> ExecutarConsultaAsync(string arquivo)
    {
        await using var conexao = new SqlConnection(fabrica.Banco.StringDeConexao);
        await conexao.OpenAsync(Cancelamento);
        await using var comando = new SqlCommand(LerConsulta(arquivo), conexao);
        await using var leitor = await comando.ExecuteReaderAsync(Cancelamento);
        var colunas = Enumerable.Range(0, leitor.FieldCount).Select(leitor.GetName).ToArray();
        var linhas = new List<object[]>();
        while (await leitor.ReadAsync(Cancelamento))
        {
            var valores = new object[leitor.FieldCount];
            leitor.GetValues(valores);
            linhas.Add(valores);
        }
        return new Resultado(colunas, linhas);
    }

    // Decimal como texto invariante: "500.00" prova o valor e a escala 2 (DECIMAL(18,2) preservado na soma).
    private static string Texto(object valor) => Convert.ToString(valor, CultureInfo.InvariantCulture)!;

    // (Posicao, ClienteId, Nome, Total)
    private static (long Posicao, int ClienteId, string Nome, string Total)[] LinhasCorrigidas(Resultado resultado) =>
        resultado.Linhas.Select(linha => ((long)linha[0], (int)linha[1], (string)linha[2], Texto(linha[3]))).ToArray();

    // (Nome, Total) na ordem devolvida
    private static (string Nome, string Total)[] LinhasOriginais(Resultado resultado) =>
        resultado.Linhas.Select(linha => ((string)linha[0], Texto(linha[1]))).ToArray();

    // IT-23: resultado idêntico à tabela da §9.5 (6 linhas, ranking completo).
    [Fact]
    public async Task RankingCorrigido_DrRank_RetornaAsSeisPosicoesDaSpec()
    {
        var dr = await InserirDrRankAsync();

        var resultado = await ExecutarConsultaAsync(ArquivoCorrigido);

        Assert.Equal(["Posicao", "ClienteId", "Nome", "Total"], resultado.Colunas);
        Assert.Equal(
            [
                (1L, dr.Bruno, "Bruno Lima", "500.00"),
                (2L, dr.Elisa, "Elisa Rocha", "450.00"),
                (3L, dr.Carla, "Carla Dias", "400.00"),
                (4L, dr.Diego, "Diego Alves", "400.00"),
                (5L, dr.Ana1, "Ana Souza", "300.00"),
                (6L, dr.Ana2, "Ana Souza", "250.00"),
            ],
            LinhasCorrigidas(resultado));
    }

    // IT-23 (D2): as duas "Ana Souza" ficam em linhas separadas, identificadas pelo ClienteId (nunca 550,00).
    [Fact]
    public async Task RankingCorrigido_Homonimos_FicamSeparadosPorClienteId()
    {
        var dr = await InserirDrRankAsync();

        var linhas = LinhasCorrigidas(await ExecutarConsultaAsync(ArquivoCorrigido));

        var anas = linhas.Where(linha => linha.Nome == "Ana Souza").ToArray();
        Assert.Equal([(5L, dr.Ana1, "Ana Souza", "300.00"), (6L, dr.Ana2, "Ana Souza", "250.00")], anas);
        Assert.DoesNotContain(linhas, linha => linha.Total == "550.00");
    }

    // IT-23 (D5): Carla e Diego empatam em 400,00; o menor ClienteId (Carla) vem antes, com posições distintas.
    [Fact]
    public async Task RankingCorrigido_EmpateDeTotal_DesempataPeloMenorClienteId()
    {
        var dr = await InserirDrRankAsync();

        var linhas = LinhasCorrigidas(await ExecutarConsultaAsync(ArquivoCorrigido));

        Assert.True(dr.Carla < dr.Diego);
        Assert.Equal((3L, dr.Carla, "Carla Dias", "400.00"), linhas[2]);
        Assert.Equal((4L, dr.Diego, "Diego Alves", "400.00"), linhas[3]);
    }

    // IT-23 (D4): ano civil de São Paulo. Fábio (31/12/2025 23:59:59 local) e o pedido de Bruno de 01/01/2027
    // 00:00:00 local ficam fora; Diego (01/01/2026 00:00:00) e Elisa (31/12/2026 23:59:59) ficam dentro.
    [Fact]
    public async Task RankingCorrigido_FronteirasDoAnoLocal_IncluemEExcluemOsPedidosCertos()
    {
        var dr = await InserirDrRankAsync();

        var linhas = LinhasCorrigidas(await ExecutarConsultaAsync(ArquivoCorrigido));

        Assert.DoesNotContain(linhas, linha => linha.ClienteId == dr.Fabio);
        Assert.DoesNotContain(linhas, linha => linha.ClienteId == dr.SemPedidos);
        Assert.Contains(linhas, linha => linha.ClienteId == dr.Diego && linha.Total == "400.00");
        Assert.Contains(linhas, linha => linha.ClienteId == dr.Elisa && linha.Total == "450.00");
        Assert.Contains(linhas, linha => linha.ClienteId == dr.Bruno && linha.Total == "500.00");
    }

    // IT-23 (RN-08): o total usa o preço gravado no item; mudar o preço atual dos produtos não altera o ranking.
    [Fact]
    public async Task RankingCorrigido_PrecoAtualAlterado_UsaPrecoHistoricoDosItens()
    {
        var dr = await InserirDrRankAsync();
        var antes = LinhasCorrigidas(await ExecutarConsultaAsync(ArquivoCorrigido));

        var produtosAlterados = await ConsultaSql.EscalarAsync<int>(
            fabrica.Banco.StringDeConexao, "UPDATE dbo.Produtos SET Preco = 9999.99; SELECT @@ROWCOUNT;", Cancelamento);
        var depois = LinhasCorrigidas(await ExecutarConsultaAsync(ArquivoCorrigido));

        Assert.Equal(2, produtosAlterados);
        Assert.Equal(antes, depois);
        Assert.Equal((1L, dr.Bruno, "Bruno Lima", "500.00"), depois[0]);
    }

    // IT-24: 5 linhas só com Nome e Total. Uma única "Ana Souza" com 550,00 (D2); Fábio presente com 1.000,00 e
    // Elisa ausente (D4); totais em ordem crescente, a primeira linha com 400,00 (D1). Carla e Diego empatam em
    // 400,00 sem ordem definida: o nome nas duas primeiras posições não é verificado.
    [Fact]
    public async Task RankingOriginal_DrRank_CaracterizaOsDefeitosD1D2D4()
    {
        await InserirDrRankAsync();

        var resultado = await ExecutarConsultaAsync(ArquivoOriginal);
        var linhas = LinhasOriginais(resultado);

        Assert.Equal(["Nome", "Total"], resultado.Colunas);
        Assert.Equal(5, linhas.Length);
        Assert.Equal("400.00", linhas[0].Total);
        Assert.Equal(["400.00", "400.00", "500.00", "550.00", "1000.00"], linhas.Select(linha => linha.Total));
        Assert.Equal(
            [("Ana Souza", "550.00"), ("Bruno Lima", "500.00"), ("Carla Dias", "400.00"), ("Diego Alves", "400.00"), ("Fabio Nunes", "1000.00")],
            linhas.OrderBy(linha => linha.Nome, StringComparer.Ordinal));
    }

    // IT-24 (D2): os homônimos viram uma linha só, com os totais somados (300,00 + 250,00).
    [Fact]
    public async Task RankingOriginal_Homonimos_SaoAgrupadosNumaUnicaLinha()
    {
        await InserirDrRankAsync();

        var linhas = LinhasOriginais(await ExecutarConsultaAsync(ArquivoOriginal));

        Assert.Equal([("Ana Souza", "550.00")], linhas.Where(linha => linha.Nome == "Ana Souza"));
    }

    // IT-24 (D4): YEAR() sobre o instante UTC inclui Fábio (31/12/2025 local = 2026 UTC) e exclui Elisa
    // (31/12/2026 local = 2027 UTC).
    [Fact]
    public async Task RankingOriginal_AnoUtc_IncluiFabioEExcluiElisa()
    {
        await InserirDrRankAsync();

        var linhas = LinhasOriginais(await ExecutarConsultaAsync(ArquivoOriginal));

        Assert.Contains(("Fabio Nunes", "1000.00"), linhas);
        Assert.DoesNotContain(linhas, linha => linha.Nome == "Elisa Rocha");
    }

    // CA-12: o arquivo da query original é idêntico ao bloco da spec §9.1 (literal do enunciado).
    [Fact]
    public void RankingOriginal_Arquivo_EIdenticoAoTextoDaSpec()
    {
        var spec = File.ReadAllText(Path.Combine(SqlServerDeTeste.LocalizarPastaDatabase(), "..", "docs", "spec.md"));

        Assert.Equal(BlocoSqlApos(spec, "### 9.1 Query original"), Normalizar(LerConsulta(ArquivoOriginal)));
    }

    private static string Normalizar(string texto) => texto.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');

    private static string BlocoSqlApos(string documento, string titulo)
    {
        var texto = Normalizar(documento);
        var inicioDaSecao = texto.IndexOf(titulo, StringComparison.Ordinal);
        Assert.True(inicioDaSecao >= 0, $"Seção '{titulo}' não encontrada na spec.");
        const string abertura = "```sql\n";
        var inicio = texto.IndexOf(abertura, inicioDaSecao, StringComparison.Ordinal) + abertura.Length;
        var fim = texto.IndexOf("\n```", inicio, StringComparison.Ordinal);
        return texto[inicio..fim];
    }
}
