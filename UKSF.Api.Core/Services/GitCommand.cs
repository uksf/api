namespace UKSF.Api.Core.Services;

public class GitCommand(IGitService gitService, string workingDirectory)
{
    private readonly IGitService _gitService = gitService;
    private readonly string _workingDirectory = workingDirectory;

    public async Task Execute(string command, bool ignoreErrors = false, CancellationToken cancellationToken = default)
    {
        try
        {
            await _gitService.ExecuteCommand(_workingDirectory, command, cancellationToken);
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
    }
}
