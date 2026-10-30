using Vitalis.Domain.Enums;

namespace Vitalis.Application.Consultas;

/// <summary>Filtros de <c>GET /api/lembretes/responsavel/{id}</c>. Todos opcionais e combináveis.</summary>
public class FiltroDeLembretes
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["dataAgendada", "tipo", "status", "id"];

    public TipoLembrete? Tipo { get; set; }
    public StatusLembrete? Status { get; set; }
    public long? PetId { get; set; }
    /// <summary>Data agendada a partir de (inclusive), <c>yyyy-MM-dd</c>.</summary>
    public DateOnly? De { get; set; }
    /// <summary>Data agendada até (inclusive), <c>yyyy-MM-dd</c>.</summary>
    public DateOnly? Ate { get; set; }
}

/// <summary>Filtros de <c>GET /api/responsavel/{id}/enderecos</c>.</summary>
public class FiltroDeEnderecos
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["id", "cidade", "estado", "bairro", "logradouro"];

    /// <summary>Parte do nome da cidade, sem diferenciar maiúsculas.</summary>
    public string? Cidade { get; set; }
    /// <summary>UF com duas letras.</summary>
    public string? Estado { get; set; }
    public bool? Principal { get; set; }
}

/// <summary>Filtros de <c>GET /api/responsavel/{id}/contatos</c>.</summary>
public class FiltroDeContatos
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["id", "tipo", "telefone"];

    /// <summary><c>CELULAR</c> ou <c>TELEFONE</c>.</summary>
    public string? Tipo { get; set; }
    public bool? Principal { get; set; }
}

/// <summary>Filtros de <c>GET /api/lembretes/responsavel/{id}/historico</c> (MongoDB).</summary>
public class FiltroDeHistorico
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["recebidaEm", "resultado", "tipo"];

    /// <summary><c>Confirmar</c>, <c>Descartar</c> ou <c>Repetir</c>.</summary>
    public string? Resultado { get; set; }
    /// <summary>Tipo do lembrete da mensagem (<c>VACINA</c>, <c>CONSULTA</c>...).</summary>
    public string? Tipo { get; set; }
    /// <summary>Recebidas a partir de (UTC).</summary>
    public DateTime? De { get; set; }
    /// <summary>Recebidas até (UTC).</summary>
    public DateTime? Ate { get; set; }
}
