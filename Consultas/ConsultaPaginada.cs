using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Vitalis.Consultas;

/// <summary>
/// Paginação e ordenação, lidas da query string: <c>?pagina=2&amp;tamanho=20&amp;ordenarPor=cidade&amp;direcao=desc</c>.
/// Valores fora da faixa viram 400 automaticamente (<c>[ApiController]</c>).
/// </summary>
public class ConsultaPaginada
{
    public const int TamanhoPadrao = 10;
    public const int TamanhoMaximo = 100;
    public const int PaginaMaxima = 100_000;

    /// <summary>Número da página, a partir de 1.</summary>
    [Range(1, PaginaMaxima, ErrorMessage = "A página vai de {1} a {2}.")]
    [FromQuery(Name = "pagina")]
    public int Pagina { get; set; } = 1;

    /// <summary>Itens por página (1 a 100).</summary>
    [Range(1, TamanhoMaximo, ErrorMessage = "O tamanho da página vai de {1} a {2}.")]
    [FromQuery(Name = "tamanho")]
    public int Tamanho { get; set; } = TamanhoPadrao;

    /// <summary>Campo de ordenação. Cada recurso aceita uma lista própria de campos.</summary>
    [FromQuery(Name = "ordenarPor")]
    public string? OrdenarPor { get; set; }

    /// <summary><c>asc</c> ou <c>desc</c>. Sem valor, vale a direção padrão do recurso.</summary>
    [RegularExpression("^(?i)(asc|desc)$", ErrorMessage = "Use 'asc' ou 'desc'.")]
    [FromQuery(Name = "direcao")]
    public string? Direcao { get; set; }

    /// <summary>Quantos itens pular até a página pedida. Calculado, não vem da query.</summary>
    [BindNever]
    public int Ignorar => (Pagina - 1) * Tamanho;

    public bool Decrescente(bool padrao)
        => Direcao is null ? padrao : Direcao.Equals("desc", StringComparison.OrdinalIgnoreCase);

    /// <summary>Campo de ordenação normalizado, ou o padrão do recurso quando nenhum foi pedido.</summary>
    public string CampoDeOrdenacao(string padrao)
        => string.IsNullOrWhiteSpace(OrdenarPor) ? padrao : OrdenarPor.Trim();

    public bool OrdenacaoValida(IReadOnlyCollection<string> camposAceitos)
        => string.IsNullOrWhiteSpace(OrdenarPor)
           || camposAceitos.Contains(OrdenarPor.Trim(), StringComparer.OrdinalIgnoreCase);
}
