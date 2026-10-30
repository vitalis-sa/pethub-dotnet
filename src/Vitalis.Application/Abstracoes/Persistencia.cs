using Vitalis.Application.Consultas;
using Vitalis.Domain.Entidades;

namespace Vitalis.Application.Abstracoes;

// Portas de persistência: a aplicação diz o que precisa, a infraestrutura (EF Core + Oracle) implementa.
// Os métodos de escrita só preparam a mudança; SalvarAlteracoes grava tudo de uma vez (mesma transação).

public interface IResponsavelRepository
{
    /// <summary>O responsável com endereços e contatos.</summary>
    Responsavel? ObterPorId(long id);
    bool Existe(long id);
    /// <summary>Busca só entre os ativos.</summary>
    Responsavel? ObterPorCpf(string cpf);
    Responsavel? ObterPorEmail(string email);
    void Adicionar(Responsavel responsavel);
    void Remover(Responsavel responsavel);
    void SalvarAlteracoes();
}

/// <summary>Endereços e contatos: listas do responsável com um item principal.</summary>
public interface IRepositorioDeItensDoResponsavel<TItem, TFiltro> where TItem : class
{
    IReadOnlyList<TItem> ListarDoResponsavel(long responsavelId);
    /// <summary>Filtrados, ordenados e paginados, com o total antes da paginação.</summary>
    (IReadOnlyList<TItem> Itens, int Total) Consultar(long responsavelId, TFiltro filtro, ConsultaPaginada consulta);
    TItem? ObterPorId(long id);
    void Adicionar(TItem item);
    void Remover(TItem item);
    void SalvarAlteracoes();
}

public interface IResponsavelEnderecoRepository : IRepositorioDeItensDoResponsavel<ResponsavelEndereco, FiltroDeEnderecos>;

public interface IResponsavelContatoRepository : IRepositorioDeItensDoResponsavel<ResponsavelContato, FiltroDeContatos>;

public interface ILembreteRepository
{
    /// <summary>
    /// Lembretes do responsável filtrados, ordenados e paginados, com o total antes da paginação.
    /// Doses de medicamento ainda não liberadas ficam de fora: só aparecem quando o horário chega.
    /// </summary>
    (IReadOnlyList<Lembrete> Itens, int Total) Consultar(long responsavelId, FiltroDeLembretes filtro, ConsultaPaginada consulta);
    /// <summary>Doses ainda pendentes depois de <paramref name="agora"/>, em ordem de horário.</summary>
    IReadOnlyList<Lembrete> ListarDosesFuturas(long responsavelId, DateTime agora, int limite);
    Lembrete? ObterPorId(long id);
    /// <summary>O lembrete ligado a um evento do pethub-java (consulta, exame...).</summary>
    Lembrete? ObterPorReferencia(long responsavelId, string referenciaTipo, long referenciaId);
    /// <summary>Doses ainda não liberadas de um tratamento.</summary>
    IReadOnlyList<Lembrete> ListarDosesPendentes(long responsavelId, string referenciaTipo, long referenciaId);
    /// <summary>Doses pendentes cujo horário já chegou.</summary>
    IReadOnlyList<Lembrete> ListarDosesVencidas(DateTime agora);
    void Adicionar(Lembrete lembrete);
    void AdicionarVarios(IEnumerable<Lembrete> lembretes);
    void Remover(Lembrete lembrete);
    void RemoverVarios(IEnumerable<Lembrete> lembretes);
    void SalvarAlteracoes();
}
