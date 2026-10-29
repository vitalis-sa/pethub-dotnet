using System.Text.Json;
using System.Text.Json.Serialization;
using Vitalis.Configuracao;
using Vitalis.Doses;
using Vitalis.Models;
using Vitalis.Observabilidade;
using Vitalis.Repositories;

namespace Vitalis.Mensageria;

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

/// <summary>Transforma a mensagem do pethub-java em um lembrete, sem criar duas vezes o mesmo.</summary>
public class ProcessadorDeLembrete(
    IResponsavelRepository responsaveis,
    ILembreteRepository lembretes,
    ILogger<ProcessadorDeLembrete> logger) : IProcessadorDeLembrete
{
    private static readonly JsonSerializerOptions Opcoes = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public ResultadoDaMensagem Processar(ReadOnlySpan<byte> corpo)
    {
        var dto = Ler(corpo);
        if (dto is null || !Valido(dto))
        {
            AplicacaoMetricas.LembretesCriadosContador.Add(1, new KeyValuePair<string, object?>("status", "erro_validacao"));
            logger.LogWarning("Mensagem de lembrete inválida descartada.");
            return ResultadoDaMensagem.Descartar;
        }

        try
        {
            if (!responsaveis.Existe(dto.ResponsavelId))
            {
                logger.LogWarning("Lembrete descartado: responsável {ResponsavelId} não existe.", dto.ResponsavelId);
                return ResultadoDaMensagem.Descartar;
            }

            if (dto.Cancelar)
            {
                var removidas = dto.ReferenciaId is null
                    ? 0
                    : lembretes.RemoverDosesPendentes(dto.ResponsavelId, dto.ReferenciaTipo ?? "VacinaTratamento", dto.ReferenciaId.Value);
                logger.LogInformation("{Removidas} dose(s) pendente(s) cancelada(s) do tratamento {ReferenciaId}.", removidas, dto.ReferenciaId);
                return ResultadoDaMensagem.Confirmar;
            }

            if (EhTratamento(dto))
            {
                GravarDoses(dto);
                return ResultadoDaMensagem.Confirmar;
            }

            // Mesma consulta de novo: ou é a mensagem repetida (nada muda) ou a consulta foi
            // remarcada (a data e o texto mudam). Nos dois casos atualiza em vez de criar outro.
            if (Atualizou(dto))
            {
                logger.LogInformation("Lembrete de {ReferenciaTipo} {ReferenciaId} já existia; atualizado.",
                    dto.ReferenciaTipo, dto.ReferenciaId);
                return ResultadoDaMensagem.Confirmar;
            }

            lembretes.Add(new Lembrete
            {
                ResponsavelId = dto.ResponsavelId,
                PetId = dto.PetId,
                Tipo = dto.Tipo,
                DataAgendada = dto.DataAgendada,
                Mensagem = dto.Mensagem,
                ReferenciaId = dto.ReferenciaId,
                ReferenciaTipo = dto.ReferenciaTipo
            });
            AplicacaoMetricas.LembretesCriadosContador.Add(1, new KeyValuePair<string, object?>("status", "sucesso"));
            logger.LogInformation("Lembrete {Tipo} criado para o responsável {ResponsavelId}.", dto.Tipo, dto.ResponsavelId);
            return ResultadoDaMensagem.Confirmar;
        }
        catch (Exception ex)
        {
            AplicacaoMetricas.LembretesCriadosContador.Add(1, new KeyValuePair<string, object?>("status", "erro_interno"));
            logger.LogError(ex, "Falha ao gravar o lembrete do responsável {ResponsavelId}; será tentado de novo.", dto.ResponsavelId);
            return ResultadoDaMensagem.Repetir;
        }
    }

    private static bool EhTratamento(CriarLembreteDto dto)
        => dto.Tipo == TipoLembrete.MEDICAMENTO && dto.VezesPorDia is not null && dto.DuracaoDias is not null;

    /// <summary>Uma receita com frequência e duração vira um lembrete por dose, nos horários da agenda.</summary>
    private void GravarDoses(CriarLembreteDto dto)
    {
        var horarios = AgendaDeDoses.Calcular(dto.Inicio ?? Relogio.Agora(), dto.VezesPorDia!.Value, dto.DuracaoDias!.Value);
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
                ReferenciaTipo = dto.ReferenciaTipo
            })
            .ToList();

        lembretes.SubstituirDoses(dto.ResponsavelId, dto.ReferenciaTipo ?? "Medicamento", dto.ReferenciaId, doses);
        AplicacaoMetricas.LembretesCriadosContador.Add(doses.Count, new KeyValuePair<string, object?>("status", "sucesso"));
        logger.LogInformation("{Doses} lembrete(s) de dose criado(s) para o responsável {ResponsavelId}.", doses.Count, dto.ResponsavelId);
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
        => dto.ReferenciaId is not null
           && dto.ReferenciaTipo is not null
           && lembretes.AtualizarPorReferencia(dto.ResponsavelId, dto.ReferenciaTipo, dto.ReferenciaId.Value,
               dto.DataAgendada, dto.Mensagem);
}
