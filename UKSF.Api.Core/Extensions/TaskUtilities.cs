namespace UKSF.Api.Core.Extensions;

public static class TaskUtilities
{
    public static async Task Delay(TimeSpan timeSpan, CancellationToken token)
    {
        try
        {
            await Task.Delay(timeSpan, token);
        }
        catch (Exception)
        {
            // Ignored
        }
    }

    public static async Task DelayWithCallback(TimeSpan timeSpan, CancellationToken token, Func<Task> callback)
    {
        try
        {
            await Task.Delay(timeSpan, token);
            await callback();
        }
        catch (Exception)
        {
            // Ignored
        }
    }
}
