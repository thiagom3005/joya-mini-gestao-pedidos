using JoyaPedidos.Api.Modulos.Pedidos.Contratos;

namespace JoyaPedidos.Api.Modulos.Pedidos;

/// <summary>
/// Validação de formato de <c>POST /pedidos</c> (spec §5.3; RN-02, RN-03, RN-04). Função pura, sem acesso a banco.
/// Devolve os erros por campo, com chaves no caminho JSON em camelCase e índices a partir de 0
/// (<c>clienteId</c>, <c>itens</c>, <c>itens[1].produtoId</c>, <c>itens[0].quantidade</c>). Vazio = requisição válida.
/// </summary>
public static class ValidadorCriarPedido
{
    /// <summary>DEC-15: limite de linhas por pedido.</summary>
    public const int MaximoDeItens = 50;

    /// <summary>DEC-15: limite de unidades por linha.</summary>
    public const int QuantidadeMaxima = 10_000;

    public static IReadOnlyDictionary<string, string[]> Validar(CriarPedidoRequisicao? requisicao)
    {
        var erros = new Dictionary<string, List<string>>();
        void Adicionar(string chave, string mensagem)
        {
            if (!erros.TryGetValue(chave, out var mensagens))
            {
                erros[chave] = mensagens = [];
            }
            mensagens.Add(mensagem);
        }

        var clienteId = requisicao?.ClienteId;
        if (clienteId is null)
        {
            Adicionar("clienteId", "O cliente é obrigatório.");
        }
        else if (clienteId <= 0)
        {
            Adicionar("clienteId", "O cliente deve ser um número inteiro positivo.");
        }

        var itens = requisicao?.Itens;
        if (itens is null)
        {
            Adicionar("itens", "Os itens são obrigatórios.");
        }
        else if (itens.Count == 0)
        {
            Adicionar("itens", "O pedido deve conter ao menos um item.");
        }
        else if (itens.Count > MaximoDeItens)
        {
            Adicionar("itens", $"O pedido pode ter no máximo {MaximoDeItens} itens.");
        }

        // Acima do limite, as linhas não são examinadas: o tamanho da resposta não cresce com o da requisição.
        IReadOnlyList<ItemPedidoRequisicao?> linhas = itens is { Count: <= MaximoDeItens } ? itens : [];
        var primeiraLinhaDoProduto = new Dictionary<int, int>();
        for (var indice = 0; indice < linhas.Count; indice++)
        {
            var item = linhas[indice];
            var prefixo = $"itens[{indice}]";
            if (item is null)
            {
                Adicionar(prefixo, "O item é obrigatório.");
                continue;
            }

            if (item.ProdutoId is null)
            {
                Adicionar($"{prefixo}.produtoId", "O produto é obrigatório.");
            }
            else if (item.ProdutoId <= 0)
            {
                Adicionar($"{prefixo}.produtoId", "O produto deve ser um número inteiro positivo.");
            }
            else if (primeiraLinhaDoProduto.TryGetValue(item.ProdutoId.Value, out var primeiraLinha))
            {
                // RN-04: o erro aparece na segunda ocorrência e nas seguintes (spec §5.3).
                Adicionar($"{prefixo}.produtoId", $"O produto {item.ProdutoId} já foi informado em itens[{primeiraLinha}].");
            }
            else
            {
                primeiraLinhaDoProduto[item.ProdutoId.Value] = indice;
            }

            if (item.Quantidade is null)
            {
                Adicionar($"{prefixo}.quantidade", "A quantidade é obrigatória.");
            }
            else if (item.Quantidade is < 1 or > QuantidadeMaxima)
            {
                Adicionar($"{prefixo}.quantidade", "A quantidade deve estar entre 1 e 10.000.");
            }
        }

        return erros.ToDictionary(par => par.Key, par => par.Value.ToArray());
    }
}
