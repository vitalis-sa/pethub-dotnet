using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Vitalis.Infrastructure.Persistencia;

/// <summary>
/// Aplica as migrations do EF Core (pasta <c>Migrations/</c>) na subida da API.
/// <para>
/// As migrations só criam e alteram as tabelas do Vitalis, mais a <c>__EFMigrationsHistory</c>; as tabelas do
/// pethub-java no mesmo schema não são tocadas. Por isso a ordem de subida dos dois serviços continua não importando.
/// </para>
/// <para>
/// Bancos criados antes das migrations (pelo antigo <c>CreateTables()</c>) já têm as tabelas, mas não têm histórico:
/// nesse caso a migration inicial é registrada como aplicada ("baseline") em vez de tentar criar tudo de novo.
/// </para>
/// </summary>
public static class InicializadorDoBanco
{
    private const string TabelaSentinela = "TB_RESPONSAVEL";

    public static void AplicarMigracoes(IServiceProvider servicos)
    {
        using var escopo = servicos.CreateScope();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InicializadorDoBanco));
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        // Testes de integração usam o provider em memória, que não tem migrations.
        if (!contexto.Database.IsRelational())
            return;

        try
        {
            if (!contexto.Database.GetAppliedMigrations().Any() && TabelasLegadasExistem(contexto))
                RegistrarBaseline(contexto, logger);

            var pendentes = contexto.Database.GetPendingMigrations().ToList();
            contexto.Database.Migrate();

            if (pendentes.Count > 0)
                logger.LogInformation("Migrations aplicadas: {Migracoes}.", string.Join(", ", pendentes));
        }
        catch (Exception ex)
        {
            // O banco fora do ar na subida não deve impedir a API de responder /health.
            logger.LogError(ex, "Não foi possível aplicar as migrations do Vitalis.");
        }
    }

    private static bool TabelasLegadasExistem(AppDbContext contexto)
        => contexto.Database
            .SqlQueryRaw<int>($"SELECT COUNT(*) AS \"Value\" FROM USER_TABLES WHERE TABLE_NAME = '{TabelaSentinela}'")
            .AsEnumerable().First() > 0;

    /// <summary>
    /// Marca a migration inicial como aplicada num banco que já tem as tabelas. Antes, garante a coluna
    /// <c>DATA_HORA</c>, que bancos mais antigos ainda não tinham e que a migration inicial já prevê.
    /// </summary>
    private static void RegistrarBaseline(AppDbContext contexto, ILogger logger)
    {
        var temDataHora = contexto.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'TB_LEMBRETE' AND COLUMN_NAME = 'DATA_HORA'")
            .AsEnumerable().First() > 0;
        if (!temDataHora)
        {
            contexto.Database.ExecuteSqlRaw("ALTER TABLE TB_LEMBRETE ADD DATA_HORA TIMESTAMP(7)");
            logger.LogInformation("Coluna TB_LEMBRETE.DATA_HORA acrescentada antes do baseline.");
        }

        contexto.GetService<IHistoryRepository>().CreateIfNotExists();

        var inicial = contexto.Database.GetMigrations().First();
        contexto.Database.ExecuteSql(
            $"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({inicial}, {ProductInfo.GetVersion()})");

        logger.LogInformation("Banco existente sem histórico: migration {Migracao} registrada como já aplicada.", inicial);
    }
}
