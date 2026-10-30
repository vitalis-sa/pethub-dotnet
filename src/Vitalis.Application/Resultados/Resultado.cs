namespace Vitalis.Application.Resultados;

/// <summary>Por que um caso de uso não pôde ser concluído. A API traduz cada tipo para um status HTTP.</summary>
public enum TipoDeErro
{
    Invalido,
    NaoAutorizado,
    Proibido,
    NaoEncontrado,
    Conflito,
    Indisponivel
}

/// <param name="Tipo">Categoria do erro.</param>
/// <param name="Mensagem">Texto para o cliente.</param>
/// <param name="Campo">Para <see cref="TipoDeErro.Invalido"/>: qual parâmetro está errado.</param>
public sealed record Erro(TipoDeErro Tipo, string Mensagem, string? Campo = null)
{
    public static Erro Invalido(string campo, string mensagem) => new(TipoDeErro.Invalido, mensagem, campo);
    public static Erro NaoAutorizado(string mensagem) => new(TipoDeErro.NaoAutorizado, mensagem);
    public static Erro Proibido(string mensagem) => new(TipoDeErro.Proibido, mensagem);
    public static Erro NaoEncontrado(string mensagem) => new(TipoDeErro.NaoEncontrado, mensagem);
    public static Erro Conflito(string mensagem) => new(TipoDeErro.Conflito, mensagem);
    public static Erro Indisponivel(string mensagem) => new(TipoDeErro.Indisponivel, mensagem);
}

/// <summary>
/// Desfecho de um caso de uso: sucesso ou um <see cref="Erro"/> previsto pela regra de negócio.
/// Falhas inesperadas (banco fora do ar, defeito) continuam sendo exceções, tratadas pelo tratador global da API.
/// </summary>
public class Resultado
{
    protected Resultado(Erro? erro) => Erro = erro;

    public Erro? Erro { get; }
    public bool Sucesso => Erro is null;

    public static Resultado Ok() => new(null);

    public static implicit operator Resultado(Erro erro) => new(erro);
}

/// <summary>Desfecho de um caso de uso que devolve um valor quando dá certo.</summary>
public sealed class Resultado<T> : Resultado
{
    private readonly T? _valor;

    private Resultado(T valor) : base(null) => _valor = valor;
    private Resultado(Erro erro) : base(erro) { }

    public T Valor => Sucesso ? _valor! : throw new InvalidOperationException($"Resultado com erro ({Erro!.Tipo}) não tem valor.");

    public static Resultado<T> Ok(T valor) => new(valor);

    public static implicit operator Resultado<T>(T valor) => new(valor);
    public static implicit operator Resultado<T>(Erro erro) => new(erro);
}
