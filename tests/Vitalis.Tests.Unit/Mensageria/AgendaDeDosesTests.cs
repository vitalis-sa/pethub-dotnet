using FluentAssertions;
using Vitalis.Doses;
using Xunit;

namespace Vitalis.Tests.Unit.Mensageria;

public class AgendaDeDosesTests
{
    private static readonly DateTime SegundaDeManha = new(2026, 11, 2, 7, 0, 0);

    [Fact]
    public void Calcular_UmaVezAoDiaPorSeteDias_DeveGerarSeteDosesAs10h()
    {
        var doses = AgendaDeDoses.Calcular(SegundaDeManha, 1, 7);

        doses.Should().HaveCount(7);
        doses.Should().OnlyContain(d => d.Hour == 10 && d.Minute == 0);
        doses.Select(d => d.Date).Should().BeEquivalentTo(Enumerable.Range(0, 7).Select(i => SegundaDeManha.Date.AddDays(i)));
    }

    [Fact]
    public void Calcular_DuasVezesAoDia_DeveUsar10hE22h()
    {
        var doses = AgendaDeDoses.Calcular(SegundaDeManha, 2, 3);

        doses.Select(d => d.Hour).Should().Equal(10, 22, 10, 22, 10, 22);
    }

    [Fact]
    public void Calcular_TresVezesAoDia_DeveComecarAs8hEEspacarEm8h()
    {
        var doses = AgendaDeDoses.Calcular(new DateTime(2026, 11, 2, 6, 0, 0), 3, 1);

        doses.Should().Equal(
            new DateTime(2026, 11, 2, 8, 0, 0),
            new DateTime(2026, 11, 2, 16, 0, 0),
            new DateTime(2026, 11, 3, 0, 0, 0));
    }

    [Fact]
    public void Calcular_QuatroVezesAoDia_DeveEspacarEm6h()
    {
        var doses = AgendaDeDoses.Calcular(new DateTime(2026, 11, 2, 6, 0, 0), 4, 1);

        doses.Select(d => d.Hour).Should().Equal(8, 14, 20, 2);
    }

    [Fact]
    public void Calcular_ReceitaSalvaDepoisDoPrimeiroHorario_DevePularOQueJaPassouEManterOTotal()
    {
        // 15h: a dose das 10h de hoje já passou, então a primeira é amanhã às 10h e o total continua 7.
        var doses = AgendaDeDoses.Calcular(new DateTime(2026, 11, 2, 15, 0, 0), 1, 7);

        doses.Should().HaveCount(7);
        doses.First().Should().Be(new DateTime(2026, 11, 3, 10, 0, 0));
        doses.Last().Should().Be(new DateTime(2026, 11, 9, 10, 0, 0));
    }

    [Fact]
    public void Calcular_DuasVezesComReceitaAs15h_DeveComecarAs22hDeHoje()
    {
        var doses = AgendaDeDoses.Calcular(new DateTime(2026, 11, 2, 15, 0, 0), 2, 1);

        doses.Should().Equal(new DateTime(2026, 11, 2, 22, 0, 0), new DateTime(2026, 11, 3, 10, 0, 0));
    }

    [Fact]
    public void Calcular_CincoVezes_DeveArredondarAoMinuto()
    {
        var doses = AgendaDeDoses.Calcular(new DateTime(2026, 11, 2, 6, 0, 0), 5, 1);

        doses.Should().HaveCount(5);
        doses.Should().OnlyContain(d => d.Second == 0);
        doses[1].Should().Be(new DateTime(2026, 11, 2, 12, 48, 0));
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(25, 5)]
    [InlineData(2, 0)]
    [InlineData(2, 366)]
    public void Calcular_ValoresForaDoLimite_DeveRecusar(int vezes, int dias)
    {
        AgendaDeDoses.Valida(vezes, dias).Should().BeFalse();
        var acao = () => AgendaDeDoses.Calcular(SegundaDeManha, vezes, dias);
        acao.Should().Throw<ArgumentOutOfRangeException>();
    }
}
