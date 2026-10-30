using Vitalis.Domain.Entidades;

namespace Vitalis.Domain.Servicos;

/// <summary>
/// Regra dos endereços e contatos: entre os itens de um responsável, um é o principal.
/// O primeiro cadastrado vira principal; marcar um como principal desmarca os demais; e, se o principal for
/// removido, o mais recente dos que sobram assume.
/// </summary>
public static class RegraDoPrincipal
{
    /// <summary>Aplica a regra a um item novo, antes de ele ser gravado.</summary>
    public static void AoIncluir<T>(IReadOnlyCollection<T> existentes, T novo) where T : IPodeSerPrincipal
    {
        if (existentes.Count == 0)
            novo.Principal = true;

        if (novo.Principal)
            DesmarcarOutros(existentes, novo);
    }

    /// <summary>Um item atualizado como principal desmarca os demais.</summary>
    public static void AoAtualizar<T>(IEnumerable<T> todos, T atualizado) where T : IPodeSerPrincipal
    {
        if (atualizado.Principal)
            DesmarcarOutros(todos, atualizado);
    }

    public static void Definir<T>(IEnumerable<T> todos, T escolhido) where T : IPodeSerPrincipal
    {
        DesmarcarOutros(todos, escolhido);
        escolhido.Principal = true;
    }

    /// <summary>Se o removido era o principal, o mais recente dos que sobram assume. Devolve quem assumiu.</summary>
    public static T? AoRemover<T>(IEnumerable<T> todos, T removido) where T : class, IPodeSerPrincipal
    {
        if (!removido.Principal)
            return null;

        var substituto = todos
            .Where(item => !Mesmo(item, removido))
            .OrderByDescending(item => item.Id)
            .FirstOrDefault();

        if (substituto is not null)
            substituto.Principal = true;

        return substituto;
    }

    private static void DesmarcarOutros<T>(IEnumerable<T> todos, T mantido) where T : IPodeSerPrincipal
    {
        foreach (var item in todos.Where(item => !Mesmo(item, mantido)))
            item.Principal = false;
    }

    private static bool Mesmo<T>(T a, T b) where T : IPodeSerPrincipal
        => ReferenceEquals(a, b) || (a.Id != 0 && a.Id == b.Id);
}
