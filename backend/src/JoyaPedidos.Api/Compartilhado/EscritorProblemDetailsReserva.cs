namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Último escritor de Problem Details (spec §5.2, CA-14). O escritor padrão do ASP.NET Core só escreve quando o
/// <c>Accept</c> aceita JSON: um cliente que pede só <c>text/html</c> receberia <c>text/plain</c> das páginas de status
/// (erros do framework) ou, nos erros da aplicação, um JSON sem <c>traceId</c>. Quando nenhum outro escritor aceita a
/// requisição, este
/// delega ao escritor padrão, que produz o mesmo <c>application/problem+json</c> de qualquer outro <c>Accept</c>.
/// </summary>
public sealed class EscritorProblemDetailsReserva(IServiceProvider provedor) : IProblemDetailsWriter
{
    private IProblemDetailsWriter? _padrao;

    public bool CanWrite(ProblemDetailsContext context) => true;

    public ValueTask WriteAsync(ProblemDetailsContext context) => EscritorPadrao().WriteAsync(context);

    // Resolvido na primeira escrita: o construtor não pode pedir a lista de escritores, que inclui este.
    private IProblemDetailsWriter EscritorPadrao() =>
        _padrao ??= provedor.GetServices<IProblemDetailsWriter>().First(escritor => escritor is not EscritorProblemDetailsReserva);
}
