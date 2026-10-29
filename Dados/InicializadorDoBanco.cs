using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Vitalis.Dados;

/// <summary>
/// Cria as tabelas do Vitalis, e só elas, se ainda não existirem. O Java faz o mesmo com as dele,
/// então os dois serviços podem subir em qualquer ordem no mesmo schema Oracle sem derrubar um ao outro
/// (por isso não se usa EnsureCreated, que desiste se achar qualquer tabela no schema).
/// </summary>
public static class InicializadorDoBanco
{
    private const string TabelaSentinela = "TB_RESPONSAVEL";

    /// <summary>Bancos criados antes de uma coluna existir não a ganham sozinhos: ela é acrescentada sem apagar nada.</summary>
    private static void AdicionarColunasNovas(AppDbContext contexto, ILogger logger)
    {
        var temDataHora = contexto.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM USER_TAB_COLUMNS WHERE TABLE_NAME = 'TB_LEMBRETE' AND COLUMN_NAME = 'DATA_HORA'")
            .AsEnumerable().First() > 0;
        if (temDataHora)
            return;

        contexto.Database.ExecuteSqlRaw("ALTER TABLE TB_LEMBRETE ADD DATA_HORA TIMESTAMP(7)");
        logger.LogInformation("Coluna TB_LEMBRETE.DATA_HORA acrescentada.");
    }

    public static void CriarTabelasSeNecessario(IServiceProvider servicos)
    {
        using var escopo = servicos.CreateScope();
        var logger = escopo.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(InicializadorDoBanco));
        var contexto = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        if (!contexto.Database.IsRelational())
            return;

        try
        {
            var existe = contexto.Database
                .SqlQueryRaw<int>($"SELECT COUNT(*) AS \"Value\" FROM USER_TABLES WHERE TABLE_NAME = '{TabelaSentinela}'")
                .AsEnumerable().First() > 0;

            if (existe)
            {
                AdicionarColunasNovas(contexto, logger);
                return;
            }

            contexto.GetService<IRelationalDatabaseCreator>().CreateTables();
            logger.LogInformation("Tabelas do Vitalis criadas.");
        }
        catch (Exception ex)
        {
            // O banco fora do ar na subida não deve impedir a API de responder /health.
            logger.LogError(ex, "Não foi possível verificar ou criar as tabelas do Vitalis.");
        }
    }
}
