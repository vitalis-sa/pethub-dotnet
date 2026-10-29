using System.Security.Claims;

namespace Vitalis.Seguranca;

public static class UsuarioAutenticado
{
    /// <summary>Id do responsável no token ("sub"), ou null se não há token válido.</summary>
    public static long? ResponsavelId(this ClaimsPrincipal usuario)
        => long.TryParse(usuario.FindFirst("sub")?.Value, out var id) ? id : null;

    public static bool EhOResponsavel(this ClaimsPrincipal usuario, long responsavelId)
        => usuario.ResponsavelId() == responsavelId;
}
