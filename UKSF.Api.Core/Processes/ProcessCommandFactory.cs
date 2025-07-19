namespace UKSF.Api.Core.Processes;

public interface IProcessCommandFactory
{
    ProcessCommand CreateCommand(string executable, string workingDirectory, string arguments);
}

public class ProcessCommandFactory(IUksfLogger logger) : IProcessCommandFactory
{
    public ProcessCommand CreateCommand(string executable, string workingDirectory, string arguments)
    {
        return new ProcessCommand(logger, executable, workingDirectory, arguments);
    }
}
