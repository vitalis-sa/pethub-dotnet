using Vitalis.Application.Abstracoes;
using Vitalis.Application.Consultas;
using Vitalis.Domain.Entidades;

namespace Vitalis.Infrastructure.Persistencia.Repositorios;

public class ResponsavelEnderecoRepository(AppDbContext contexto) : IResponsavelEnderecoRepository
{
    public IReadOnlyList<ResponsavelEndereco> ListarDoResponsavel(long responsavelId)
        => contexto.Enderecos.Where(e => e.ResponsavelId == responsavelId).ToList();

    public (IReadOnlyList<ResponsavelEndereco> Itens, int Total) Consultar(
        long responsavelId, FiltroDeEnderecos filtro, ConsultaPaginada consulta)
    {
        var consultaBase = contexto.Enderecos.Where(e => e.ResponsavelId == responsavelId);

        // Cada filtro só entra na consulta quando informado: um "x == null || ..." no SQL vira o literal
        // FALSE, que o Oracle da FIAP não aceita (ORA-00904).
        if (!string.IsNullOrWhiteSpace(filtro.Cidade))
        {
            var cidade = filtro.Cidade.Trim().ToUpper();
            consultaBase = consultaBase.Where(e => e.Cidade.ToUpper().Contains(cidade));
        }
        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            var estado = filtro.Estado.Trim().ToUpper();
            consultaBase = consultaBase.Where(e => e.Estado == estado);
        }
        if (filtro.Principal is not null)
        {
            var principal = filtro.Principal.Value;
            consultaBase = consultaBase.Where(e => e.Principal == principal);
        }

        var total = consultaBase.Count();
        var desc = consulta.Decrescente(padrao: false);
        var ordenada = consulta.CampoDeOrdenacao("id").ToLowerInvariant() switch
        {
            "cidade" => Ordenacao.Por(consultaBase, e => e.Cidade, desc).ThenBy(e => e.Id),
            "estado" => Ordenacao.Por(consultaBase, e => e.Estado, desc).ThenBy(e => e.Id),
            "bairro" => Ordenacao.Por(consultaBase, e => e.Bairro, desc).ThenBy(e => e.Id),
            "logradouro" => Ordenacao.Por(consultaBase, e => e.Logradouro, desc).ThenBy(e => e.Id),
            _ => Ordenacao.Por(consultaBase, e => e.Id, desc)
        };

        return (ordenada.Skip(consulta.Deslocamento()).Take(consulta.Tamanho).ToList(), total);
    }

    public ResponsavelEndereco? ObterPorId(long id) => contexto.Enderecos.FirstOrDefault(e => e.Id == id);

    public void Adicionar(ResponsavelEndereco item) => contexto.Enderecos.Add(item);

    public void Remover(ResponsavelEndereco item) => contexto.Enderecos.Remove(item);

    public void SalvarAlteracoes() => contexto.SaveChanges();
}
