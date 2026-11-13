using System.Data;
using Microsoft.Data.SqlClient;

namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>
/// Sessão SQL paralela à API, com transação aberta, que segura locks de produto para provocar espera, deadlock ou
/// cancelamento de forma determinística. As esperas consultam o estado real do servidor (<c>sys.dm_exec_requests</c>)
/// com prazo máximo; não há atraso fixo esperando "o suficiente".
/// </summary>
public sealed class SessaoDeBloqueio : IAsyncDisposable
{
    private static readonly TimeSpan PrazoDeEspera = TimeSpan.FromSeconds(30);

    private readonly string _stringDeConexao;
    private readonly SqlConnection _conexao;
    private readonly SqlTransaction _transacao;

    private SessaoDeBloqueio(string stringDeConexao, SqlConnection conexao, SqlTransaction transacao, short spid)
    {
        _stringDeConexao = stringDeConexao;
        _conexao = conexao;
        _transacao = transacao;
        Spid = spid;
    }

    /// <summary>Id da sessão no SQL Server (<c>@@SPID</c>).</summary>
    public short Spid { get; }

    /// <param name="prioridadeAltaEmDeadlock">
    /// <c>SET DEADLOCK_PRIORITY HIGH</c>: num deadlock com a API (prioridade normal), a vítima escolhida é a API.
    /// </param>
    public static async Task<SessaoDeBloqueio> AbrirAsync(string stringDeConexao, bool prioridadeAltaEmDeadlock = false)
    {
        var cancelamento = TestContext.Current.CancellationToken;
        var conexao = new SqlConnection(stringDeConexao);
        try
        {
            await conexao.OpenAsync(cancelamento);
            var preparo = prioridadeAltaEmDeadlock ? "SET DEADLOCK_PRIORITY HIGH; SELECT @@SPID;" : "SELECT @@SPID;";
            await using var comando = new SqlCommand(preparo, conexao);
            var spid = (short)(await comando.ExecuteScalarAsync(cancelamento))!;
            var transacao = (SqlTransaction)await conexao.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancelamento);
            return new SessaoDeBloqueio(stringDeConexao, conexao, transacao, spid);
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Lock U no produto (o mesmo da API, §7.2), retido até o fim da transação desta sessão. Fica esperando se outra
    /// sessão já tiver lock incompatível.
    /// </summary>
    public async Task BloquearProdutoAsync(int produtoId, CancellationToken cancelamento)
    {
        await using var comando = new SqlCommand(
            "SELECT Estoque FROM dbo.Produtos WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id;", _conexao, _transacao);
        comando.Parameters.AddWithValue("@Id", produtoId);
        await comando.ExecuteScalarAsync(cancelamento);
    }

    /// <summary>Espera até alguma outra sessão (a da API) ficar bloqueada aguardando um lock desta.</summary>
    public Task EsperarSessaoBloqueadaPorEstaAsync() =>
        EsperarAsync(bloqueadas => bloqueadas > 0, "nenhuma sessão ficou bloqueada", PrazoDeEspera);

    /// <summary>
    /// Espera até nenhuma sessão estar mais bloqueada por esta (ex.: a API desistiu da espera). O prazo é do chamador:
    /// para provar um cancelamento, ele precisa ficar bem abaixo do tempo limite de comando do SqlClient (30 s), que
    /// também encerraria a espera.
    /// </summary>
    public Task EsperarNenhumaSessaoBloqueadaPorEstaAsync(TimeSpan prazo) =>
        EsperarAsync(bloqueadas => bloqueadas == 0, "a sessão bloqueada não desistiu da espera", prazo);

    public Task DesfazerAsync() => _transacao.RollbackAsync(TestContext.Current.CancellationToken);

    /// <summary>
    /// Verifica, numa sessão nova, se dá para obter o lock U do produto em até 2 s. Falso com o erro 1222 (tempo de
    /// espera por lock esgotado): alguma transação ainda segura o produto.
    /// </summary>
    public static async Task<bool> ProdutoLivreAsync(string stringDeConexao, int produtoId)
    {
        await using var sessao = await AbrirAsync(stringDeConexao);
        await using var comando = new SqlCommand(
            "SET LOCK_TIMEOUT 2000; SELECT Estoque FROM dbo.Produtos WITH (UPDLOCK, ROWLOCK) WHERE Id = @Id;", sessao._conexao, sessao._transacao);
        comando.Parameters.AddWithValue("@Id", produtoId);
        try
        {
            await comando.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return true;
        }
        catch (SqlException erro) when (erro.Number == 1222)
        {
            return false;
        }
    }

    private async Task EsperarAsync(Func<int, bool> condicao, string mensagemDeFalha, TimeSpan prazo)
    {
        var limite = DateTime.UtcNow + prazo;
        while (true)
        {
            var bloqueadas = await ConsultaSql.EscalarAsync<int>(
                _stringDeConexao,
                "SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id = @Spid;",
                TestContext.Current.CancellationToken,
                ("@Spid", Spid));
            if (condicao(bloqueadas))
            {
                return;
            }
            Assert.True(DateTime.UtcNow < limite, $"Prazo de {prazo.TotalSeconds} s esgotado: {mensagemDeFalha}.");
            await Task.Delay(TimeSpan.FromMilliseconds(20), TestContext.Current.CancellationToken);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _transacao.DisposeAsync();
        await _conexao.DisposeAsync();
    }
}
