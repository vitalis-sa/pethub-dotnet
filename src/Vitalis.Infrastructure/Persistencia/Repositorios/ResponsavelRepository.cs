using Microsoft.EntityFrameworkCore;
using Vitalis.Application.Abstracoes;
using Vitalis.Domain.Entidades;

namespace Vitalis.Infrastructure.Persistencia.Repositorios;

public class ResponsavelRepository(AppDbContext contexto) : IResponsavelRepository
{
    public Responsavel? ObterPorId(long id)
        => contexto.Responsavels
            .Include(r => r.Enderecos)
            .Include(r => r.Contatos)
            .FirstOrDefault(r => r.Id == id);

    public bool Existe(long id)
        => contexto.Responsavels.Any(r => r.Id == id);

    public Responsavel? ObterPorCpf(string cpf)
        => contexto.Responsavels.FirstOrDefault(r => r.Cpf == cpf && r.Ativo);

    public Responsavel? ObterPorEmail(string email)
        => contexto.Responsavels.FirstOrDefault(r => r.Email == email);

    public void Adicionar(Responsavel responsavel) => contexto.Responsavels.Add(responsavel);

    public void Remover(Responsavel responsavel) => contexto.Responsavels.Remove(responsavel);

    public void SalvarAlteracoes() => contexto.SaveChanges();
}
