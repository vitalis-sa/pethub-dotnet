using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Vitalis.Application.Abstracoes;
using Vitalis.Application.Dtos;
using Vitalis.Application.Historico;
using Vitalis.Application.Observabilidade;
using Vitalis.Domain.Entidades;
using Vitalis.Domain.Enums;
using Vitalis.Domain.Servicos;

namespace Vitalis.Application.Mensageria;

public enum ResultadoDaMensagem
{
    /// <summary>Tratada (criada ou já existente): confirmar à fila.</summary>
    Confirmar,
    /// <summary>Inválida e sem como ser corrigida: descartar, senão volta para sempre.</summary>
    Descartar,
    /// <summary>Falha passageira (banco fora do ar, por exemplo): devolver à fila.</summary>
    Repetir
}

public interface IProcessadorDeLembrete
{
    ResultadoDaMensagem Processar(ReadOnlySpan<byte> corpo);
}

/// <summary>
/// Transforma a mensagem do pethub-java em um lembrete, sem criar duas vezes o mesmo, e registra no histórico
/// (MongoDB) o que foi feito com cada mensagem.
/// </summary>
public class ProcessadorDeLembrete(
    IResponsavelRepository responsaveis,
    ILembreteRepository lembretes,
    IHistoricoDeMensagens historico,
    IRelogio relogio,
    ILogger<ProcessadorDeLembrete> logger) : IProcessadorDeLembrete
{
    private const int TamanhoMaximoDoCorpo = 4000;

    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ResultadoDaMensagem Processar(ReadOnlySpan<byte> corpo)
    {
        var dto = Ler(corpo);
        var (resultado, motivo) = Tratar(dto);
        RegistrarNoHistorico(dto, corpo, resultado, motivo);
        return resultado;
    }

    private (ResultadoDaMensagem Resultado, string Motivo) Tratar(CriarLembreteDto? dto)
    {
        if (dto is null || !Valido(dto))
        {
            AplicacaoMetricas.LembretesCriadosContador.Add(1, new KeyValuePair<string, object?>("status", "erro_validacao"));
            logger.LogWarning("Mensagem de lembrete inválida descartada.");
            return (ResultadoDaMensagem.Descartar, "mensagem inválida");
        }

        try
        {
            if (!responsaveis.Existe(dto.ResponsavelId))
            {
                logger.LogWarning("Lembrete descartado: responsável {ResponsavelId} não existe.", dto.ResponsavelId);
                return (ResultadoDaMensagem.Descartar, "responsável inexistente");
            }

            if (dto.Cancelar)
            {
                var removidas = CancelarDoses(dto);
                logger.LogInformation("{Removidas} dose(s) pendente(s) cancelada(s) do tratamento {ReferenciaId}.", removidas, dto.ReferenciaId);
                return (ResultadoDaMensagem.Confirmar, $"{removidas} dose(s) cancelada(s)");
            }

            if (EhTratamento(dto))
            {
                var doses = GravarDoses(dto);
                return (ResultadoDaMensagem.Confirmar, $"{doses} dose(s) agendada(s)");
            }

            // Mesma consulta de novo: ou é a mensagem repetida (nada muda) ou a consulta foi
            // remarcada (a data e o texto mudam). Nos dois casos atualiza em vez de criar outro.
            if (Atualizou(dto))
            {
                logger.LogInformation("Lembrete de {ReferenciaTipo} {ReferenciaId} já existia; atualizado.",
                    dto.ReferenciaTipo, dto.ReferenciaId);
                return (ResultadoDaMensagem.Confirmar, "lembrete atualizado");
            }

            lembretes.Adicionar(new Lembrete
            {
                ResponsavelId = dto.ResponsavelId,
                PetId = dto.PetId,
                Tipo = dto.Tipo,
                DataAgendada = dto.DataAgendada,
                Mensagem = dto.Mensagem,
                ReferenciaId = dto.ReferenciaId,
                ReferenciaTipo = dto.ReferenciaTipo,
                Status = StatusLembrete.PENDENTE,
                CreatedAt = relogio.Agora()
            });
            lembretes.SalvarAlteracoes();
            AplicacaoMetricas.LembretesCriadosContador.Add(1, new KeyValuePair<string, object?>("status", "sucesso"));
            logger.LogInformation("Lembrete {Tipo} criado para o responsável {ResponsavelId}.", dto.Tipo, dto.ResponsavelId);
            return (ResultadoDaMensagem.Confirmar, "lembrete criado");
        }
        catch (Exception ex)
        {
            AplicacaoMetricas.LembretesCriadosContador.Add(1, new KeyValuePair<string, object?>("status", "erro_interno"));
            logger.LogError(ex, "Falha ao gravar o lembrete do responsável {ResponsavelId}; será tentado de novo.", dto.ResponsavelId);
            return (ResultadoDaMensagem.Repetir, $"falha ao gravar ({ex.GetType().Name})");
        }
    }

    /// <summary>
    /// O histórico é auditoria: se o MongoDB falhar, a mensagem segue o caminho decidido acima mesmo assim
    /// (não faz sentido devolver à fila um lembrete já gravado no Oracle só porque o log não foi escrito).
    /// </summary>
    private void RegistrarNoHistorico(CriarLembreteDto? dto, ReadOnlySpan<byte> corpo, ResultadoDaMensagem resultado, string motivo)
    {
        if (!historico.Habilitado)
            return;

        var texto = Encoding.UTF8.GetString(corpo);
        try
        {
            historico.Registrar(new RegistroDeMensagem
            {
                RecebidaEm = DateTime.UtcNow,
                ResponsavelId = dto?.ResponsavelId > 0 ? dto.ResponsavelId : null,
                PetId = dto?.PetId > 0 ? dto.PetId : null,
                Tipo = dto is not null && Enum.IsDefined(dto.Tipo) ? dto.Tipo.ToString() : null,
                ReferenciaTipo = dto?.ReferenciaTipo,
                ReferenciaId = dto?.ReferenciaId,
                Resultado = resultado.ToString(),
                Motivo = motivo,
                Corpo = texto.Length > TamanhoMaximoDoCorpo ? texto[..TamanhoMaximoDoCorpo] : texto
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Não foi possível registrar a mensagem no histórico (MongoDB).");
        }
    }

    private static bool EhTratamento(CriarLembreteDto dto)
        => dto.Tipo == TipoLembrete.MEDICAMENTO && dto.VezesPorDia is not null && dto.DuracaoDias is not null;

    /// <summary>Tratamento removido no Java: apaga as doses que ainda não foram liberadas.</summary>
    private int CancelarDoses(CriarLembreteDto dto)
    {
        if (dto.ReferenciaId is null)
            return 0;

        var pendentes = lembretes.ListarDosesPendentes(dto.ResponsavelId, dto.ReferenciaTipo ?? "VacinaTratamento", dto.ReferenciaId.Value);
        lembretes.RemoverVarios(pendentes);
        lembretes.SalvarAlteracoes();
        return pendentes.Count;
    }

    /// <summary>
    /// Uma receita com frequência e duração vira um lembrete por dose, nos horários da agenda. Se a mesma receita
    /// chegar de novo (alterada ou repetida), as doses ainda não liberadas são trocadas pelas novas, sem duplicar.
    /// </summary>
    private int GravarDoses(CriarLembreteDto dto)
    {
        var agora = relogio.Agora();
        var horarios = AgendaDeDoses.Calcular(dto.Inicio ?? agora, dto.VezesPorDia!.Value, dto.DuracaoDias!.Value);
        var doses = horarios
            .Select((horario, i) => new Lembrete
            {
                ResponsavelId = dto.ResponsavelId,
                PetId = dto.PetId,
                Tipo = dto.Tipo,
                DataAgendada = DateOnly.FromDateTime(horario),
                DataHora = horario,
                Mensagem = $"{dto.Mensagem} (dose {i + 1} de {horarios.Count})",
                ReferenciaId = dto.ReferenciaId,
                ReferenciaTipo = dto.ReferenciaTipo,
                Status = StatusLembrete.PENDENTE,
                CreatedAt = agora
            })
            .ToList();

        if (dto.ReferenciaId is not null)
            lembretes.RemoverVarios(lembretes.ListarDosesPendentes(
                dto.ResponsavelId, dto.ReferenciaTipo ?? "Medicamento", dto.ReferenciaId.Value));

        lembretes.AdicionarVarios(doses);
        lembretes.SalvarAlteracoes();
        AplicacaoMetricas.LembretesCriadosContador.Add(doses.Count, new KeyValuePair<string, object?>("status", "sucesso"));
        logger.LogInformation("{Doses} lembrete(s) de dose criado(s) para o responsável {ResponsavelId}.", doses.Count, dto.ResponsavelId);
        return doses.Count;
    }

    private static CriarLembreteDto? Ler(ReadOnlySpan<byte> corpo)
    {
        try { return JsonSerializer.Deserialize<CriarLembreteDto>(corpo, Opcoes); }
        catch (JsonException) { return null; }
    }

    private static bool Valido(CriarLembreteDto dto)
        => dto.ResponsavelId > 0
           && !string.IsNullOrWhiteSpace(dto.Mensagem)
           && Enum.IsDefined(dto.Tipo)
           && (dto.VezesPorDia is null && dto.DuracaoDias is null
               || dto.VezesPorDia is not null && dto.DuracaoDias is not null
                  && AgendaDeDoses.Valida(dto.VezesPorDia.Value, dto.DuracaoDias.Value));

    private bool Atualizou(CriarLembreteDto dto)
    {
        if (dto.ReferenciaId is null || dto.ReferenciaTipo is null)
            return false;

        var existente = lembretes.ObterPorReferencia(dto.ResponsavelId, dto.ReferenciaTipo, dto.ReferenciaId.Value);
        if (existente is null)
            return false;

        existente.Reagendar(dto.DataAgendada, dto.Mensagem);
        lembretes.SalvarAlteracoes();
        return true;
    }
}
