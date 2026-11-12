using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Tratamento central de exceções não tratadas (spec §5.2). Requisições malformadas detectadas pelo
/// framework mantêm o status original; qualquer outra exceção vira 500 <c>ERRO_INTERNO</c>, sem stack trace,
/// SQL ou mensagem interna na resposta. O mapeamento do erro SQL 1205 para 503 entra na entrega E5.
/// </summary>
public sealed class TratadorExcecoes(IProblemDetailsService problemDetails, ILogger<TratadorExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken cancelamento)
    {
        var status = excecao is BadHttpRequestException requisicaoInvalida
            ? requisicaoInvalida.StatusCode
            : StatusCodes.Status500InternalServerError;

        var problema = new ProblemDetails
        {
            Status = status,
            Detail = status >= StatusCodes.Status500InternalServerError
                ? "Ocorreu um erro inesperado."
                : "A requisição não pôde ser interpretada.",
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(
                excecao,
                "Erro não tratado em {Metodo} {Caminho} (traceId {TraceId})",
                contexto.Request.Method,
                contexto.Request.Path,
                ConfiguracaoProblemDetails.IdentificadorDeRastreio(contexto));
        }

        contexto.Response.StatusCode = status;
        var escrito = await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            ProblemDetails = problema,
            Exception = excecao,
        });

        // Clientes que não aceitam JSON (ex.: Accept: text/html) também recebem o Problem Details, nunca corpo vazio.
        if (!escrito)
        {
            ConfiguracaoProblemDetails.Padronizar(problema, contexto);
            await Results.Problem(problema).ExecuteAsync(contexto);
        }
        return true;
    }
}
