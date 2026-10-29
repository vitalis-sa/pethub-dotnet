using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Vitalis.Configuracao;
using Vitalis.Seguranca;
using Xunit;

namespace Vitalis.Tests.Unit.Seguranca;

public class EmissorDeTokenTests
{
    private const string Segredo = "segredo-de-teste-com-mais-de-32-bytes!!";

    private static EmissorDeToken CriarEmissor(string segredo = Segredo)
        => new(Options.Create(new JwtOpcoes { Segredo = segredo, Emissor = "vitalis-teste", ExpiracaoMinutos = 30 }));

    [Fact]
    public void Emitir_ResponsavelValido_DeveGerarTokenComOIdNoSub()
    {
        // Arrange
        var responsavel = new Responsavel { Id = 42, Nome = "Ana", Email = "ana@pethub.com", Cpf = "1", Senha = "x" };

        // Act
        var token = new JsonWebTokenHandler().ReadJsonWebToken(CriarEmissor().Emitir(responsavel));

        // Assert
        token.Subject.Should().Be("42");
        token.Issuer.Should().Be("vitalis-teste");
        token.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public void Emitir_TokenGerado_NaoDeveConterASenha()
    {
        // Arrange
        var responsavel = new Responsavel { Id = 1, Nome = "Ana", Email = "ana@pethub.com", Cpf = "1", Senha = "SenhaSecreta" };

        // Act
        var token = new JsonWebTokenHandler().ReadJsonWebToken(CriarEmissor().Emitir(responsavel));

        // Assert
        token.Claims.Should().NotContain(c => c.Value.Contains("SenhaSecreta"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("curto")]
    public void Emitir_SegredoInvalido_DeveFalharEmVezDeAssinarComChaveFraca(string segredo)
    {
        // Arrange
        var responsavel = new Responsavel { Id = 1, Nome = "Ana", Email = "ana@pethub.com", Cpf = "1", Senha = "x" };

        // Act
        var acao = () => CriarEmissor(segredo).Emitir(responsavel);

        // Assert
        acao.Should().Throw<InvalidOperationException>();
    }
}
