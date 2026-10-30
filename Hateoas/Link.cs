namespace Vitalis.Hateoas;

/// <summary>Ação disponível a partir de um recurso (HATEOAS): para onde ir, o que significa e com qual verbo.</summary>
/// <param name="Href">Caminho relativo à raiz da API.</param>
/// <param name="Rel">Relação com o recurso atual (<c>self</c>, <c>next</c>, <c>atualizar</c>...).</param>
/// <param name="Metodo">Verbo HTTP.</param>
public record Link(string Href, string Rel, string Metodo);
