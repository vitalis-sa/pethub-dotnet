using Vitalis.Api.Hateoas;
using Vitalis.Application.Consultas;

namespace Vitalis.Api.Respostas;

/// <summary>Envelope de toda listagem da API: os itens da página, os totais e os links de navegação.</summary>
public record PaginaDeResultados<T>(
    IReadOnlyList<T> Itens,
    int Pagina,
    int Tamanho,
    long TotalDeItens,
    int TotalDePaginas,
    IReadOnlyList<Link> Links);

public static class PaginaDeResultados
{
    /// <summary>Converte cada item com <paramref name="converter"/> e acrescenta os totais e os links da página.</summary>
    public static PaginaDeResultados<TResposta> Montar<TItem, TResposta>(
        Pagina<TItem> pagina, Func<TItem, TResposta> converter, ConsultaPaginada consulta, HttpRequest requisicao)
        => Montar(pagina.Itens.Select(converter).ToList(), pagina.Total, consulta, requisicao);

    public static PaginaDeResultados<T> Montar<T>(
        IReadOnlyList<T> itens, long total, ConsultaPaginada consulta, HttpRequest requisicao)
    {
        var totalDePaginas = (int)Math.Ceiling(total / (double)consulta.Tamanho);
        return new PaginaDeResultados<T>(
            itens, consulta.Pagina, consulta.Tamanho, total, totalDePaginas,
            Links.DaPagina(requisicao, consulta.Pagina, consulta.Tamanho, totalDePaginas));
    }
}
