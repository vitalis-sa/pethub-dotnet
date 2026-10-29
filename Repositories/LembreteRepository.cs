using Microsoft.EntityFrameworkCore;
using Vitalis.Models;

namespace Vitalis.Repositories;

public class LembreteRepository : ILembreteRepository
{
    private readonly AppDbContext _context;

    public LembreteRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<Lembrete> GetAll()
        => _context.Lembretes
            .Include(l => l.Responsavel)
            .OrderByDescending(l => l.DataAgendada)
            .ToList();

    public IEnumerable<Lembrete> GetByResponsavelId(long responsavelId)
        => _context.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && !(l.DataHora != null && l.Status == StatusLembrete.PENDENTE))
            .OrderByDescending(l => l.DataAgendada)
            .ToList();

    public IEnumerable<Lembrete> GetDosesFuturas(long responsavelId, DateTime agora, int limite)
        => _context.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && l.DataHora != null && l.DataHora > agora
                        && l.Status == StatusLembrete.PENDENTE)
            .OrderBy(l => l.DataHora)
            .Take(limite)
            .ToList();

    public IEnumerable<Lembrete> GetByResponsavelIdETipo(long responsavelId, TipoLembrete tipo)
        => _context.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && l.Tipo == tipo
                        && !(l.DataHora != null && l.Status == StatusLembrete.PENDENTE))
            .OrderByDescending(l => l.DataAgendada)
            .ToList();

    public Lembrete? GetById(long id)
        => _context.Lembretes
            .Include(l => l.Responsavel)
            .FirstOrDefault(l => l.Id == id);

    public bool AtualizarPorReferencia(long responsavelId, string referenciaTipo, long referenciaId,
        DateOnly dataAgendada, string mensagem)
    {
        var lembrete = _context.Lembretes.FirstOrDefault(l => l.ResponsavelId == responsavelId
                                                              && l.ReferenciaTipo == referenciaTipo
                                                              && l.ReferenciaId == referenciaId);
        if (lembrete is null)
        {
            return false;
        }

        lembrete.DataAgendada = dataAgendada;
        lembrete.Mensagem = mensagem;
        lembrete.Status = StatusLembrete.PENDENTE;
        _context.SaveChanges();
        return true;
    }

    public void SubstituirDoses(long responsavelId, string referenciaTipo, long? referenciaId, IReadOnlyList<Lembrete> doses)
    {
        if (referenciaId is not null)
        {
            var antigas = _context.Lembretes
                .Where(l => l.ResponsavelId == responsavelId && l.ReferenciaTipo == referenciaTipo
                            && l.ReferenciaId == referenciaId && l.DataHora != null && l.Status == StatusLembrete.PENDENTE)
                .ToList();
            _context.Lembretes.RemoveRange(antigas);
        }

        var criadoEm = Vitalis.Configuracao.Relogio.Agora();
        foreach (var dose in doses)
        {
            dose.CreatedAt = criadoEm;
            dose.Status = StatusLembrete.PENDENTE;
        }
        _context.Lembretes.AddRange(doses);
        _context.SaveChanges();
    }

    public int RemoverDosesPendentes(long responsavelId, string referenciaTipo, long referenciaId)
    {
        var pendentes = _context.Lembretes
            .Where(l => l.ResponsavelId == responsavelId && l.ReferenciaTipo == referenciaTipo
                        && l.ReferenciaId == referenciaId && l.DataHora != null && l.Status == StatusLembrete.PENDENTE)
            .ToList();
        _context.Lembretes.RemoveRange(pendentes);
        _context.SaveChanges();
        return pendentes.Count;
    }

    public int LiberarVencidos(DateTime agora)
    {
        var vencidos = _context.Lembretes
            .Where(l => l.DataHora != null && l.DataHora <= agora && l.Status == StatusLembrete.PENDENTE)
            .ToList();
        foreach (var lembrete in vencidos)
            lembrete.Status = StatusLembrete.ENVIADO;

        _context.SaveChanges();
        return vencidos.Count;
    }

    public void Add(Lembrete lembrete)
    {
        lembrete.CreatedAt = Vitalis.Configuracao.Relogio.Agora();
        lembrete.Status = StatusLembrete.PENDENTE;
        _context.Lembretes.Add(lembrete);
        _context.SaveChanges();
    }

    public void AtualizarStatus(long id, StatusLembrete status)
    {
        var lembrete = _context.Lembretes.Find(id);
        if (lembrete != null)
        {
            lembrete.Status = status;
            _context.SaveChanges();
        }
    }
    
    public void Delete(long id)
    {
        var lembrete = _context.Lembretes.Find(id);
        if (lembrete != null)
        {
            _context.Lembretes.Remove(lembrete);
            _context.SaveChanges();
        }
    }
}