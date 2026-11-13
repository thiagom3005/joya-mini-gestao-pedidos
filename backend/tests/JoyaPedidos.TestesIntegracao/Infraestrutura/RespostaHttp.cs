using System.Net;
using System.Text.Json;

namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>Leitura de respostas da API nos testes: JSON do corpo e conferência do Problem Details (spec §5.2).</summary>
public static class RespostaHttp
{
    public static async Task<JsonElement> LerJsonAsync(HttpResponseMessage resposta)
    {
        var texto = await resposta.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var documento = JsonDocument.Parse(texto);
        return documento.RootElement.Clone();
    }

    /// <summary>
    /// Confere status, <c>application/problem+json</c>, <c>codigo</c> e <c>traceId</c>; devolve o corpo para as asserções
    /// específicas de cada erro.
    /// </summary>
    public static async Task<JsonElement> LerProblemaAsync(HttpResponseMessage resposta, HttpStatusCode status, string codigo)
    {
        var corpo = await LerJsonAsync(resposta);
        Assert.True(status == resposta.StatusCode, $"Esperado {(int)status}, obtido {(int)resposta.StatusCode}: {corpo}");
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal(codigo, corpo.GetProperty("codigo").GetString());
        Assert.Equal((int)status, corpo.GetProperty("status").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("traceId").GetString()));
        return corpo;
    }
}
