using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Vitalis.Domain.Entidades;

namespace Vitalis.Infrastructure.Persistencia.Configuracoes;

// Mapeamento das entidades para as tabelas do Oracle. Fica aqui, e não em atributos nas entidades, para que o
// domínio não conheça o banco. As regras de validação ([Required], [StringLength]) continuam nas entidades e o
// EF as usa para NOT NULL e tamanho das colunas.

public class ResponsavelConfiguracao : IEntityTypeConfiguration<Responsavel>
{
    public void Configure(EntityTypeBuilder<Responsavel> entidade)
    {
        entidade.ToTable("TB_RESPONSAVEL");
        entidade.HasKey(r => r.Id);

        entidade.Property(r => r.Id).HasColumnName("ID");
        entidade.Property(r => r.Nome).HasColumnName("NOME");
        entidade.Property(r => r.Cpf).HasColumnName("CPF");
        entidade.Property(r => r.Email).HasColumnName("EMAIL");
        entidade.Property(r => r.Senha).HasColumnName("SENHA");
        // Oracle anterior ao 23ai não tem tipo booleano.
        entidade.Property(r => r.Ativo).HasColumnName("ATIVO").HasColumnType("NUMBER(1)");
        entidade.Property(r => r.CreatedAt).HasColumnName("CREATED_AT").HasDefaultValueSql("SYSDATE");
    }
}

public class ResponsavelEnderecoConfiguracao : IEntityTypeConfiguration<ResponsavelEndereco>
{
    public void Configure(EntityTypeBuilder<ResponsavelEndereco> entidade)
    {
        entidade.ToTable("TB_RESPONSAVEL_ENDERECO");
        entidade.HasKey(e => e.Id);

        entidade.Property(e => e.Id).HasColumnName("ID");
        entidade.Property(e => e.ResponsavelId).HasColumnName("RESPONSAVEL_ID");
        entidade.Property(e => e.Logradouro).HasColumnName("LOGRADOURO");
        entidade.Property(e => e.Numero).HasColumnName("NUMERO");
        entidade.Property(e => e.Complemento).HasColumnName("COMPLEMENTO");
        entidade.Property(e => e.Bairro).HasColumnName("BAIRRO");
        entidade.Property(e => e.Cidade).HasColumnName("CIDADE");
        entidade.Property(e => e.Estado).HasColumnName("ESTADO");
        entidade.Property(e => e.Cep).HasColumnName("CEP");
        entidade.Property(e => e.Principal).HasColumnName("PRINCIPAL").HasColumnType("NUMBER(1)");

        entidade.HasOne(e => e.Responsavel)
            .WithMany(r => r.Enderecos)
            .HasForeignKey(e => e.ResponsavelId);
    }
}

public class ResponsavelContatoConfiguracao : IEntityTypeConfiguration<ResponsavelContato>
{
    public void Configure(EntityTypeBuilder<ResponsavelContato> entidade)
    {
        entidade.ToTable("TB_RESPONSAVEL_CONTATO");
        entidade.HasKey(c => c.Id);

        entidade.Property(c => c.Id).HasColumnName("ID");
        entidade.Property(c => c.ResponsavelId).HasColumnName("RESPONSAVEL_ID");
        entidade.Property(c => c.Tipo).HasColumnName("TIPO");
        entidade.Property(c => c.Telefone).HasColumnName("TELEFONE");
        entidade.Property(c => c.Principal).HasColumnName("PRINCIPAL").HasColumnType("NUMBER(1)");

        entidade.HasOne(c => c.Responsavel)
            .WithMany(r => r.Contatos)
            .HasForeignKey(c => c.ResponsavelId);
    }
}

public class LembreteConfiguracao : IEntityTypeConfiguration<Lembrete>
{
    public void Configure(EntityTypeBuilder<Lembrete> entidade)
    {
        entidade.ToTable("TB_LEMBRETE");
        entidade.HasKey(l => l.Id);
        entidade.Ignore(l => l.EhDose);

        entidade.Property(l => l.Id).HasColumnName("ID");
        entidade.Property(l => l.ResponsavelId).HasColumnName("RESPONSAVEL_ID");
        entidade.Property(l => l.PetId).HasColumnName("PET_ID");
        // Enums gravados como texto ("VACINA", "PENDENTE"), legíveis direto no banco.
        entidade.Property(l => l.Tipo).HasColumnName("TIPO").HasConversion<string>();
        entidade.Property(l => l.DataAgendada).HasColumnName("DATA_AGENDADA");
        entidade.Property(l => l.DataHora).HasColumnName("DATA_HORA");
        entidade.Property(l => l.Mensagem).HasColumnName("MENSAGEM");
        entidade.Property(l => l.Status).HasColumnName("STATUS").HasConversion<string>();
        entidade.Property(l => l.ReferenciaId).HasColumnName("REFERENCIA_ID");
        entidade.Property(l => l.ReferenciaTipo).HasColumnName("REFERENCIA_TIPO");
        entidade.Property(l => l.CreatedAt).HasColumnName("CREATED_AT").HasDefaultValueSql("SYSDATE");

        entidade.HasOne(l => l.Responsavel)
            .WithMany(r => r.Lembretes)
            .HasForeignKey(l => l.ResponsavelId);
    }
}
