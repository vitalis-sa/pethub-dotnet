using System.ComponentModel.DataAnnotations;

namespace Vitalis.Application.Consultas;

/// <summary>
/// Paginação e ordenação, lidas da query string: <c>?pagina=2&amp;tamanho=20&amp;ordenarPor=cidade&amp;direcao=desc</c>.
/// Valores fora da faixa são recusados pela validação automática da API (400).
/// </summary>
public class ConsultaPaginada
{
    public const int TamanhoPadrao = 10;
    public const int TamanhoMaximo = 100;
    public const int PaginaMaxima = 100_000;

    /// <summary>Número da página, a partir de 1.</summary>
    [Range(1, PaginaMaxima, ErrorMessage = "A página vai de {1} a {2}.")]
    public int Pagina { get; set; } = 1;

    /// <summary>Itens por página (1 a 100).</summary>
    [Range(1, TamanhoMaximo, ErrorMessage = "O tamanho da página vai de {1} a {2}.")]
    public int Tamanho { get; set; } = TamanhoPadrao;

    /// <summary>Campo de ordenação. Cada recurso aceita uma lista própria de campos.</summary>
    public string? OrdenarPor { get; set; }

    /// <summary><c>asc</c> ou <c>desc</c>. Sem valor, vale a direção padrão do recurso.</summary>
    [RegularExpression("^(?i)(asc|desc)$", ErrorMessage = "Use 'asc' ou 'desc'.")]
    public string? Direcao { get; set; }

    /// <summary>Quantos itens pular até a página pedida.</summary>
    public int Deslocamento() => (Pagina - 1) * Tamanho;

    public bool Decrescente(bool padrao)
        => Direcao is null ? padrao : Direcao.Equals("desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Campo de ordenação normalizado, ou o padrão do recurso quando nenhum foi pedido.</summary>
    public string CampoDeOrdenacao(string padrao)
        => string.IsNullOrWhiteSpace(OrdenarPor) ? padrao : OrdenarPor.Trim();

    public bool OrdenacaoValida(IReadOnlyCollection<string> camposAceitos)
        => string.IsNullOrWhiteSpace(OrdenarPor)
           || camposAceitos.Contains(OrdenarPor.Trim(), StringComparer.OrdinalIgnoreCase);
}

/// <summary>Uma página de resultados e o total de itens antes da paginação.</summary>
public sealed record Pagina<T>(IReadOnlyList<T> Itens, long Total);
