using System.ComponentModel.DataAnnotations;
using Vitalis.Domain.Entidades;

namespace Vitalis.Application.Dtos;

/// <summary>Corpo de criação e atualização de endereço. O id e o responsável vêm da rota, nunca do corpo.</summary>
public class EnderecoDto
{
    [Required] public string Logradouro { get; set; } = null!;
    [Required] public string Numero { get; set; } = null!;
    public string? Complemento { get; set; }
    [Required] public string Bairro { get; set; } = null!;
    [Required] public string Cidade { get; set; } = null!;

    /// <summary>UF com duas letras.</summary>
    [Required, StringLength(2)]
    public string Estado { get; set; } = null!;

    /// <summary>Só os 8 dígitos.</summary>
    [Required, StringLength(8)]
    public string Cep { get; set; } = null!;

    /// <summary>Marca como principal (os demais são desmarcados). O primeiro endereço sempre vira principal.</summary>
    public bool Principal { get; set; }

    public ResponsavelEndereco ParaEntidade(long responsavelId)
    {
        var endereco = new ResponsavelEndereco { ResponsavelId = responsavelId };
        AplicarEm(endereco);
        return endereco;
    }

    public void AplicarEm(ResponsavelEndereco endereco)
    {
        endereco.Logradouro = Logradouro;
        endereco.Numero = Numero;
        endereco.Complemento = Complemento;
        endereco.Bairro = Bairro;
        endereco.Cidade = Cidade;
        endereco.Estado = Estado;
        endereco.Cep = Cep;
        endereco.Principal = Principal;
    }
}
