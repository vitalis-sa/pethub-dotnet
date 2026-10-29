using System.ComponentModel.DataAnnotations;

/// <summary>Atualização de dados cadastrais; a senha não muda por aqui.</summary>
public class AtualizarResponsavelDto
{
    [Required] public string Nome  { get; set; } = null!;
    [Required] public string Cpf   { get; set; } = null!;
    [Required][EmailAddress] public string Email { get; set; } = null!;
}
