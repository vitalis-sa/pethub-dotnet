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

    public bool CpfEmUso(string cpf, long? excetoId = null)
        => Outros(contexto.Responsavels.Where(r => r.Cpf == cpf && r.Ativo), excetoId).Any();

    public bool EmailEmUso(string email, long? excetoId = null)
        => Outros(contexto.Responsavels.Where(r => r.Email == email), excetoId).Any();

    /// <summary>
    /// Tira o próprio responsável da busca só quando há um: um "excetoId == null || ..." dentro da consulta vira
    /// o literal FALSE no SQL, que o Oracle da FIAP não aceita (ORA-00904).
    /// </summary>
    private static IQueryable<Responsavel> Outros(IQueryable<Responsavel> consulta, long? excetoId)
    {
        if (excetoId is null)
            return consulta;

        var id = excetoId.Value;
        return consulta.Where(r => r.Id != id);
    }

    public void Adicionar(Responsavel responsavel) => contexto.Responsavels.Add(responsavel);

    public void Remover(Responsavel responsavel) => contexto.Responsavels.Remove(responsavel);

    public void SalvarAlteracoes() => contexto.SaveChanges();
}
