namespace ProjetoComErro;

// Erro de compilação PROPOSITAL (fixture do hook pos-edicao.mjs): int não aceita texto.
public static class ComErro
{
    public static int Valor() => "isto não é um número";
}
