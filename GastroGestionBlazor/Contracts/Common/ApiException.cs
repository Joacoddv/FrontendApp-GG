namespace GastroGestionBlazor.Contracts.Common;

public sealed class ApiException : Exception
{
    public ApiException(string message) : base(message)
    {
    }
}
