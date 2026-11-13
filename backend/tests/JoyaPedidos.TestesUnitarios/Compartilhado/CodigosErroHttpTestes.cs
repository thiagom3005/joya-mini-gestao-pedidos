using JoyaPedidos.Api.Compartilhado;

namespace JoyaPedidos.TestesUnitarios.Compartilhado;

/// <summary>Código e título genéricos por status para erros gerados pelo framework (spec §5.2).</summary>
public sealed class CodigosErroHttpTestes
{
    [Theory]
    [InlineData(404, "RECURSO_NAO_ENCONTRADO")]
    [InlineData(400, "REQUISICAO_INVALIDA")]
    [InlineData(405, "REQUISICAO_INVALIDA")]
    [InlineData(415, "REQUISICAO_INVALIDA")]
    [InlineData(500, "ERRO_INTERNO")]
    public void GenericoParaStatus_StatusDeErro_RetornaCodigoDaSpec(int status, string codigoEsperado)
    {
        var codigo = CodigosErroHttp.GenericoParaStatus(status);

        Assert.Equal(codigoEsperado, codigo);
    }

    [Theory]
    [InlineData(400, "Requisição inválida")]
    [InlineData(404, "Recurso não encontrado")]
    [InlineData(405, "Método não permitido")]
    [InlineData(415, "Tipo de conteúdo não suportado")]
    [InlineData(500, "Erro interno")]
    public void TituloGenericoParaStatus_StatusDeErro_RetornaTituloEmPortugues(int status, string tituloEsperado)
    {
        var titulo = CodigosErroHttp.TituloGenericoParaStatus(status);

        Assert.Equal(tituloEsperado, titulo);
    }

    [Theory]
    [InlineData(400, "A requisição não pôde ser interpretada.")]
    [InlineData(404, "O recurso solicitado não existe.")]
    [InlineData(405, "O método HTTP não é permitido para este recurso.")]
    [InlineData(415, "O tipo de conteúdo não é suportado. Envie application/json.")]
    [InlineData(500, "Ocorreu um erro inesperado.")]
    public void DetalheGenericoParaStatus_StatusDeErro_RetornaDetalheEmPortugues(int status, string detalheEsperado)
    {
        var detalhe = CodigosErroHttp.DetalheGenericoParaStatus(status);

        Assert.Equal(detalheEsperado, detalhe);
    }
}
