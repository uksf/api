namespace UKSF.Api.Shared.Extensions;

public static class ExceptionExtensions
{
    public static string GetCompleteString(this Exception exception)
    {
        return exception.InnerException is { } ? $"{exception}\n{exception.InnerException}" : exception.ToString();
    }

    public static string GetCompleteMessage(this Exception exception)
    {
        return exception.InnerException is { } ? $"{exception.Message}\n{exception.InnerException.Message}" : exception.Message;
    }
}
