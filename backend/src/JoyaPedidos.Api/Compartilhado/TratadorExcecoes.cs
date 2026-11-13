using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Tratamento central de exceções não tratadas (spec §5.2, §7.5). Requisições malformadas detectadas pelo framework
/// mantêm o status original; erro SQL 1205 (vítima de deadlock) vira 503 <c>CONFLITO_CONCORRENCIA</c>, sem retry;
/// qualquer outra exceção vira 500 <c>ERRO_INTERNO</c>. Nenhuma resposta leva stack trace, SQL ou mensagem interna.
/// </summary>
public sealed class TratadorExcecoes(IProblemDetailsService problemDetails, ILogger<TratadorExcecoes> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excecao, CancellationToken cancelamento)
    {
        // Requisição cancelada pelo cliente (OperationCanceledException com RequestAborted) nem chega aqui: o
        // UseExceptionHandler responde 499 sozinho. A transação em curso já foi desfeita no descarte.
        var traceId = ConfiguracaoProblemDetails.IdentificadorDeRastreio(contexto);
        var problema = ProblemaPara(excecao);
        Registrar(contexto, excecao, problema.Status!.Value, traceId);

        contexto.Response.StatusCode = problema.Status.Value;
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

    private static ProblemDetails ProblemaPara(Exception excecao) => excecao switch
    {
        BadHttpRequestException requisicaoInvalida => new ProblemDetails
        {
            Status = requisicaoInvalida.StatusCode,
            Detail = "A requisição não pôde ser interpretada.",
        },
        SqlException erroSql => MapeadorErros.DeErroSql(erroSql.Number).ParaProblemDetails(),
        _ => MapeadorErros.ErroInterno().ParaProblemDetails(),
    };

    // 503 fica como aviso, com o número do erro, para a investigação exigida pela §7.5; 500 é erro, com a exceção.
    private void Registrar(HttpContext contexto, Exception excecao, int status, string traceId)
    {
        if (status == StatusCodes.Status503ServiceUnavailable && excecao is SqlException erroSql)
        {
            logger.LogWarning(excecao, "Conflito de concorrência (erro SQL {NumeroErroSql}) em {Metodo} {Caminho} (traceId {TraceId})",
                erroSql.Number, contexto.Request.Method, contexto.Request.Path, traceId);
        }
        else if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(excecao, "Erro não tratado em {Metodo} {Caminho} (traceId {TraceId})",
                contexto.Request.Method, contexto.Request.Path, traceId);
        }
    }
}
