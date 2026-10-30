namespace Vitalis.Domain.Entidades;

/// <summary>Item de uma lista do responsável em que um, e só um, é o principal (endereços e contatos).</summary>
public interface IPodeSerPrincipal
{
    long Id { get; }
    bool Principal { get; set; }
}

/// <summary>Item que pertence a um responsável e pode ser o principal da lista dele.</summary>
public interface IItemDoResponsavel : IPodeSerPrincipal
{
    long ResponsavelId { get; }
}
