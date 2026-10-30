using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Vitalis.Historico;

/// <summary>
/// Documento da coleção <c>historico_mensagens</c>: cada mensagem que chegou do pethub-java pela fila, com o que
/// o Vitalis fez com ela. Fica no MongoDB porque é um registro de eventos, sem relação com as tabelas e com
/// formato livre (o corpo original é guardado como veio, mesmo quando inválido).
/// </summary>
public class RegistroDeMensagem
{
    [BsonId, BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    /// <summary>Instante do processamento, em UTC.</summary>
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RecebidaEm { get; set; }

    /// <summary>Nulo quando a mensagem nem pôde ser lida.</summary>
    public long? ResponsavelId { get; set; }
    public long? PetId { get; set; }
    public string? Tipo { get; set; }
    public string? ReferenciaTipo { get; set; }
    public long? ReferenciaId { get; set; }

    /// <summary><c>Confirmar</c>, <c>Descartar</c> ou <c>Repetir</c>: o que foi respondido à fila.</summary>
    public string Resultado { get; set; } = "";
    /// <summary>Por que: "lembrete criado", "responsável inexistente", "mensagem inválida"...</summary>
    public string Motivo { get; set; } = "";
    /// <summary>JSON original da mensagem (até 4.000 caracteres).</summary>
    public string Corpo { get; set; } = "";
}
