using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Vitalis.Mensageria;
using Vitalis.Models;
using Vitalis.Repositories;
using Xunit;

namespace Vitalis.Tests.Unit.Mensageria;

public class ProcessadorDeLembreteTests
{
    private readonly Mock<IResponsavelRepository> _responsaveis = new();
    private readonly Mock<ILembreteRepository> _lembretes = new();
    private readonly ProcessadorDeLembrete _processador;

    public ProcessadorDeLembreteTests()
    {
        _responsaveis.Setup(r => r.Existe(7)).Returns(true);
        _processador = new ProcessadorDeLembrete(
            _responsaveis.Object, _lembretes.Object, NullLogger<ProcessadorDeLembrete>.Instance);
    }

    private static byte[] Corpo(string json) => Encoding.UTF8.GetBytes(json);

    private const string MensagemValida = """
        {"responsavelId":7,"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01",
         "mensagem":"Consulta agendada para Rex","referenciaId":100,"referenciaTipo":"Consulta"}
        """;

    [Fact]
    public void Processar_MensagemValida_DeveGravarOLembreteEConfirmar()
    {
        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        _lembretes.Verify(l => l.Add(It.Is<Lembrete>(x =>
            x.ResponsavelId == 7 && x.PetId == 5 && x.Tipo == TipoLembrete.CONSULTA
            && x.DataAgendada == new DateOnly(2026, 11, 1) && x.ReferenciaId == 100 && x.ReferenciaTipo == "Consulta")),
            Times.Once);
    }

    [Fact]
    public void Processar_MensagemDeConsultaJaExistente_DeveAtualizarSemCriarOutroLembrete()
    {
        // Arrange
        _lembretes.Setup(l => l.AtualizarPorReferencia(7, "Consulta", 100, It.IsAny<DateOnly>(), It.IsAny<string>()))
            .Returns(true);

        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        _lembretes.Verify(l => l.AtualizarPorReferencia(7, "Consulta", 100, new DateOnly(2026, 11, 1), It.IsAny<string>()),
            Times.Once);
        _lembretes.Verify(l => l.Add(It.IsAny<Lembrete>()), Times.Never);
    }

    [Theory]
    [InlineData("{ isso não é json }")]
    [InlineData("""{"responsavelId":7,"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01","mensagem":" "}""")]
    [InlineData("""{"responsavelId":7,"petId":5,"tipo":"BANHO","dataAgendada":"2026-11-01","mensagem":"x"}""")]
    [InlineData("""{"responsavelId":0,"petId":5,"tipo":"CONSULTA","dataAgendada":"2026-11-01","mensagem":"x"}""")]
    public void Processar_MensagemInvalida_DeveDescartarSemGravar(string json)
    {
        // Act
        var resultado = _processador.Processar(Corpo(json));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Descartar);
        _lembretes.Verify(l => l.Add(It.IsAny<Lembrete>()), Times.Never);
    }

    [Fact]
    public void Processar_ResponsavelInexistente_DeveDescartar()
    {
        // Arrange
        _responsaveis.Setup(r => r.Existe(7)).Returns(false);

        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Descartar);
        _lembretes.Verify(l => l.Add(It.IsAny<Lembrete>()), Times.Never);
    }

    [Fact]
    public void Processar_BancoIndisponivel_DeveDevolverParaAFila()
    {
        // Arrange
        _lembretes.Setup(l => l.Add(It.IsAny<Lembrete>())).Throws(new InvalidOperationException("banco fora"));

        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Repetir);
    }

    [Fact]
    public void Processar_MensagemSemReferencia_DeveGravarSemChecarDuplicidade()
    {
        // Arrange
        const string semReferencia = """
            {"responsavelId":7,"petId":5,"tipo":"VACINA","dataAgendada":"2026-11-01","mensagem":"Vacina"}
            """;

        // Act
        var resultado = _processador.Processar(Corpo(semReferencia));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        _lembretes.Verify(l => l.AtualizarPorReferencia(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>(), It.IsAny<DateOnly>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Processar_TratamentoComFrequenciaEDuracao_DeveCriarUmLembretePorDose()
    {
        // Arrange
        const string tratamento = """
            {"responsavelId":7,"petId":5,"tipo":"MEDICAMENTO","dataAgendada":"2026-11-02","mensagem":"Dar Prednisolona ao Rex",
             "referenciaId":30,"referenciaTipo":"VacinaTratamento","vezesPorDia":2,"duracaoDias":3,"inicio":"2026-11-02T07:00:00"}
            """;
        IReadOnlyList<Lembrete>? gravados = null;
        _lembretes.Setup(l => l.SubstituirDoses(7, "VacinaTratamento", 30, It.IsAny<IReadOnlyList<Lembrete>>()))
            .Callback<long, string, long?, IReadOnlyList<Lembrete>>((_, _, _, doses) => gravados = doses);

        // Act
        var resultado = _processador.Processar(Corpo(tratamento));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        gravados.Should().NotBeNull().And.HaveCount(6);
        gravados!.Select(d => d.DataHora!.Value.Hour).Should().Equal(10, 22, 10, 22, 10, 22);
        gravados!.First().Mensagem.Should().Be("Dar Prednisolona ao Rex (dose 1 de 6)");
        _lembretes.Verify(l => l.Add(It.IsAny<Lembrete>()), Times.Never);
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(2, 0)]
    [InlineData(30, 3)]
    public void Processar_TratamentoComFrequenciaOuDuracaoInvalida_DeveDescartar(int vezes, int dias)
    {
        var json = $$"""
            {"responsavelId":7,"petId":5,"tipo":"MEDICAMENTO","dataAgendada":"2026-11-02","mensagem":"x",
             "referenciaId":30,"referenciaTipo":"VacinaTratamento","vezesPorDia":{{vezes}},"duracaoDias":{{dias}}}
            """;

        _processador.Processar(Corpo(json)).Should().Be(ResultadoDaMensagem.Descartar);
        _lembretes.Verify(l => l.SubstituirDoses(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long?>(), It.IsAny<IReadOnlyList<Lembrete>>()), Times.Never);
    }

    [Fact]
    public void Processar_CancelamentoDeTratamento_DeveApagarAsDosesPendentesSemCriarNada()
    {
        const string cancelamento = """
            {"responsavelId":7,"petId":5,"tipo":"MEDICAMENTO","dataAgendada":"2026-11-02","mensagem":"Tratamento removido",
             "referenciaId":30,"referenciaTipo":"VacinaTratamento","cancelar":true}
            """;

        var resultado = _processador.Processar(Corpo(cancelamento));

        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        _lembretes.Verify(l => l.RemoverDosesPendentes(7, "VacinaTratamento", 30), Times.Once);
        _lembretes.Verify(l => l.Add(It.IsAny<Lembrete>()), Times.Never);
    }
}
