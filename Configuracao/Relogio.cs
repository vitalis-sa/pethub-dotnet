namespace Vitalis.Configuracao;

/// <summary>
/// Horário do negócio: America/Sao_Paulo, qualquer que seja o fuso do servidor (no GCP é UTC).
/// Datas gravadas no banco (CreatedAt) usam esse relógio; o JWT continua em UTC, como manda o padrão.
/// </summary>
public static class Relogio
{
    private static readonly TimeZoneInfo Fuso = ResolverFuso();

    public static DateTime Agora() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Fuso);

    private static TimeZoneInfo ResolverFuso()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Imagem sem tzdata: o Brasil não tem horário de verão desde 2019, então UTC-3 fixo é equivalente.
            return TimeZoneInfo.CreateCustomTimeZone("America/Sao_Paulo", TimeSpan.FromHours(-3), "Brasília", "Brasília");
        }
    }
}
