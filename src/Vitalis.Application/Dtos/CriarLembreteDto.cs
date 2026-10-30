using Vitalis.Domain.Enums;

namespace Vitalis.Application.Dtos;

public class CriarLembreteDto
{
    public long ResponsavelId { get; set; }
    public long PetId { get; set; }
    public TipoLembrete Tipo { get; set; }
    public DateOnly DataAgendada { get; set; }
    public string Mensagem { get; set; } = null!;
    /// <summary>Só em lembretes de medicamento: doses por dia, duração e quando a receita foi salva.</summary>
    public int? VezesPorDia { get; set; }
    public int? DuracaoDias { get; set; }
    public DateTime? Inicio { get; set; }
    /// <summary>true = apagar as doses ainda não liberadas da referência (tratamento removido).</summary>
    public bool Cancelar { get; set; }
    public long? ReferenciaId { get; set; }
    public string? ReferenciaTipo { get; set; }
}
