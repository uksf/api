namespace UKSF.Api.Core.Services;

public class GitCommand(IGitService gitService, string workingDirectory)
{
    private readonly IGitService _gitService = gitService;
    private readonly string _workingDirectory = workingDirectory;

    public async Task<string> Execute(string command, bool ignoreErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _gitService.ExecuteCommand(_workingDirectory, command, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }
        catch (OperationCanceledException)
        {
            // Always re-throw cancellation exceptions
            throw;
        }
        catch (Exception)
        {
            if (!ignoreErrors)
            {
                throw;
            }
        }

        return string.Empty;
    }
}
