using Vitalis.Consultas;

namespace Vitalis.Repositories;

public interface IResponsavelContatoRepository
{
    IEnumerable<ResponsavelContato> GetByResponsavelId(long responsavelId);
    /// <summary>Contatos do responsável filtrados, ordenados e paginados, com o total antes da paginação.</summary>
    (IReadOnlyList<ResponsavelContato> Itens, int Total) Consultar(
        long responsavelId, FiltroDeContatos filtro, ConsultaPaginada consulta);
    ResponsavelContato? GetById(long id);
    void Add(ResponsavelContato contato);
    void Update(ResponsavelContato contato);
    void Delete(long id);
    void SetPrincipal(long responsavelId, long contatoId);
}