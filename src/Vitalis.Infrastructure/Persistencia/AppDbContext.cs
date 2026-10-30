using Microsoft.EntityFrameworkCore;
using Vitalis.Domain.Entidades;

namespace Vitalis.Infrastructure.Persistencia;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Responsavel> Responsavels => Set<Responsavel>();
    public DbSet<ResponsavelEndereco> Enderecos => Set<ResponsavelEndereco>();
    public DbSet<ResponsavelContato> Contatos => Set<ResponsavelContato>();
    public DbSet<Lembrete> Lembretes => Set<Lembrete>();

    /// <summary>O mapeamento de cada entidade está em <c>Persistencia/Configuracoes</c>.</summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
        => modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}
