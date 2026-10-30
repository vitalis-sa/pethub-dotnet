using System.ComponentModel.DataAnnotations;
using Vitalis.Domain.Entidades;

namespace Vitalis.Application.Dtos;

/// <summary>Corpo de criação e atualização de contato. O id e o responsável vêm da rota, nunca do corpo.</summary>
public class ContatoDto
{
    /// <summary><c>CELULAR</c> ou <c>TELEFONE</c>.</summary>
    [Required] public string Tipo { get; set; } = null!;

    [Required] public string Telefone { get; set; } = null!;

    /// <summary>Marca como principal (os demais são desmarcados). O primeiro contato sempre vira principal.</summary>
    public bool Principal { get; set; }

    public ResponsavelContato ParaEntidade(long responsavelId)
    {
        var contato = new ResponsavelContato { ResponsavelId = responsavelId };
        AplicarEm(contato);
        return contato;
    }

    public void AplicarEm(ResponsavelContato contato)
    {
        contato.Tipo = Tipo;
        contato.Telefone = Telefone;
        contato.Principal = Principal;
    }
}
