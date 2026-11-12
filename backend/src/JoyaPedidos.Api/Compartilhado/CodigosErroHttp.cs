namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Códigos estáveis de erro devolvidos na extensão <c>codigo</c> do Problem Details (spec §5.2).
/// Códigos da aplicação são definidos explicitamente por quem gera o erro; erros gerados pelo
/// framework recebem um código e um título genéricos derivados do status.
/// </summary>
public static class CodigosErroHttp
{
    public const string RecursoNaoEncontrado = "RECURSO_NAO_ENCONTRADO";
    public const string RequisicaoInvalida = "REQUISICAO_INVALIDA";
    public const string ErroInterno = "ERRO_INTERNO";

    /// <summary>Código genérico para um status sem código definido pela aplicação.</summary>
    public static string GenericoParaStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound => RecursoNaoEncontrado,
        >= 400 and < 500 => RequisicaoInvalida,
        _ => ErroInterno,
    };

    /// <summary>Título em pt-BR para erros do framework, igual em qualquer ambiente.</summary>
    public static string TituloGenericoParaStatus(int status) => status switch
    {
        StatusCodes.Status404NotFound => "Recurso não encontrado",
        StatusCodes.Status405MethodNotAllowed => "Método não permitido",
        StatusCodes.Status415UnsupportedMediaType => "Tipo de conteúdo não suportado",
        >= 400 and < 500 => "Requisição inválida",
        _ => "Erro interno",
    };
}
