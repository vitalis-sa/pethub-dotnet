using FluentAssertions;
using Xunit;

namespace Vitalis.Tests.Unit.Infraestrutura;

public class HasherBCryptTests
{
    private readonly HasherBCrypt _hasher = new();

    [Fact]
    public void Gerar_MesmaSenhaDuasVezes_DeveGerarHashesDiferentesQueConferemComASenha()
    {
        // Act
        var primeiro = _hasher.Gerar("SenhaSegura@123");
        var segundo = _hasher.Gerar("SenhaSegura@123");

        // Assert
        primeiro.Should().NotBe(segundo).And.NotContain("SenhaSegura@123");
        _hasher.Verificar("SenhaSegura@123", primeiro).Should().BeTrue();
        _hasher.Verificar("SenhaSegura@123", segundo).Should().BeTrue();
    }

    [Fact]
    public void Verificar_SenhaErrada_DeveRecusar()
        => _hasher.Verificar("outra", _hasher.Gerar("SenhaSegura@123")).Should().BeFalse();

    [Fact]
    public void Verificar_ValorQueNaoEHashBCrypt_DeveRecusarSemLancarExcecao()
        => _hasher.Verificar("x", "isto-nao-e-um-hash").Should().BeFalse();
}
