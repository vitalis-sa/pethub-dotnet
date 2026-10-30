using Vitalis.Application.Abstracoes;

namespace Vitalis.Infrastructure.Seguranca;

/// <summary>Hash de senha com BCrypt (salt aleatório e custo embutidos no próprio hash).</summary>
public sealed class HasherBCrypt : IHasherDeSenha
{
    public string Gerar(string senha) => BCrypt.Net.BCrypt.HashPassword(senha);

    public bool Verificar(string senha, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(senha, hash);
        }
        catch (BCrypt.Net.SaltParseException)
        {
            // Valor gravado que não é um hash BCrypt: nunca confere.
            return false;
        }
    }
}
