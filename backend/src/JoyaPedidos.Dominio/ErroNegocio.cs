namespace JoyaPedidos.Dominio;

/// <summary>
/// Falha esperada de uma regra de negócio. Carrega só os dados do problema; texto, status HTTP e código do contrato
/// são decididos na camada HTTP (MapeadorErros), para o domínio não depender de apresentação.
/// </summary>
public abstract record ErroNegocio;

/// <summary>RN-01: o cliente informado não existe.</summary>
public sealed record ClienteNaoEncontrado(int ClienteId) : ErroNegocio;

/// <summary>
/// RN-05: produtos informados que não existem, em ordem crescente e sem repetição. Guarda uma cópia somente leitura da
/// lista e compara pelos elementos.
/// </summary>
public sealed record ProdutosNaoEncontrados : ErroNegocio
{
    public ProdutosNaoEncontrados(IEnumerable<int> produtoIds) => ProdutoIds = produtoIds.ToList().AsReadOnly();

    public IReadOnlyList<int> ProdutoIds { get; }

    public bool Equals(ProdutosNaoEncontrados? outro) => outro is not null && ProdutoIds.SequenceEqual(outro.ProdutoIds);

    public override int GetHashCode() => ProdutoIds.Count;
}

/// <summary>
/// RN-06: itens sem estoque suficiente, em ordem crescente de produto. Guarda uma cópia somente leitura da lista e
/// compara pelos elementos.
/// </summary>
public sealed record EstoqueInsuficiente : ErroNegocio
{
    public EstoqueInsuficiente(IEnumerable<ItemSemEstoque> itens) => Itens = itens.ToList().AsReadOnly();

    public IReadOnlyList<ItemSemEstoque> Itens { get; }

    public bool Equals(EstoqueInsuficiente? outro) => outro is not null && Itens.SequenceEqual(outro.Itens);

    public override int GetHashCode() => Itens.Count;
}

/// <summary>Detalhe de um item sem estoque (spec §5.3, extensão <c>itensSemEstoque</c>).</summary>
public sealed record ItemSemEstoque(int ProdutoId, string NomeProduto, int QuantidadeSolicitada, int EstoqueDisponivel);
