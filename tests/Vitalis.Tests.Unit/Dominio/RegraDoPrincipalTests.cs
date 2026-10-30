using FluentAssertions;
using Xunit;

namespace Vitalis.Tests.Unit.Dominio;

public class RegraDoPrincipalTests
{
    [Fact]
    public void AoIncluir_PrimeiroItem_DeveVirarPrincipalMesmoSemPedir()
    {
        // Arrange
        var novo = Item(0, principal: false);

        // Act
        RegraDoPrincipal.AoIncluir([], novo);

        // Assert
        novo.Principal.Should().BeTrue();
    }

    [Fact]
    public void AoIncluir_NovoPrincipal_DeveDesmarcarOsDemais()
    {
        // Arrange
        var atual = Item(1, principal: true);
        var novo = Item(0, principal: true);

        // Act
        RegraDoPrincipal.AoIncluir([atual], novo);

        // Assert
        atual.Principal.Should().BeFalse();
        novo.Principal.Should().BeTrue();
    }

    [Fact]
    public void AoAtualizar_ItemMarcadoComoPrincipal_DeveDesmarcarOsOutrosMasNaoEleMesmo()
    {
        // Arrange
        var outro = Item(1, principal: true);
        var atualizado = Item(2, principal: true);

        // Act
        RegraDoPrincipal.AoAtualizar([outro, atualizado], atualizado);

        // Assert
        outro.Principal.Should().BeFalse();
        atualizado.Principal.Should().BeTrue();
    }

    [Fact]
    public void Definir_ComVariosItens_DeveDeixarSoOEscolhidoComoPrincipal()
    {
        // Arrange
        var itens = new[] { Item(1, principal: true), Item(2), Item(3) };

        // Act
        RegraDoPrincipal.Definir(itens, itens[2]);

        // Assert
        itens.Where(i => i.Principal).Should().ContainSingle().Which.Id.Should().Be(3);
    }

    [Fact]
    public void AoRemover_Principal_DeveDevolverOMaisRecenteDosQueSobramComoNovoPrincipal()
    {
        // Arrange
        var removido = Item(5, principal: true);
        var antigo = Item(2);
        var recente = Item(9);

        // Act
        var substituto = RegraDoPrincipal.AoRemover([antigo, removido, recente], removido);

        // Assert
        substituto.Should().BeSameAs(recente);
        recente.Principal.Should().BeTrue();
        antigo.Principal.Should().BeFalse();
    }

    [Fact]
    public void AoRemover_ItemQueNaoEraPrincipal_NaoDeveMexerEmNada()
    {
        // Arrange
        var principal = Item(1, principal: true);
        var removido = Item(2);

        // Act
        var substituto = RegraDoPrincipal.AoRemover([principal, removido], removido);

        // Assert
        substituto.Should().BeNull();
        principal.Principal.Should().BeTrue();
    }

    [Fact]
    public void AoRemover_UnicoItem_NaoDeveTerSubstituto()
    {
        // Arrange
        var unico = Item(1, principal: true);

        // Act
        var substituto = RegraDoPrincipal.AoRemover([unico], unico);

        // Assert
        substituto.Should().BeNull();
    }

    private static ResponsavelContato Item(long id, bool principal = false) => new()
    {
        Id = id, ResponsavelId = 1, Tipo = "CELULAR", Telefone = "1199999" + id.ToString("0000"), Principal = principal
    };
}
