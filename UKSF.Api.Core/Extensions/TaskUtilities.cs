namespace UKSF.Api.Core.Extensions;

public static class TaskUtilities
{
    public static async Task Delay(TimeSpan timeSpan, CancellationToken token)
    {
        try
        {
            await Task.Delay(timeSpan, token);
        }
        catch (OperationCanceledException)
        {
            // Expected when token is cancelled
        }
    }

    public static async Task DelayWithCallback(TimeSpan timeSpan, CancellationToken token, Func<Task> callback)
    {
        try
        {
            await Task.Delay(timeSpan, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await callback();
    }
}
