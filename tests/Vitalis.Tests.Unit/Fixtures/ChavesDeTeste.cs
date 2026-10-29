using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vitalis.Configuracao;
using Vitalis.Seguranca;

namespace Vitalis.Tests.Unit.Fixtures;

/// <summary>Segredo e tokens para os testes de autenticação entre serviços.</summary>
public static class ChavesDeTeste
{
    public const string Segredo = "segredo-de-servico-de-teste-com-mais-de-32-bytes";
    public const string OutroSegredo = "outro-segredo-de-servico-bem-diferente-do-primeiro";

    public static ServicoOpcoes OpcoesDoVitalis() => new() { Segredo = Segredo };

    public static AutenticadorDeServico NovoAutenticador() => new(Options.Create(OpcoesDoVitalis()));

    /// <summary>Token como o Java o emitiria para chamar o Vitalis.</summary>
    public static string TokenDoJava(
        string segredo = Segredo,
        string emissor = AutenticadorDeServico.EmissorJava,
        string publico = AutenticadorDeServico.EmissorVitalis,
        TimeSpan? emitidoHa = null,
        TimeSpan? validade = null)
    {
        var emitido = DateTime.UtcNow - (emitidoHa ?? TimeSpan.Zero);
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = publico,
            Subject = new ClaimsIdentity([new Claim("sub", "pethub-java"), new Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = emitido,
            NotBefore = emitido,
            Expires = emitido + (validade ?? TimeSpan.FromSeconds(60)),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo)), SecurityAlgorithms.HmacSha256)
        });
    }
}
