namespace AutoPecas.Api.Domain;

/// <summary>Violação de regra de negócio. Vira uma resposta HTTP com a mensagem para o usuário.</summary>
public class RegraNegocioException(string mensagem, int statusCode = StatusCodes.Status422UnprocessableEntity)
    : Exception(mensagem)
{
    public int StatusCode { get; } = statusCode;

    public static RegraNegocioException NaoEncontrado(string mensagem) =>
        new(mensagem, StatusCodes.Status404NotFound);
}
