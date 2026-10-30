using Vitalis.Hateoas;

namespace Vitalis.Consultas;

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
    public static PaginaDeResultados<T> Montar<T>(
        IReadOnlyList<T> itens, long total, ConsultaPaginada consulta, HttpRequest requisicao)
    {
        var totalDePaginas = (int)Math.Ceiling(total / (double)consulta.Tamanho);
        return new PaginaDeResultados<T>(
            itens, consulta.Pagina, consulta.Tamanho, total, totalDePaginas,
            Links.DaPagina(requisicao, consulta.Pagina, consulta.Tamanho, totalDePaginas));
    }
}
