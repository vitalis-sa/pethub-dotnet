using Microsoft.Extensions.Primitives;

namespace Vitalis.Hateoas;

/// <summary>
/// Monta os links de cada recurso. Ficam num lugar só para que a resposta de cada endpoint aponte para as rotas
/// que de fato existem nos controllers. Os caminhos são relativos à raiz da API.
/// </summary>
public static class Links
{
    public static IReadOnlyList<Link> DoResponsavel(long id) =>
    [
        new($"/api/responsavel/{id}", "self", "GET"),
        new($"/api/responsavel/{id}", "atualizar", "PUT"),
        new($"/api/responsavel/{id}", "excluir", "DELETE"),
        new($"/api/responsavel/{id}/enderecos", "enderecos", "GET"),
        new($"/api/responsavel/{id}/contatos", "contatos", "GET"),
        new($"/api/lembretes/responsavel/{id}", "lembretes", "GET"),
        new($"/api/lembretes/responsavel/{id}/historico", "historico", "GET")
    ];

    public static IReadOnlyList<Link> DoEndereco(long responsavelId, long id)
    {
        var self = $"/api/responsavel/{responsavelId}/enderecos/{id}";
        return
        [
            new(self, "self", "GET"),
            new(self, "atualizar", "PUT"),
            new(self, "excluir", "DELETE"),
            new($"{self}/principal", "definir-principal", "PATCH"),
            new($"/api/responsavel/{responsavelId}", "responsavel", "GET")
        ];
    }

    public static IReadOnlyList<Link> DoContato(long responsavelId, long id)
    {
        var self = $"/api/responsavel/{responsavelId}/contatos/{id}";
        return
        [
            new(self, "self", "GET"),
            new(self, "atualizar", "PUT"),
            new(self, "excluir", "DELETE"),
            new($"{self}/principal", "definir-principal", "PATCH"),
            new($"/api/responsavel/{responsavelId}", "responsavel", "GET")
        ];
    }

    public static IReadOnlyList<Link> DoLembrete(long responsavelId, long id) =>
    [
        new($"/api/lembretes/{id}", "self", "GET"),
        new($"/api/lembretes/{id}/status", "atualizar-status", "PATCH"),
        new($"/api/lembretes/{id}", "excluir", "DELETE"),
        new($"/api/responsavel/{responsavelId}", "responsavel", "GET")
    ];

    public static IReadOnlyList<Link> DoRegistroDeHistorico(long responsavelId) =>
    [
        new($"/api/lembretes/responsavel/{responsavelId}", "lembretes", "GET"),
        new($"/api/responsavel/{responsavelId}", "responsavel", "GET")
    ];

    /// <summary>
    /// Navegação da página: <c>self</c>, <c>first</c>, <c>last</c> e, quando existirem, <c>prev</c> e <c>next</c>.
    /// Os demais parâmetros da query (filtros e ordenação) são mantidos em todos os links.
    /// </summary>
    public static IReadOnlyList<Link> DaPagina(HttpRequest requisicao, int pagina, int tamanho, int totalDePaginas)
    {
        var ultima = Math.Max(totalDePaginas, 1);
        var links = new List<Link>
        {
            new(ComPagina(requisicao, pagina, tamanho), "self", "GET"),
            new(ComPagina(requisicao, 1, tamanho), "first", "GET")
        };

        if (pagina > 1)
            links.Add(new(ComPagina(requisicao, Math.Min(pagina - 1, ultima), tamanho), "prev", "GET"));
        if (pagina < totalDePaginas)
            links.Add(new(ComPagina(requisicao, pagina + 1, tamanho), "next", "GET"));

        links.Add(new(ComPagina(requisicao, ultima, tamanho), "last", "GET"));
        return links;
    }

    private static string ComPagina(HttpRequest requisicao, int pagina, int tamanho)
    {
        var parametros = new List<KeyValuePair<string, StringValues>>
        {
            new("pagina", pagina.ToString()),
            new("tamanho", tamanho.ToString())
        };
        parametros.AddRange(requisicao.Query
            .Where(p => !p.Key.Equals("pagina", StringComparison.OrdinalIgnoreCase)
                        && !p.Key.Equals("tamanho", StringComparison.OrdinalIgnoreCase)));

        return $"{requisicao.PathBase}{requisicao.Path}{QueryString.Create(parametros)}";
    }
}
