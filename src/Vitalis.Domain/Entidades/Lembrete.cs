using System.ComponentModel.DataAnnotations;
using Vitalis.Domain.Enums;

namespace Vitalis.Domain.Entidades;

/// <summary>
/// Aviso de um evento veterinário (consulta, vacina, exame ou dose de medicamento) para o responsável.
/// Nasce de uma mensagem do pethub-java; o responsável só consulta, muda o status e apaga.
/// </summary>
public class Lembrete
{
    public long Id { get; set; }

    public long ResponsavelId { get; set; }
    public Responsavel? Responsavel { get; set; }

    public long PetId { get; set; }

    [Required]
    public TipoLembrete Tipo { get; set; }

    public DateOnly DataAgendada { get; set; }

    /// <summary>Horário exato da dose (lembretes de medicamento). Nulo nos demais, que valem para o dia todo.</summary>
    public DateTime? DataHora { get; set; }

    [Required] public string Mensagem { get; set; } = null!;

    public StatusLembrete Status { get; set; } = StatusLembrete.PENDENTE;

    public long? ReferenciaId { get; set; }
    public string? ReferenciaTipo { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Dose de medicamento com horário marcado (as demais valem para o dia todo).</summary>
    public bool EhDose => DataHora is not null;

    /// <summary>
    /// O evento de origem mudou (consulta remarcada) ou a mesma mensagem chegou de novo: atualiza a data e o texto
    /// e volta a ficar pendente, em vez de criar outro lembrete.
    /// </summary>
    public void Reagendar(DateOnly dataAgendada, string mensagem)
    {
        DataAgendada = dataAgendada;
        Mensagem = mensagem;
        Status = StatusLembrete.PENDENTE;
    }

    public void AlterarStatus(StatusLembrete status) => Status = status;

    /// <summary>O horário da dose chegou: ela passa a aparecer para o responsável.</summary>
    public void Liberar() => Status = StatusLembrete.ENVIADO;
}
