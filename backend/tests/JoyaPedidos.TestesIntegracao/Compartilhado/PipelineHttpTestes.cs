using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using JoyaPedidos.TestesIntegracao.Infraestrutura;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace JoyaPedidos.TestesIntegracao.Compartilhado;

/// <summary>Pipeline HTTP base (plano E2; spec §5.2 e §5.9).</summary>
public sealed class PipelineHttpTestes(FabricaApi fabrica)
{
    private const string RotaDeFalha = "/teste/falha-proposital";
    private const string MensagemInterna = "detalhe interno que nao pode vazar";

    [Fact]
    public async Task Health_Get_Retorna200ComHealthy()
    {
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("Healthy", await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Health_Delete_Retorna405ComCodigoGenerico()
    {
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.DeleteAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.MethodNotAllowed, resposta.StatusCode);
        using var corpo = await LerProblema(resposta);
        Assert.Equal("REQUISICAO_INVALIDA", corpo.RootElement.GetProperty("codigo").GetString());
        Assert.Equal("Método não permitido", corpo.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task RotaInexistente_QualquerRequisicao_RetornaProblemDetailsComCodigoETraceId()
    {
        using var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/rota-que-nao-existe", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
        using var corpo = await LerProblema(resposta);
        Assert.Equal(404, corpo.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("RECURSO_NAO_ENCONTRADO", corpo.RootElement.GetProperty("codigo").GetString());
        Assert.Equal("Recurso não encontrado", corpo.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpo.RootElement.GetProperty("traceId").GetString()));
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("text/html")]
    public async Task ExcecaoNaoTratada_QualquerAccept_Retorna500SemDetalhesInternos(string accept)
    {
        await using var fabricaComFalha = fabrica.WithWebHostBuilder(construtor =>
            construtor.ConfigureServices(servicos => servicos.AddSingleton<IStartupFilter, FiltroComFalhaProposital>()));
        using var cliente = fabricaComFalha.CreateClient();
        cliente.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));

        var resposta = await cliente.GetAsync(RotaDeFalha, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.InternalServerError, resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain(MensagemInterna, texto);
        Assert.DoesNotContain(nameof(InvalidOperationException), texto);

        using var corpo = await LerProblema(resposta);
        Assert.Equal("ERRO_INTERNO", corpo.RootElement.GetProperty("codigo").GetString());
        Assert.Equal("Erro interno", corpo.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpo.RootElement.GetProperty("traceId").GetString()));
    }

    /// <summary>
    /// Erros do framework e da aplicação em todas as variantes de Accept (CA-14, com os casos do IT-08 e do IT-17;
    /// "" = sem cabeçalho Accept). Regressão da E11: com Accept: text/html havia text/plain ou JSON sem traceId.
    /// </summary>
    public static TheoryData<string, string, int, string, string> ErrosEmQualquerAccept()
    {
        string[] accepts = ["text/html", "application/json", "application/problem+json", "*/*", ""];
        (string Metodo, string Caminho, int Status, string Codigo)[] erros =
        [
            ("GET", "/pedidos/abc", 404, "RECURSO_NAO_ENCONTRADO"),
            ("GET", "/pedidos?pagina=abc", 400, "REQUISICAO_INVALIDA"),
            ("GET", "/rota-que-nao-existe", 404, "RECURSO_NAO_ENCONTRADO"),
            ("GET", $"/pedidos/{int.MaxValue}", 404, "PEDIDO_NAO_ENCONTRADO"),
            ("DELETE", "/pedidos", 405, "REQUISICAO_INVALIDA"),
            ("POST", "/pedidos", 415, "REQUISICAO_INVALIDA"),
        ];

        var dados = new TheoryData<string, string, int, string, string>();
        foreach (var (metodo, caminho, status, codigo) in erros)
        {
            foreach (var accept in accepts)
            {
                dados.Add(metodo, caminho, status, codigo, accept);
            }
        }
        return dados;
    }

    [Theory]
    [MemberData(nameof(ErrosEmQualquerAccept))]
    public async Task Erro_QualquerAccept_RetornaProblemDetailsComCodigoETraceId(
        string metodo, string caminho, int status, string codigo, string accept)
    {
        using var cliente = fabrica.CreateClient();
        using var requisicao = new HttpRequestMessage(new HttpMethod(metodo), caminho);
        if (accept != "")
        {
            requisicao.Headers.Accept.ParseAdd(accept);
        }
        if (metodo == "POST")
        {
            requisicao.Content = new StringContent("{}", Encoding.UTF8, "text/plain");
        }

        var resposta = await cliente.SendAsync(requisicao, TestContext.Current.CancellationToken);

        var corpo = await RespostaHttp.LerProblemaAsync(resposta, (HttpStatusCode)status, codigo);
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("title").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("detail").GetString()));
    }

    [Theory]
    [InlineData("/health", "text/plain")]
    [InlineData("/clientes", "application/json")]
    public async Task Sucesso_AcceptTextHtml_MantemRespostaOriginal(string caminho, string tipoEsperado)
    {
        using var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Accept.ParseAdd("text/html");

        var resposta = await cliente.GetAsync(caminho, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(tipoEsperado, resposta.Content.Headers.ContentType?.MediaType);
    }

    private static async Task<JsonDocument> LerProblema(HttpResponseMessage resposta)
    {
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    // Só para teste: acrescenta, depois do pipeline da API (portanto dentro do tratador de exceções),
    // uma rota que lança exceção com mensagem interna.
    private sealed class FiltroComFalhaProposital : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> proximo) => aplicacao =>
        {
            proximo(aplicacao);
            aplicacao.Use((contexto, seguinte) => contexto.Request.Path == RotaDeFalha
                ? throw new InvalidOperationException(MensagemInterna)
                : seguinte(contexto));
        };
    }
}
