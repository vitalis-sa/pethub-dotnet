using System.Linq.Expressions;

namespace Vitalis.Infrastructure.Persistencia.Repositorios;

/// <summary>Ordena por uma chave tipada em qualquer direção, sem converter a chave para object (o EF traduz direto).</summary>
internal static class Ordenacao
{
    public static IOrderedQueryable<T> Por<T, TChave>(IQueryable<T> consulta, Expression<Func<T, TChave>> chave, bool decrescente)
        => decrescente ? consulta.OrderByDescending(chave) : consulta.OrderBy(chave);
}
