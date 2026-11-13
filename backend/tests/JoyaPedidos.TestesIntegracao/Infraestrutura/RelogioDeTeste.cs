namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>
/// Relógio controlado da API nos testes de integração (spec RN-09, IT-13, IT-19). Ao contrário do FakeTimeProvider,
/// aceita voltar no tempo: testes diferentes, na mesma execução, precisam de instantes anteriores entre si
/// (ex.: fronteiras de 2025/2026 depois de um teste em 2027). Só o instante atual é controlado.
/// </summary>
public sealed class RelogioDeTeste(DateTimeOffset instanteInicial) : TimeProvider
{
    private long _ticksUtc = instanteInicial.UtcTicks;

    public DateTimeOffset Agora
    {
        get => new(Interlocked.Read(ref _ticksUtc), TimeSpan.Zero);
        set => Interlocked.Exchange(ref _ticksUtc, value.UtcTicks);
    }

    public override DateTimeOffset GetUtcNow() => Agora;

    public void Avancar(TimeSpan intervalo) => Agora = Agora.Add(intervalo);
}
