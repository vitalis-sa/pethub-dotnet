using Vitalis.Application.Consultas;

namespace Vitalis.Application.Historico;

/// <summary>Repositório do histórico de mensagens da fila (MongoDB).</summary>
public interface IHistoricoDeMensagens
{
    /// <summary>False quando o MongoDB não está configurado: nada é gravado e as consultas não estão disponíveis.</summary>
    bool Habilitado { get; }

    void Registrar(RegistroDeMensagem registro);

    Task<Pagina<RegistroDeMensagem>> ConsultarAsync(
        long responsavelId, FiltroDeHistorico filtro, ConsultaPaginada consulta, CancellationToken ct = default);
}
