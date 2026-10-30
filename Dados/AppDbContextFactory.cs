using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Oracle.EntityFrameworkCore.Infrastructure;

namespace Vitalis.Dados;

/// <summary>
/// Usada só pelas ferramentas do EF (<c>dotnet ef migrations add</c>, <c>dotnet ef database update</c>).
/// Monta o contexto sem subir a API inteira, que exige os segredos de JWT e de serviço.
/// A conexão vem da variável <c>ConnectionStrings__OracleConnection</c>; gerar uma migration não abre conexão,
/// então o valor de exemplo basta para isso.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string ConexaoDeExemplo = "User Id=vitalis;Password=vitalis;Data Source=localhost:1521/FREEPDB1";

    public AppDbContext CreateDbContext(string[] args)
    {
        var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__OracleConnection");

        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseOracle(string.IsNullOrWhiteSpace(conexao) ? ConexaoDeExemplo : conexao,
                oracle => oracle.UseOracleSQLCompatibility(OracleSQLCompatibility.DatabaseVersion19))
            .Options;

        return new AppDbContext(opcoes);
    }
}
