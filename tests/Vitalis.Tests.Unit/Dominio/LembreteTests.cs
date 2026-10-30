using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Xunit;

namespace Vitalis.Tests.Unit.Dominio;

public class LembreteTests
{
    [Fact]
    public void NovoLembrete_QuandoInstanciado_DeveIniciarComStatusPendente()
    {
        // Arrange & Act
        var lembrete = NovoLembreteValido();

        // Assert
        lembrete.Status.Should().Be(StatusLembrete.PENDENTE);
    }

    [Fact]
    public void Validar_DadosValidos_DeveRetornarSemErros()
    {
        // Arrange
        var lembrete = NovoLembreteValido();

        // Act
        var erros = Validar(lembrete);

        // Assert
        erros.Should().BeEmpty();
    }

    [Fact]
    public void Validar_SemMensagem_DeveRetornarErroNoCampoMensagem()
    {
        // Arrange
        var lembrete = NovoLembreteValido();
        lembrete.Mensagem = null!;

        // Act
        var erros = Validar(lembrete);

        // Assert
        erros.Should().Contain(e => e.MemberNames.Contains(nameof(Lembrete.Mensagem)));
    }

    [Theory]
    [InlineData(TipoLembrete.VACINA)]
    [InlineData(TipoLembrete.CONSULTA)]
    [InlineData(TipoLembrete.EXAME)]
    [InlineData(TipoLembrete.MEDICAMENTO)]
    public void NovoLembrete_ParaCadaTipoSuportado_DevePreservarOTipoInformado(TipoLembrete tipo)
    {
        // Arrange & Act
        var lembrete = NovoLembreteValido();
        lembrete.Tipo = tipo;

        // Assert
        lembrete.Tipo.Should().Be(tipo);
    }

    [Fact]
    public void ConverterTipo_ComNomeInexistente_NaoDeveConverter()
    {
        // Arrange
        const string tipoInvalido = "BANHO";

        // Act
        var conseguiuConverter = Enum.TryParse<TipoLembrete>(tipoInvalido, false, out _);

        // Assert
        conseguiuConverter.Should().BeFalse();
    }

    [Fact]
    public void Reagendar_LembreteJaEnviado_DeveTrocarDataETextoEVoltarAFicarPendente()
    {
        // Arrange
        var lembrete = NovoLembreteValido();
        lembrete.Status = StatusLembrete.ENVIADO;

        // Act
        lembrete.Reagendar(new DateOnly(2026, 12, 1), "Consulta remarcada");

        // Assert
        lembrete.DataAgendada.Should().Be(new DateOnly(2026, 12, 1));
        lembrete.Mensagem.Should().Be("Consulta remarcada");
        lembrete.Status.Should().Be(StatusLembrete.PENDENTE);
    }

    [Fact]
    public void Liberar_DosePendente_DeveFicarEnviada()
    {
        // Arrange
        var dose = NovoLembreteValido();
        dose.DataHora = new DateTime(2026, 9, 15, 10, 0, 0);

        // Act
        dose.Liberar();

        // Assert
        dose.Status.Should().Be(StatusLembrete.ENVIADO);
        dose.EhDose.Should().BeTrue();
    }

    [Fact]
    public void EhDose_LembreteSemHorario_DeveSerFalso()
        => NovoLembreteValido().EhDose.Should().BeFalse();

    private static Lembrete NovoLembreteValido() => new()
    {
        ResponsavelId = 1,
        PetId         = 7,
        Tipo          = TipoLembrete.VACINA,
        DataAgendada  = new DateOnly(2026, 9, 15),
        Mensagem      = "Vacina antirrábica agendada"
    };

    private static List<ValidationResult> Validar(Lembrete lembrete)
    {
        var erros = new List<ValidationResult>();
        Validator.TryValidateObject(lembrete, new ValidationContext(lembrete), erros, true);
        return erros;
    }
}
