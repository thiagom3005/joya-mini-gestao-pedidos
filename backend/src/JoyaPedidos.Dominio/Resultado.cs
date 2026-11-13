using System.Diagnostics.CodeAnalysis;

namespace JoyaPedidos.Dominio;

/// <summary>
/// Resultado de uma regra: um valor ou um <see cref="ErroNegocio"/>, nunca os dois. Falhas esperadas de negócio não
/// usam exceção; exceções ficam para erros de programação e de infraestrutura.
/// </summary>
public sealed class Resultado<T>
    where T : notnull
{
    private Resultado(T? valor, ErroNegocio? erro)
    {
        Valor = valor;
        Erro = erro;
    }

    public T? Valor { get; }

    public ErroNegocio? Erro { get; }

    [MemberNotNullWhen(true, nameof(Valor))]
    [MemberNotNullWhen(false, nameof(Erro))]
    public bool Sucesso => Erro is null;

    public static Resultado<T> Ok(T valor) => new(valor ?? throw new ArgumentNullException(nameof(valor)), null);

    public static Resultado<T> Falha(ErroNegocio erro) => new(default, erro ?? throw new ArgumentNullException(nameof(erro)));
}
