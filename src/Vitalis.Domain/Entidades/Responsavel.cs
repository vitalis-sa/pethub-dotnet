using System.ComponentModel.DataAnnotations;

namespace Vitalis.Domain.Entidades;

/// <summary>Tutor dos pets: quem se cadastra no app, recebe os lembretes e é dono dos próprios dados.</summary>
public class Responsavel
{
    public long Id { get; set; }

    [Required, StringLength(150)]
    public string Nome { get; set; } = null!;

    [Required, StringLength(11)]
    public string Cpf { get; set; } = null!;

    [Required]
    public string Email { get; set; } = null!;

    /// <summary>Hash BCrypt da senha. A senha em texto nunca é guardada.</summary>
    [Required]
    public string Senha { get; set; } = null!;

    public bool Ativo { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public List<ResponsavelEndereco> Enderecos { get; set; } = [];
    public List<ResponsavelContato> Contatos { get; set; } = [];
    public List<Lembrete> Lembretes { get; set; } = [];

    /// <summary>Atualiza os dados cadastrais. A senha não muda por aqui.</summary>
    public void AtualizarCadastro(string nome, string cpf, string email)
    {
        Nome = nome;
        Cpf = cpf;
        Email = email;
    }
}
