using Vitalis.Domain.Entidades;

namespace Vitalis.Application.Dtos;

/// <summary>Resultado do cadastro ou do login: o responsável e o JWT que ele vai usar no app.</summary>
public sealed record Sessao(Responsavel Responsavel, string Token);
