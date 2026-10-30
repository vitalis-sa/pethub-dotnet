using System.ComponentModel.DataAnnotations;

namespace Vitalis.Domain.Entidades;

public class ResponsavelContato : IItemDoResponsavel
{
    public long Id { get; set; }

    public long ResponsavelId { get; set; }
    public Responsavel? Responsavel { get; set; }

    /// <summary><c>CELULAR</c> ou <c>TELEFONE</c>.</summary>
    [Required] public string Tipo { get; set; } = null!;

    [Required] public string Telefone { get; set; } = null!;

    public bool Principal { get; set; }
}
