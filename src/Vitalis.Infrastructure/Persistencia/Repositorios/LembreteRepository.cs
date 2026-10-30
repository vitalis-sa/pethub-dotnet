using Vitalis.Application.Abstracoes;
using Vitalis.Application.Consultas;
using Vitalis.Domain.Entidades;
using Vitalis.Domain.Enums;

namespace Vitalis.Infrastructure.Persistencia.Repositorios;

public class LembreteRepository(AppDbContext contexto) : ILembreteRepository
{
    public (IReadOnlyList<Lembrete> Itens, int Total) Consultar(
        long responsavelId, FiltroDeLembretes filtro, ConsultaPaginada consulta)
    {
        var consultaBase = contexto.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && !(l.DataHora != null && l.Status == StatusLembrete.PENDENTE));

        if (filtro.Tipo is not null)
        {
            var tipo = filtro.Tipo.Value;
            consultaBase = consultaBase.Where(l => l.Tipo == tipo);
        }
        if (filtro.Status is not null)
        {
            var status = filtro.Status.Value;
            consultaBase = consultaBase.Where(l => l.Status == status);
        }
        if (filtro.PetId is not null)
        {
            var petId = filtro.PetId.Value;
            consultaBase = consultaBase.Where(l => l.PetId == petId);
        }
        if (filtro.De is not null)
        {
            var de = filtro.De.Value;
            consultaBase = consultaBase.Where(l => l.DataAgendada >= de);
        }
        if (filtro.Ate is not null)
        {
            var ate = filtro.Ate.Value;
            consultaBase = consultaBase.Where(l => l.DataAgendada <= ate);
        }

        var total = consultaBase.Count();
        var ordenada = consulta.CampoDeOrdenacao("dataAgendada").ToLowerInvariant() switch
        {
            "tipo" => Ordenacao.Por(consultaBase, l => l.Tipo, consulta.Decrescente(padrao: false)).ThenByDescending(l => l.Id),
            "status" => Ordenacao.Por(consultaBase, l => l.Status, consulta.Decrescente(padrao: false)).ThenByDescending(l => l.Id),
            "id" => Ordenacao.Por(consultaBase, l => l.Id, consulta.Decrescente(padrao: true)),
            // Padrão: os mais recentes primeiro, como a listagem sempre foi.
            _ => Ordenacao.Por(consultaBase, l => l.DataAgendada, consulta.Decrescente(padrao: true)).ThenByDescending(l => l.Id)
        };

        return (ordenada.Skip(consulta.Deslocamento()).Take(consulta.Tamanho).ToList(), total);
    }

    public IReadOnlyList<Lembrete> ListarDosesFuturas(long responsavelId, DateTime agora, int limite)
        => contexto.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && l.DataHora != null && l.DataHora > agora
                        && l.Status == StatusLembrete.PENDENTE)
            .OrderBy(l => l.DataHora)
            .Take(limite)
            .ToList();

    public Lembrete? ObterPorId(long id) => contexto.Lembretes.FirstOrDefault(l => l.Id == id);

    public Lembrete? ObterPorReferencia(long responsavelId, string referenciaTipo, long referenciaId)
        => contexto.Lembretes.FirstOrDefault(l => l.ResponsavelId == responsavelId
                                                  && l.ReferenciaTipo == referenciaTipo
                                                  && l.ReferenciaId == referenciaId);

    public IReadOnlyList<Lembrete> ListarDosesPendentes(long responsavelId, string referenciaTipo, long referenciaId)
        => contexto.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && l.ReferenciaTipo == referenciaTipo
                        && l.ReferenciaId == referenciaId && l.DataHora != null && l.Status == StatusLembrete.PENDENTE)
            .ToList();

    public IReadOnlyList<Lembrete> ListarDosesVencidas(DateTime agora)
        => contexto.Lembretes
            .Where(l => l.DataHora != null && l.DataHora <= agora && l.Status == StatusLembrete.PENDENTE)
            .ToList();

    public void Adicionar(Lembrete lembrete) => contexto.Lembretes.Add(lembrete);

    public void AdicionarVarios(IEnumerable<Lembrete> lembretes) => contexto.Lembretes.AddRange(lembretes);

    public void Remover(Lembrete lembrete) => contexto.Lembretes.Remove(lembrete);

    public void RemoverVarios(IEnumerable<Lembrete> lembretes) => contexto.Lembretes.RemoveRange(lembretes);

    public void SalvarAlteracoes() => contexto.SaveChanges();
}
