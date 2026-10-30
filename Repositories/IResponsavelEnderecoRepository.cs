
using Vitalis.Consultas;

namespace Vitalis.Repositories;

public interface IResponsavelEnderecoRepository
{
    IEnumerable<ResponsavelEndereco> GetByResponsavelId(long responsavelId);
    /// <summary>Endereços do responsável filtrados, ordenados e paginados, com o total antes da paginação.</summary>
    (IReadOnlyList<ResponsavelEndereco> Itens, int Total) Consultar(
        long responsavelId, FiltroDeEnderecos filtro, ConsultaPaginada consulta);
    ResponsavelEndereco? GetById(long id);
    void Add(ResponsavelEndereco endereco);
    void Update(ResponsavelEndereco endereco);
    void Delete(long id);
    void SetPrincipal(long responsavelId, long enderecoId);
}