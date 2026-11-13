using Microsoft.Data.SqlClient;

namespace JoyaPedidos.TestesIntegracao.Infraestrutura;

/// <summary>Consultas de apoio para arranjos e verificações diretas no banco de teste.</summary>
public static class ConsultaSql
{
    public static async Task<T> EscalarAsync<T>(string stringDeConexao, string sql, CancellationToken cancelamento)
    {
        await using var conexao = new SqlConnection(stringDeConexao);
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(sql, conexao);
        var valor = await comando.ExecuteScalarAsync(cancelamento);
        return (T)Convert.ChangeType(valor!, typeof(T), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Primeira coluna de todas as linhas, como texto.</summary>
    public static async Task<IReadOnlyList<string>> ListaAsync(string stringDeConexao, string sql, CancellationToken cancelamento)
    {
        await using var conexao = new SqlConnection(stringDeConexao);
        await conexao.OpenAsync(cancelamento);
        await using var comando = new SqlCommand(sql, conexao);
        await using var leitor = await comando.ExecuteReaderAsync(cancelamento);
        var valores = new List<string>();
        while (await leitor.ReadAsync(cancelamento))
        {
            valores.Add(Convert.ToString(leitor.GetValue(0), System.Globalization.CultureInfo.InvariantCulture)!);
        }
        return valores;
    }

    /// <summary>Executa um comando dentro de uma transação desfeita ao final; devolve a exceção SQL, se houver.</summary>
    public static async Task<SqlException?> TentarEmTransacaoDesfeitaAsync(string stringDeConexao, string sql, CancellationToken cancelamento)
    {
        await using var conexao = new SqlConnection(stringDeConexao);
        await conexao.OpenAsync(cancelamento);
        await using var transacao = (SqlTransaction)await conexao.BeginTransactionAsync(cancelamento);
        try
        {
            await using var comando = new SqlCommand(sql, conexao, transacao);
            await comando.ExecuteNonQueryAsync(cancelamento);
            return null;
        }
        catch (SqlException erro)
        {
            return erro;
        }
        finally
        {
            if (transacao.Connection is not null)
            {
                await transacao.RollbackAsync(cancelamento);
            }
        }
    }
}
