using System.ComponentModel.DataAnnotations;

namespace Vitalis.Domain.Entidades;

public class ResponsavelEndereco : IItemDoResponsavel
{
    public long Id { get; set; }

    public long ResponsavelId { get; set; }
    public Responsavel? Responsavel { get; set; }

    [Required] public string Logradouro { get; set; } = null!;
    [Required] public string Numero { get; set; } = null!;
    public string? Complemento { get; set; }
    [Required] public string Bairro { get; set; } = null!;
    [Required] public string Cidade { get; set; } = null!;

    [Required, StringLength(2)]
    public string Estado { get; set; } = null!;

    [Required, StringLength(8)]
    public string Cep { get; set; } = null!;

    public bool Principal { get; set; }
}
