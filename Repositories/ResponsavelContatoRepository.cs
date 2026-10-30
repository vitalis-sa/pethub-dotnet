using Vitalis.Consultas;

namespace Vitalis.Repositories;

public class ResponsavelContatoRepository : IResponsavelContatoRepository
{
    private readonly AppDbContext _context;

    public ResponsavelContatoRepository(AppDbContext context)
    {
        _context = context;
    }

    public IEnumerable<ResponsavelContato> GetByResponsavelId(long responsavelId)
        => _context.Contatos
            .Where(c => c.ResponsavelId == responsavelId)
            .ToList();

    public (IReadOnlyList<ResponsavelContato> Itens, int Total) Consultar(
        long responsavelId, FiltroDeContatos filtro, ConsultaPaginada consulta)
    {
        var consultaBase = _context.Contatos.Where(c => c.ResponsavelId == responsavelId);

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

        return (ordenada.Skip(consulta.Ignorar).Take(consulta.Tamanho).ToList(), total);
    }

    public ResponsavelContato? GetById(long id)
        => _context.Contatos.FirstOrDefault(c => c.Id == id);

    public void Add(ResponsavelContato contato)
    {
        bool temOutros = _context.Contatos.Any(c => c.ResponsavelId == contato.ResponsavelId);
        if (!temOutros)
            contato.Principal = true;

        if (contato.Principal)
            DesmarcarOutros(contato.ResponsavelId, excludeId: null);

        _context.Contatos.Add(contato);
        _context.SaveChanges();
    }

    public void Update(ResponsavelContato contato)
    {
        if (contato.Principal)
            DesmarcarOutros(contato.ResponsavelId, excludeId: contato.Id);

        _context.Contatos.Update(contato);
        _context.SaveChanges();
    }

    public void Delete(long id)
    {
        var contato = GetById(id);
        if (contato == null) return;

        _context.Contatos.Remove(contato);
        _context.SaveChanges();

        if (contato.Principal)
        {
            var substituto = _context.Contatos
                .Where(c => c.ResponsavelId == contato.ResponsavelId)
                .OrderByDescending(c => c.Id)
                .FirstOrDefault();

            if (substituto != null)
            {
                substituto.Principal = true;
                _context.SaveChanges();
            }
        }
    }

    public void SetPrincipal(long responsavelId, long contatoId)
    {
        DesmarcarOutros(responsavelId, excludeId: contatoId);

        var alvo = GetById(contatoId);
        if (alvo != null)
        {
            alvo.Principal = true;
            _context.SaveChanges();
        }
    }

    private void DesmarcarOutros(long responsavelId, long? excludeId)
    {
        // A exclusão do próprio id é aplicada em C#: um `excludeId == null || ...` dentro da consulta
        // vira o literal FALSE no SQL, que o Oracle da FIAP não aceita (ORA-00904).
        var consulta = _context.Contatos.Where(c => c.ResponsavelId == responsavelId && c.Principal);
        if (excludeId.HasValue)
        {
            var idIgnorado = excludeId.Value;
            consulta = consulta.Where(c => c.Id != idIgnorado);
        }

        var outros = consulta.ToList();

        foreach (var c in outros)
            c.Principal = false;
    }
}