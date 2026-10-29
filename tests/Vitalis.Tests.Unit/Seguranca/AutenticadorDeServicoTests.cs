using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Vitalis.Configuracao;
using Vitalis.Integracao;
using Vitalis.Seguranca;
using Vitalis.Tests.Unit.Fixtures;
using Xunit;

namespace Vitalis.Tests.Unit.Seguranca;

public class AutenticadorDeServicoTests
{
    private readonly AutenticadorDeServico _autenticador = ChavesDeTeste.NovoAutenticador();

    private static HttpRequest ComCabecalho(string? valor)
    {
        var http = new DefaultHttpContext();
        if (valor is not null)
            http.Request.Headers[AutenticadorDeServico.Cabecalho] = valor;
        return http.Request;
    }

    [Fact]
    public void EmitirParaJava_DeveAssinarComOSegredoComSubjectEVidaCurta()
    {
        var token = _autenticador.EmitirParaJava(7);

        var jwt = new JsonWebToken(token);
        jwt.Issuer.Should().Be("vitalis");
        jwt.Audiences.Should().ContainSingle().Which.Should().Be("pethub-java");
        jwt.Subject.Should().Be("7");
        jwt.Alg.Should().Be("HS256");
        (jwt.ValidTo - jwt.IssuedAt).Should().Be(TimeSpan.FromSeconds(60));

        var validacao = new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "vitalis",
            ValidAudience = "pethub-java",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(ChavesDeTeste.Segredo))
        }).GetAwaiter().GetResult();
        validacao.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EmitirParaJava_SemSegredo_DeveLancarServicoIndisponivel()
    {
        var semSegredo = new AutenticadorDeServico(Options.Create(new ServicoOpcoes()));

        var acao = () => semSegredo.EmitirParaJava(7);

        acao.Should().Throw<ServicoIndisponivelException>();
    }

    [Fact]
    public void TokenDoJavaValido_TokenAssinadoComOSegredo_DeveAceitar()
        => _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava())).Should().BeTrue();

    [Fact]
    public void TokenDoJavaValido_OutroSegredo_DeveRecusar()
        => _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava(segredo: ChavesDeTeste.OutroSegredo)))
            .Should().BeFalse();

    [Fact]
    public void TokenDoJavaValido_EmissorOuPublicoErrado_DeveRecusar()
    {
        _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava(emissor: "outro"))).Should().BeFalse();
        _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava(publico: "outro"))).Should().BeFalse();
    }

    [Fact]
    public void TokenDoJavaValido_NaoAceitaOTokenQueOProprioVitalisEmite()
        => _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + _autenticador.EmitirParaJava(7))).Should().BeFalse();

    [Fact]
    public void TokenDoJavaValido_Expirado_DeveRecusar()
        => _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava(
                emitidoHa: TimeSpan.FromMinutes(10), validade: TimeSpan.FromMinutes(1))))
            .Should().BeFalse();

    [Fact]
    public void TokenDoJavaValido_VidaLongaDemais_DeveRecusar()
        => _autenticador.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava(validade: TimeSpan.FromHours(1))))
            .Should().BeFalse();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Bearer ")]
    [InlineData("Bearer isso.nao.eh-jwt")]
    public void TokenDoJavaValido_AusenteOuLixo_DeveRecusar(string? cabecalho)
        => _autenticador.TokenDoJavaValido(ComCabecalho(cabecalho)).Should().BeFalse();

    [Fact]
    public void TokenDoJavaValido_SemBearer_DeveRecusar()
        => _autenticador.TokenDoJavaValido(ComCabecalho(ChavesDeTeste.TokenDoJava())).Should().BeFalse();

    [Fact]
    public void TokenDoJavaValido_SemSegredoConfigurado_NaoAceitaNada()
    {
        var semSegredo = new AutenticadorDeServico(Options.Create(new ServicoOpcoes()));

        semSegredo.TokenDoJavaValido(ComCabecalho("Bearer " + ChavesDeTeste.TokenDoJava())).Should().BeFalse();
    }

    [Fact]
    public void Construtor_SegredoCurtoDemais_DeveFalharNaSubida()
    {
        var acao = () => new AutenticadorDeServico(Options.Create(new ServicoOpcoes { Segredo = "curto" }));

        acao.Should().Throw<InvalidOperationException>();
    }
}
