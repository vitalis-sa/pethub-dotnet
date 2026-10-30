using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Vitalis.Tests.Unit.Aplicacao;

public class ProcessadorDeLembreteTests
{
    private readonly Mock<IResponsavelRepository> _responsaveis = new();
    private readonly Mock<ILembreteRepository> _lembretes = new();
    private readonly Mock<IHistoricoDeMensagens> _historico = new();
    private readonly Mock<IRelogio> _relogio = new();
    private readonly ProcessadorDeLembrete _processador;

    private static readonly DateTime Agora = new(2026, 11, 1, 9, 0, 0);

    public ProcessadorDeLembreteTests()
    {
        _responsaveis.Setup(r => r.Existe(7)).Returns(true);
        _historico.SetupGet(h => h.Habilitado).Returns(true);
        _relogio.Setup(r => r.Agora()).Returns(Agora);
        _processador = new ProcessadorDeLembrete(
            _responsaveis.Object, _lembretes.Object, _historico.Object, _relogio.Object,
            NullLogger<ProcessadorDeLembrete>.Instance);
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
        _lembretes.Verify(l => l.Adicionar(It.Is<Lembrete>(x =>
            x.ResponsavelId == 7 && x.PetId == 5 && x.Tipo == TipoLembrete.CONSULTA
            && x.DataAgendada == new DateOnly(2026, 11, 1) && x.ReferenciaId == 100 && x.ReferenciaTipo == "Consulta"
            && x.Status == StatusLembrete.PENDENTE && x.CreatedAt == Agora)),
            Times.Once);
        _lembretes.Verify(l => l.SalvarAlteracoes(), Times.Once);
    }

