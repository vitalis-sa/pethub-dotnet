using Vitalis.Models;

namespace Vitalis.Repositories;

public interface ILembreteRepository
{
    IEnumerable<Lembrete> GetAll();
    IEnumerable<Lembrete> GetByResponsavelId(long responsavelId);
    /// <summary>Doses de medicamento ainda por vir, em ordem de horário: o app agenda a notificação de cada uma.</summary>
    IEnumerable<Lembrete> GetDosesFuturas(long responsavelId, DateTime agora, int limite);
    IEnumerable<Lembrete> GetByResponsavelIdETipo(long responsavelId, TipoLembrete tipo);
    Lembrete? GetById(long id);
    /// <summary>
    /// Atualiza o lembrete ligado a uma consulta (ou outra referência) quando ela muda de data.
    /// Devolve false se ainda não existe lembrete para a referência.
    /// </summary>
    bool AtualizarPorReferencia(long responsavelId, string referenciaTipo, long referenciaId,
        DateOnly dataAgendada, string mensagem);
    /// <summary>
    /// Troca as doses ainda não liberadas de uma referência (medicamento) pelas novas, sem duplicar
    /// quando a mesma mensagem chega de novo.
    /// </summary>
    void SubstituirDoses(long responsavelId, string referenciaTipo, long? referenciaId, IReadOnlyList<Lembrete> doses);
    /// <summary>Apaga as doses ainda não liberadas de uma referência (tratamento removido). Devolve quantas.</summary>
    int RemoverDosesPendentes(long responsavelId, string referenciaTipo, long referenciaId);
    /// <summary>Libera (PENDENTE vira ENVIADO) as doses cujo horário já chegou. Devolve quantas.</summary>
    int LiberarVencidos(DateTime agora);
    void Add(Lembrete lembrete);
    void AtualizarStatus(long id, StatusLembrete status);
    void Delete(long id);
}