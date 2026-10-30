using Microsoft.AspNetCore.Mvc;
using Vitalis.Models;

namespace Vitalis.Consultas;

/// <summary>Filtros de <c>GET /api/lembretes/responsavel/{id}</c>. Todos opcionais e combináveis.</summary>
public class FiltroDeLembretes
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["dataAgendada", "tipo", "status", "id"];

    [FromQuery(Name = "tipo")]
    public TipoLembrete? Tipo { get; set; }
    [FromQuery(Name = "status")]
    public StatusLembrete? Status { get; set; }
    [FromQuery(Name = "petId")]
    public long? PetId { get; set; }
    /// <summary>Data agendada a partir de (inclusive), <c>yyyy-MM-dd</c>.</summary>
    [FromQuery(Name = "de")]
    public DateOnly? De { get; set; }
    /// <summary>Data agendada até (inclusive), <c>yyyy-MM-dd</c>.</summary>
    [FromQuery(Name = "ate")]
    public DateOnly? Ate { get; set; }
}

/// <summary>Filtros de <c>GET /api/responsavel/{id}/enderecos</c>.</summary>
public class FiltroDeEnderecos
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["id", "cidade", "estado", "bairro", "logradouro"];

    /// <summary>Parte do nome da cidade, sem diferenciar maiúsculas.</summary>
    [FromQuery(Name = "cidade")]
    public string? Cidade { get; set; }
    /// <summary>UF com duas letras.</summary>
    [FromQuery(Name = "estado")]
    public string? Estado { get; set; }
    [FromQuery(Name = "principal")]
    public bool? Principal { get; set; }
}

/// <summary>Filtros de <c>GET /api/responsavel/{id}/contatos</c>.</summary>
public class FiltroDeContatos
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["id", "tipo", "telefone"];

    /// <summary><c>CELULAR</c> ou <c>TELEFONE</c>.</summary>
    [FromQuery(Name = "tipo")]
    public string? Tipo { get; set; }
    [FromQuery(Name = "principal")]
    public bool? Principal { get; set; }
}

/// <summary>Filtros de <c>GET /api/lembretes/responsavel/{id}/historico</c> (MongoDB).</summary>
public class FiltroDeHistorico
{
    public static readonly IReadOnlyCollection<string> CamposOrdenaveis = ["recebidaEm", "resultado", "tipo"];

    /// <summary><c>Confirmar</c>, <c>Descartar</c> ou <c>Repetir</c>.</summary>
    [FromQuery(Name = "resultado")]
    public string? Resultado { get; set; }
    /// <summary>Tipo do lembrete da mensagem (<c>VACINA</c>, <c>CONSULTA</c>...).</summary>
    [FromQuery(Name = "tipo")]
    public string? Tipo { get; set; }
    /// <summary>Recebidas a partir de (UTC).</summary>
    [FromQuery(Name = "de")]
    public DateTime? De { get; set; }
    /// <summary>Recebidas até (UTC).</summary>
    [FromQuery(Name = "ate")]
    public DateTime? Ate { get; set; }
}
