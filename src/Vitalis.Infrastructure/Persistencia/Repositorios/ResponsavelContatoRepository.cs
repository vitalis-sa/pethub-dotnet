using Vitalis.Application.Abstracoes;
using Vitalis.Application.Consultas;
using Vitalis.Domain.Entidades;

namespace Vitalis.Infrastructure.Persistencia.Repositorios;

public class ResponsavelContatoRepository(AppDbContext contexto) : IResponsavelContatoRepository
{
    public IReadOnlyList<ResponsavelContato> ListarDoResponsavel(long responsavelId)
        => contexto.Contatos.Where(c => c.ResponsavelId == responsavelId).ToList();

    public (IReadOnlyList<ResponsavelContato> Itens, int Total) Consultar(
        long responsavelId, FiltroDeContatos filtro, ConsultaPaginada consulta)
    {
        var consultaBase = contexto.Contatos.Where(c => c.ResponsavelId == responsavelId);

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            var tipo = filtro.Tipo.Trim().ToUpper();
            consultaBase = consultaBase.Where(c => c.Tipo == tipo);
        }
        if (filtro.Principal is not null)
        {
            var principal = filtro.Principal.Value;
            consultaBase = consultaBase.Where(c => c.Principal == principal);
        }

        var total = consultaBase.Count();
        var desc = consulta.Decrescente(padrao: false);
        var ordenada = consulta.CampoDeOrdenacao("id").ToLowerInvariant() switch
        {
            "tipo" => Ordenacao.Por(consultaBase, c => c.Tipo, desc).ThenBy(c => c.Id),
            "telefone" => Ordenacao.Por(consultaBase, c => c.Telefone, desc).ThenBy(c => c.Id),
            _ => Ordenacao.Por(consultaBase, c => c.Id, desc)
        };

        return (ordenada.Skip(consulta.Deslocamento()).Take(consulta.Tamanho).ToList(), total);
    }

    public ResponsavelContato? ObterPorId(long id) => contexto.Contatos.FirstOrDefault(c => c.Id == id);

    public void Adicionar(ResponsavelContato item) => contexto.Contatos.Add(item);

    public void Remover(ResponsavelContato item) => contexto.Contatos.Remove(item);

    public void SalvarAlteracoes() => contexto.SaveChanges();
}
