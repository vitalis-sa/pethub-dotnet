using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vitalis.Configuracao;

namespace Vitalis.Seguranca;

public interface IEmissorDeToken
{
    string Emitir(Responsavel responsavel);
}

public class EmissorDeToken(IOptions<JwtOpcoes> opcoes) : IEmissorDeToken
{
    public string Emitir(Responsavel responsavel)
    {
        var jwt = opcoes.Value;
        var descritor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", responsavel.Id.ToString()),
                new Claim("name", responsavel.Nome),
                new Claim("email", responsavel.Email)
            ]),
            Issuer = jwt.Emissor,
            Audience = jwt.Audiencia,
            Expires = DateTime.UtcNow.AddMinutes(jwt.ExpiracaoMinutos),
            SigningCredentials = new SigningCredentials(ChaveDeAssinatura.Criar(jwt.Segredo), SecurityAlgorithms.HmacSha256)
        };
        return new JsonWebTokenHandler().CreateToken(descritor);
    }
}

public static class ChaveDeAssinatura
{
    public const int TamanhoMinimoEmBytes = 32;

    public static bool SegredoValido(string? segredo)
        => !string.IsNullOrWhiteSpace(segredo) && Encoding.UTF8.GetByteCount(segredo) >= TamanhoMinimoEmBytes;

    public static SymmetricSecurityKey Criar(string? segredo)
        => SegredoValido(segredo)
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(segredo!))
            : throw new InvalidOperationException($"Jwt:Segredo precisa ter ao menos {TamanhoMinimoEmBytes} bytes.");
}
