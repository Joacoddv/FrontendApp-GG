namespace GastroGestionBlazor.Contracts.Common;

public sealed record ProblemDetailsResponse(
    string? Title,
    string? Detail,
    int? Status);
