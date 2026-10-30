namespace Vitalis.Doses;

/// <summary>
/// Calcula os horários das doses de um medicamento, no horário local do negócio (America/Sao_Paulo).
///
/// Regras: 1x ao dia = 10h; 2x ao dia = 10h e 22h (a cada 12h); 3x ou mais = primeira dose às 8h e as
/// demais a cada 24h/x. O tratamento tem dias x vezes doses no total; a primeira dose é a primeira do
/// padrão que ainda não passou quando a receita foi salva.
/// </summary>
public static class AgendaDeDoses
{
    public const int MaximoDeDosesPorDia = 24;
    public const int MaximoDeDias = 365;

    private static readonly TimeSpan PrimeiraDoseDeUmaVez = TimeSpan.FromHours(10);
    private static readonly TimeSpan PrimeiraDoseDeDuas = TimeSpan.FromHours(10);
    private static readonly TimeSpan PrimeiraDoseDeTresOuMais = TimeSpan.FromHours(8);

    public static bool Valida(int vezesPorDia, int duracaoDias)
        => vezesPorDia is >= 1 and <= MaximoDeDosesPorDia && duracaoDias is >= 1 and <= MaximoDeDias;

    /// <param name="inicio">Momento em que a receita foi salva (horário local).</param>
    /// <param name="vezesPorDia">Doses por dia (1 a <see cref="MaximoDeDosesPorDia"/>).</param>
    /// <param name="duracaoDias">Dias de tratamento (1 a <see cref="MaximoDeDias"/>).</param>
    public static IReadOnlyList<DateTime> Calcular(DateTime inicio, int vezesPorDia, int duracaoDias)
    {
        if (!Valida(vezesPorDia, duracaoDias))
            throw new ArgumentOutOfRangeException(nameof(vezesPorDia), "Frequência ou duração fora do limite aceito.");

        var intervalo = TimeSpan.FromHours(24.0 / vezesPorDia);
        var primeira = vezesPorDia switch
        {
            1 => PrimeiraDoseDeUmaVez,
            2 => PrimeiraDoseDeDuas,
            _ => PrimeiraDoseDeTresOuMais
        };

        var total = vezesPorDia * duracaoDias;
        var doses = new List<DateTime>(total);
        var dia = inicio.Date;

        // O padrão se repete todo dia; descartam-se só os horários que já passaram no primeiro dia.
        while (doses.Count < total)
        {
            for (var k = 0; k < vezesPorDia && doses.Count < total; k++)
            {
                var horario = dia + primeira + k * intervalo;
                if (horario >= inicio)
                    doses.Add(Arredondar(horario));
            }
            dia = dia.AddDays(1);
        }

        return doses;
    }

    /// <summary>Intervalos como 24/7 h não são exatos: arredonda ao minuto.</summary>
    private static DateTime Arredondar(DateTime horario)
        => new(horario.Year, horario.Month, horario.Day, horario.Hour, horario.Minute, 0, horario.Kind);
}
