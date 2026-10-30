using Microsoft.Extensions.Logging;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Consultas;
using Vitalis.Application.Historico;
using Vitalis.Application.Resultados;
using Vitalis.Domain.Entidades;
using Vitalis.Domain.Enums;

namespace Vitalis.Application.Servicos;

/// <summary>
/// Casos de uso dos lembretes. Eles nascem de mensagens do pethub-java (veja <c>ProcessadorDeLembrete</c>);
/// aqui o responsável consulta, muda o status e apaga os próprios.
/// </summary>
public interface IServicoDeLembretes
{
    Resultado<Pagina<Lembrete>> Listar(long responsavelId, FiltroDeLembretes filtro, ConsultaPaginada consulta);
    /// <summary>Próximas doses ainda não liberadas, para o app agendar as notificações locais.</summary>
    IReadOnlyList<Lembrete> ListarDosesFuturas(long responsavelId);
    /// <summary>O que o pethub-java mandou pela fila para este responsável e o que foi feito (MongoDB).</summary>
    Task<Resultado<Pagina<RegistroDeMensagem>>> ListarHistoricoAsync(
        long responsavelId, FiltroDeHistorico filtro, ConsultaPaginada consulta, CancellationToken ct = default);
    Resultado<Lembrete> Obter(long id, long? solicitanteId);
    Resultado AlterarStatus(long id, StatusLembrete status, long? solicitanteId);
    Resultado Remover(long id, long? solicitanteId);
    /// <summary>Libera (PENDENTE vira ENVIADO) as doses cujo horário já chegou. Devolve quantas.</summary>
    int LiberarDosesVencidas();
}

public class ServicoDeLembretes(
    ILembreteRepository repositorio,
    IHistoricoDeMensagens historico,
    IRelogio relogio,
    ILogger<ServicoDeLembretes> logger) : IServicoDeLembretes
{
    /// <summary>O iOS aceita até 64 notificações locais pendentes: não adianta devolver mais do que isso.</summary>
    public const int LimiteDeDosesFuturas = 60;

    private const string NaoEncontrado = "Lembrete não encontrado";
    private const string SemPermissao = "Você só pode acessar os seus próprios dados";

    public Resultado<Pagina<Lembrete>> Listar(long responsavelId, FiltroDeLembretes filtro, ConsultaPaginada consulta)
    {
        if (!consulta.OrdenacaoValida(FiltroDeLembretes.CamposOrdenaveis))
            return Erro.Invalido("ordenarPor",
                $"Ordene por um destes campos: {string.Join(", ", FiltroDeLembretes.CamposOrdenaveis)}.");

        if (filtro.De is not null && filtro.Ate is not null && filtro.De > filtro.Ate)
            return Erro.Invalido("de", "A data inicial não pode ser depois da final.");

        logger.LogInformation("Buscando lembretes do responsável {ResponsavelId} (página {Pagina}).",
            responsavelId, consulta.Pagina);

        var (itens, total) = repositorio.Consultar(responsavelId, filtro, consulta);
        return new Pagina<Lembrete>(itens, total);
    }

    public IReadOnlyList<Lembrete> ListarDosesFuturas(long responsavelId)
    {
        logger.LogInformation("Buscando doses futuras do responsável {ResponsavelId}.", responsavelId);
        return repositorio.ListarDosesFuturas(responsavelId, relogio.Agora(), LimiteDeDosesFuturas);
    }

    public async Task<Resultado<Pagina<RegistroDeMensagem>>> ListarHistoricoAsync(
        long responsavelId, FiltroDeHistorico filtro, ConsultaPaginada consulta, CancellationToken ct = default)
    {
        if (!historico.Habilitado)
            return Erro.Indisponivel("Histórico de mensagens indisponível: MongoDB não configurado.");

        if (!consulta.OrdenacaoValida(FiltroDeHistorico.CamposOrdenaveis))
            return Erro.Invalido("ordenarPor",
                $"Ordene por um destes campos: {string.Join(", ", FiltroDeHistorico.CamposOrdenaveis)}.");

        logger.LogInformation("Buscando histórico de mensagens do responsável {ResponsavelId} (página {Pagina}).",
            responsavelId, consulta.Pagina);

        return await historico.ConsultarAsync(responsavelId, filtro, consulta, ct);
    }

    public Resultado<Lembrete> Obter(long id, long? solicitanteId)
    {
        var lembrete = repositorio.ObterPorId(id);
        if (lembrete is null)
        {
            logger.LogWarning("Lembrete {LembreteId} não encontrado.", id);
            return Erro.NaoEncontrado(NaoEncontrado);
        }

        return lembrete.ResponsavelId == solicitanteId ? lembrete : Erro.Proibido(SemPermissao);
    }

    public Resultado AlterarStatus(long id, StatusLembrete status, long? solicitanteId)
    {
        var obtido = Obter(id, solicitanteId);
        if (!obtido.Sucesso)
            return obtido.Erro!;

        obtido.Valor.AlterarStatus(status);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("Status do lembrete {LembreteId} atualizado para {StatusLembrete}.", id, status);
        return Resultado.Ok();
    }

    public Resultado Remover(long id, long? solicitanteId)
    {
        var obtido = Obter(id, solicitanteId);
        if (!obtido.Sucesso)
            return obtido.Erro!;

        repositorio.Remover(obtido.Valor);
        repositorio.SalvarAlteracoes();

        logger.LogInformation("Lembrete {LembreteId} removido com sucesso.", id);
        return Resultado.Ok();
    }

    public int LiberarDosesVencidas()
    {
        var vencidas = repositorio.ListarDosesVencidas(relogio.Agora());
        foreach (var dose in vencidas)
            dose.Liberar();

        repositorio.SalvarAlteracoes();

        if (vencidas.Count > 0)
            logger.LogInformation("{Quantidade} lembrete(s) de dose liberado(s).", vencidas.Count);

        return vencidas.Count;
    }
}
