using Microsoft.Data.SqlClient;

namespace JoyaPedidos.Api.Compartilhado;

/// <summary>
/// Abre conexões com o banco do projeto a partir da string de conexão <c>ConnectionStrings:BancoPedidos</c>.
/// Quem abre é dono da conexão e a descarta (<c>await using</c>); o pool do SqlClient reaproveita as conexões físicas.
/// </summary>
public sealed class FabricaConexao
{
    public const string NomeDaStringDeConexao = "BancoPedidos";

    private readonly string _stringDeConexao;

    private FabricaConexao(string stringDeConexao) => _stringDeConexao = stringDeConexao;

    /// <summary>Falha rápida e com mensagem clara se a string de conexão não estiver configurada.</summary>
    public static FabricaConexao Criar(IConfiguration configuracao)
    {
        var stringDeConexao = configuracao.GetConnectionString(NomeDaStringDeConexao);
        if (string.IsNullOrWhiteSpace(stringDeConexao))
        {
            throw new InvalidOperationException(
                $"String de conexão '{NomeDaStringDeConexao}' não configurada. Defina ConnectionStrings__{NomeDaStringDeConexao} " +
                "no ambiente da API.");
        }
        return new FabricaConexao(stringDeConexao);
    }

    public async Task<SqlConnection> AbrirAsync(CancellationToken cancelamento)
    {
        var conexao = new SqlConnection(_stringDeConexao);
        try
        {
            await conexao.OpenAsync(cancelamento);
            return conexao;
        }
        catch
        {
            await conexao.DisposeAsync();
            throw;
        }
    }
}
