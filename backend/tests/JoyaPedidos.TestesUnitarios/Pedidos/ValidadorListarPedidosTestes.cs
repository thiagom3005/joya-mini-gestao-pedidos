using JoyaPedidos.Api.Modulos.Pedidos;
using JoyaPedidos.Api.Modulos.Pedidos.Contratos;

namespace JoyaPedidos.TestesUnitarios.Pedidos;

/// <summary>UT-14: validação de GET /pedidos (spec §5.4, §8.1; RN-15).</summary>
public sealed class ValidadorListarPedidosTestes
{
    private static ListarPedidosParametros Parametros(
        int? clienteId = null, string? dataInicio = null, string? dataFim = null, int? pagina = null, int? tamanhoPagina = null) =>
        new(clienteId, dataInicio, dataFim, pagina, tamanhoPagina);

    // UT-14 (parâmetros omitidos)
    [Fact]
    public void Validar_ParametrosOmitidos_AplicaPadroesPagina1ETamanho10SemFiltros()
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros());

        Assert.Empty(erros);
        Assert.Equal(new FiltroListarPedidos(null, null, null, 1, 10), filtro);
    }

    // UT-14 (filtros válidos)
    [Fact]
    public void Validar_TodosOsParametrosValidos_DevolveOFiltroComAsDatasConvertidas()
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(3, "2026-01-01", "2026-12-31", 2, 25));

        Assert.Empty(erros);
        Assert.Equal(new FiltroListarPedidos(3, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), 2, 25), filtro);
    }

    // UT-14 (pagina 0)
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validar_PaginaMenorQue1_RetornaErroNaChavePagina(int pagina)
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(pagina: pagina));

        Assert.Equal(["pagina"], erros.Keys);
        Assert.Null(filtro);
    }

    // UT-14 (tamanhoPagina 0 e 101): rejeitado, nunca ajustado para a faixa.
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void Validar_TamanhoPaginaForaDe1A100_RetornaErroSemAjustar(int tamanhoPagina)
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(tamanhoPagina: tamanhoPagina));

        Assert.Equal(["tamanhoPagina"], erros.Keys);
        Assert.Equal(["O tamanho da página deve estar entre 1 e 100."], erros["tamanhoPagina"]);
        Assert.Null(filtro);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void Validar_TamanhoPaginaNosLimites_EhValido(int tamanhoPagina)
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(tamanhoPagina: tamanhoPagina));

        Assert.Empty(erros);
        Assert.Equal(tamanhoPagina, filtro!.TamanhoPagina);
    }

    // Página muito alta é válida: devolve lista vazia, sem estouro no deslocamento (§8.3).
    [Fact]
    public void Validar_PaginaMaximaDoInteiro_EhValida()
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(pagina: int.MaxValue));

        Assert.Empty(erros);
        Assert.Equal(int.MaxValue, filtro!.Pagina);
    }

    // UT-14 (clienteId 0)
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public void Validar_ClienteIdNaoPositivo_RetornaErroNaChaveClienteId(int clienteId)
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(clienteId: clienteId));

        Assert.Equal(["clienteId"], erros.Keys);
        Assert.Null(filtro);
    }

    // UT-14 (dataInicio > dataFim)
    [Fact]
    public void Validar_DataInicioPosteriorADataFim_RetornaErroNaChaveDataInicio()
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(dataInicio: "2026-02-01", dataFim: "2026-01-31"));

        Assert.Equal(["dataInicio"], erros.Keys);
        Assert.Equal(["A data inicial não pode ser posterior à data final."], erros["dataInicio"]);
        Assert.Null(filtro);
    }

    [Fact]
    public void Validar_MesmoDiaNasDuasDatas_EhValido()
    {
        var (erros, filtro) = ValidadorListarPedidos.Validar(Parametros(dataInicio: "2026-03-15", dataFim: "2026-03-15"));

        Assert.Empty(erros);
        Assert.Equal(filtro!.DataInicio, filtro.DataFim);
    }

    // UT-14 (data 2026-13-01 e 01/02/2026), mais outras formas fora de yyyy-MM-dd.
    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("01/02/2026")]
    [InlineData("2026-02-30")]
    [InlineData("2026-1-1")]
    [InlineData("2026-01-01T00:00:00")]
    [InlineData(" 2026-01-01")]
    [InlineData("")]
    public void Validar_DataForaDoFormatoExato_RetornaErroNaChaveDaData(string data)
    {
        var (errosInicio, filtroInicio) = ValidadorListarPedidos.Validar(Parametros(dataInicio: data));
        var (errosFim, filtroFim) = ValidadorListarPedidos.Validar(Parametros(dataFim: data));

        Assert.Equal(["dataInicio"], errosInicio.Keys);
        Assert.Equal(["A data inicial deve estar no formato yyyy-MM-dd."], errosInicio["dataInicio"]);
        Assert.Equal(["dataFim"], errosFim.Keys);
        Assert.Equal(["A data final deve estar no formato yyyy-MM-dd."], errosFim["dataFim"]);
        Assert.Null(filtroInicio);
        Assert.Null(filtroFim);
    }

    // Data inválida não entra na comparação de ordem: só o erro de formato aparece.
    [Fact]
    public void Validar_DataFimInvalidaComInicioValido_RetornaSoOErroDeFormato()
    {
        var (erros, _) = ValidadorListarPedidos.Validar(Parametros(dataInicio: "2026-05-01", dataFim: "2026-13-01"));

        Assert.Equal(["dataFim"], erros.Keys);
    }

    [Fact]
    public void Validar_VariosParametrosInvalidos_RetornaTodosOsErrosDeUmaVez()
    {
        var (erros, _) = ValidadorListarPedidos.Validar(Parametros(0, "abc", "2026-01-01", 0, 101));

        Assert.Equal(["clienteId", "dataInicio", "pagina", "tamanhoPagina"], erros.Keys.Order());
    }
}