    [Fact]
    public void Processar_MensagemDeConsultaJaExistente_DeveReagendarSemCriarOutroLembrete()
    {
        // Arrange
        var existente = new Lembrete
        {
            Id = 3, ResponsavelId = 7, Tipo = TipoLembrete.CONSULTA, DataAgendada = new DateOnly(2026, 10, 20),
            Mensagem = "Consulta antiga", Status = StatusLembrete.ENVIADO, ReferenciaId = 100, ReferenciaTipo = "Consulta"
        };
        _lembretes.Setup(l => l.ObterPorReferencia(7, "Consulta", 100)).Returns(existente);

        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        existente.DataAgendada.Should().Be(new DateOnly(2026, 11, 1));
        existente.Mensagem.Should().Be("Consulta agendada para Rex");
        existente.Status.Should().Be(StatusLembrete.PENDENTE);
        _lembretes.Verify(l => l.SalvarAlteracoes(), Times.Once);
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Never);
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
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Never);
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
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Never);
    }

    [Fact]
    public void Processar_BancoIndisponivel_DeveDevolverParaAFila()
    {
        // Arrange
        _lembretes.Setup(l => l.Adicionar(It.IsAny<Lembrete>())).Throws(new InvalidOperationException("banco fora"));

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
        _lembretes.Verify(l => l.ObterPorReferencia(It.IsAny<long>(), It.IsAny<string>(), It.IsAny<long>()), Times.Never);
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Once);
    }

    [Fact]
    public void Processar_TratamentoComFrequenciaEDuracao_DeveCriarUmLembretePorDose()
    {
        // Arrange
        const string tratamento = """
            {"responsavelId":7,"petId":5,"tipo":"MEDICAMENTO","dataAgendada":"2026-11-02","mensagem":"Dar Prednisolona ao Rex",
             "referenciaId":30,"referenciaTipo":"VacinaTratamento","vezesPorDia":2,"duracaoDias":3,"inicio":"2026-11-02T07:00:00"}
            """;
        // Doses de uma versão anterior da mesma receita, ainda não liberadas: são trocadas pelas novas.
        List<Lembrete> antigas = [new() { Id = 90, ResponsavelId = 7, DataHora = new DateTime(2026, 11, 3, 10, 0, 0) }];
        _lembretes.Setup(l => l.ListarDosesPendentes(7, "VacinaTratamento", 30)).Returns(antigas);
        List<Lembrete>? gravados = null;
        _lembretes.Setup(l => l.AdicionarVarios(It.IsAny<IEnumerable<Lembrete>>()))
            .Callback<IEnumerable<Lembrete>>(doses => gravados = doses.ToList());

        // Act
        var resultado = _processador.Processar(Corpo(tratamento));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        gravados.Should().NotBeNull().And.HaveCount(6);
        gravados!.Select(d => d.DataHora!.Value.Hour).Should().Equal(10, 22, 10, 22, 10, 22);
        gravados!.First().Mensagem.Should().Be("Dar Prednisolona ao Rex (dose 1 de 6)");
        gravados!.Should().OnlyContain(d => d.Status == StatusLembrete.PENDENTE && d.CreatedAt == Agora);
        _lembretes.Verify(l => l.RemoverVarios(antigas), Times.Once);
        _lembretes.Verify(l => l.SalvarAlteracoes(), Times.Once);
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Never);
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
        _lembretes.Verify(l => l.AdicionarVarios(It.IsAny<IEnumerable<Lembrete>>()), Times.Never);
    }

    [Fact]
    public void Processar_CancelamentoDeTratamento_DeveApagarAsDosesPendentesSemCriarNada()
    {
        const string cancelamento = """
            {"responsavelId":7,"petId":5,"tipo":"MEDICAMENTO","dataAgendada":"2026-11-02","mensagem":"Tratamento removido",
             "referenciaId":30,"referenciaTipo":"VacinaTratamento","cancelar":true}
            """;

        List<Lembrete> pendentes = [new() { Id = 1, ResponsavelId = 7 }, new() { Id = 2, ResponsavelId = 7 }];
        _lembretes.Setup(l => l.ListarDosesPendentes(7, "VacinaTratamento", 30)).Returns(pendentes);

        var resultado = _processador.Processar(Corpo(cancelamento));

        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        _lembretes.Verify(l => l.RemoverVarios(pendentes), Times.Once);
        _lembretes.Verify(l => l.SalvarAlteracoes(), Times.Once);
        _historico.Verify(h => h.Registrar(It.Is<RegistroDeMensagem>(r => r.Motivo == "2 dose(s) cancelada(s)")), Times.Once);
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Never);
    }

    [Fact]
    public void Processar_MensagemValida_DeveRegistrarNoHistoricoOResultadoEOMotivo()
    {
        // Act
        _processador.Processar(Corpo(MensagemValida));

        // Assert
        _historico.Verify(h => h.Registrar(It.Is<RegistroDeMensagem>(r =>
            r.ResponsavelId == 7 && r.PetId == 5 && r.Tipo == "CONSULTA" && r.ReferenciaId == 100
            && r.Resultado == "Confirmar" && r.Motivo == "lembrete criado" && r.Corpo.Contains("Rex"))), Times.Once);
    }

    [Fact]
    public void Processar_MensagemIlegivel_DeveRegistrarODescarteMesmoSemResponsavel()
    {
        // Act
        var resultado = _processador.Processar(Corpo("isto não é json"));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Descartar);
        _historico.Verify(h => h.Registrar(It.Is<RegistroDeMensagem>(r =>
            r.ResponsavelId == null && r.Resultado == "Descartar" && r.Motivo == "mensagem inválida"
            && r.Corpo == "isto não é json")), Times.Once);
    }

    [Fact]
    public void Processar_BancoForaDoAr_DeveRegistrarARepeticao()
    {
        // Arrange
        _lembretes.Setup(l => l.Adicionar(It.IsAny<Lembrete>())).Throws(new InvalidOperationException("ORA-12541"));

        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Repetir);
        _historico.Verify(h => h.Registrar(It.Is<RegistroDeMensagem>(r =>
            r.Resultado == "Repetir" && r.Motivo.Contains("InvalidOperationException"))), Times.Once);
    }

    [Fact]
    public void Processar_HistoricoForaDoAr_DeveConfirmarAMensagemMesmoAssim()
    {
        // Arrange
        _historico.Setup(h => h.Registrar(It.IsAny<RegistroDeMensagem>())).Throws(new TimeoutException("mongo"));

        // Act
        var resultado = _processador.Processar(Corpo(MensagemValida));

        // Assert
        resultado.Should().Be(ResultadoDaMensagem.Confirmar);
        _lembretes.Verify(l => l.Adicionar(It.IsAny<Lembrete>()), Times.Once);
    }

    [Fact]
    public void Processar_HistoricoDesligado_NaoDeveTentarRegistrar()
    {
        // Arrange
        _historico.SetupGet(h => h.Habilitado).Returns(false);

        // Act
        _processador.Processar(Corpo(MensagemValida));

        // Assert
        _historico.Verify(h => h.Registrar(It.IsAny<RegistroDeMensagem>()), Times.Never);
    }
}
