using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vitalis.Configuracao;

namespace Vitalis.Seguranca;

/// <summary>
/// Autenticação entre o Vitalis e o pethub-java por JWT (HS256) assinado com um segredo que só os dois
/// backends conhecem, independente do segredo do JWT do usuário.
/// </summary>
public interface IAutenticadorDeServico
{
    /// <summary>Token curto para uma chamada ao Java em nome do responsável (<c>sub</c>).</summary>
    string EmitirParaJava(long responsavelId);

    /// <summary>A requisição traz <c>X-Service-Authorization</c> assinado pelo Java, ainda válido?</summary>
    bool TokenDoJavaValido(HttpRequest request);
}

/// <summary>
/// Saída (Vitalis → Java): HS256 de 60 s, emissor <c>vitalis</c>, público <c>pethub-java</c>.
/// Entrada (Java → Vitalis): só passa token HS256 com o mesmo segredo, emissor <c>pethub-java</c>,
/// público <c>vitalis</c>, expiração e vida de no máximo 5 minutos.
/// Sem segredo configurado nada é aceito nem emitido.
/// </summary>
public sealed class AutenticadorDeServico : IAutenticadorDeServico
{
    public const string Cabecalho = "X-Service-Authorization";
    public const string EmissorVitalis = "vitalis";
    public const string EmissorJava = "pethub-java";

    private const string Prefixo = "Bearer ";
    private static readonly TimeSpan Validade = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan VidaMaximaAceita = TimeSpan.FromMinutes(5);

    private readonly SymmetricSecurityKey? _chave;
    private readonly TokenValidationParameters? _validacao;
    private readonly JsonWebTokenHandler _handler = new();

    public AutenticadorDeServico(IOptions<ServicoOpcoes> opcoes)
    {
        var segredo = opcoes.Value.Segredo;
        if (string.IsNullOrWhiteSpace(segredo))
            return;

        if (!ChaveDeAssinatura.SegredoValido(segredo))
            throw new InvalidOperationException(
                $"ServicoAuth:Segredo precisa ter ao menos {ChaveDeAssinatura.TamanhoMinimoEmBytes} bytes.");

        _chave = ChaveDeAssinatura.Criar(segredo);
        _validacao = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = EmissorJava,
            ValidateAudience = true,
            ValidAudience = EmissorVitalis,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = _chave,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    public string EmitirParaJava(long responsavelId)
    {
        if (_chave is null)
            throw new Integracao.ServicoIndisponivelException(
                "Segredo de serviço não configurado (ServicoAuth:Segredo).");

        var agora = DateTime.UtcNow;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = EmissorVitalis,
            Audience = EmissorJava,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", responsavelId.ToString()),
                new Claim("jti", Guid.NewGuid().ToString())
            ]),
            IssuedAt = agora,
            NotBefore = agora,
            Expires = agora.Add(Validade),
            SigningCredentials = new SigningCredentials(_chave, SecurityAlgorithms.HmacSha256)
        });
    }

    public bool TokenDoJavaValido(HttpRequest request)
    {
        if (_validacao is null)
            return false;

        var cabecalho = request.Headers[Cabecalho].ToString();
        if (!cabecalho.StartsWith(Prefixo, StringComparison.OrdinalIgnoreCase))
            return false;

        // Com chave local a validação termina de forma síncrona; não há I/O por trás do await.
        var resultado = _handler.ValidateTokenAsync(cabecalho[Prefixo.Length..].Trim(), _validacao)
            .GetAwaiter().GetResult();

        return resultado.IsValid
               && resultado.SecurityToken is JsonWebToken jwt
               && jwt.ValidTo - jwt.IssuedAt <= VidaMaximaAceita;
    }
}
