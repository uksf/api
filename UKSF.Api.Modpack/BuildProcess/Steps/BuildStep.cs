using System.Text.Json;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Core.Processes;

namespace UKSF.Api.Modpack.BuildProcess.Steps;

public interface IBuildStep
{
    void Init(
        IServiceProvider serviceProvider,
        IUksfLogger logger,
        DomainModpackBuild modpackBuild,
        ModpackBuildStep modpackBuildStep,
        Func<UpdateDefinition<DomainModpackBuild>, Task> buildUpdatedCallback,
        Func<Task> stepUpdatedCallback,
        CancellationTokenSource cancellationTokenSource
    );

    Task Start();
    bool CheckGuards();
    Task Setup();
    Task Process();
    Task Succeed();
    Task Fail(Exception exception);
    Task Cancel();
    void Warning(string message);
    Task Skip();
}

public class BuildStep : IBuildStep
{
    private const string ColourBlue = "#0c78ff";
    private readonly CancellationTokenSource _updatePusherCancellationTokenSource = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1);
    private ModpackBuildStep _buildStep;
    private Func<UpdateDefinition<DomainModpackBuild>, Task> _buildUpdatedCallback;
    private IUksfLogger _logger;
    private Func<Task> _stepUpdatedCallback;
    private TimeSpan _updateInterval;
    protected DomainModpackBuild Build;
    protected CancellationTokenSource CancellationTokenSource;
    protected IServiceProvider ServiceProvider;
    protected IStepLogger StepLogger;
    protected IVariablesService VariablesService;
    private IProcessCommandFactory _processCommandFactory;
    private IBuildProcessTracker _processTracker;

    public void Init(
        IServiceProvider newServiceProvider,
        IUksfLogger logger,
        DomainModpackBuild modpackBuild,
        ModpackBuildStep modpackBuildStep,
        Func<UpdateDefinition<DomainModpackBuild>, Task> buildUpdatedCallback,
        Func<Task> stepUpdatedCallback,
        CancellationTokenSource newCancellationTokenSource
    )
    {
        ServiceProvider = newServiceProvider;
        _logger = logger;
        VariablesService = ServiceProvider.GetService<IVariablesService>();
        _processCommandFactory = ServiceProvider.GetService<IProcessCommandFactory>();
        _processTracker = ServiceProvider.GetService<IBuildProcessTracker>();
        Build = modpackBuild;
        _buildStep = modpackBuildStep;
        _buildUpdatedCallback = buildUpdatedCallback;
        _stepUpdatedCallback = stepUpdatedCallback;
        CancellationTokenSource = newCancellationTokenSource;
        StepLogger = new StepLogger(_buildStep);

        var updateInterval = VariablesService.GetVariable("BUILD_STATE_UPDATE_INTERVAL").AsDouble();
        _updateInterval = TimeSpan.FromSeconds(updateInterval);
    }

    public async Task Start()
    {
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        StartUpdatePusher();
        _buildStep.Running = true;
        _buildStep.StartTime = DateTime.UtcNow;
        StepLogger.LogStart();
        await Update();
    }

    public virtual bool CheckGuards()
    {
        return true;
    }

    public async Task Setup()
    {
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        StepLogger.Log("\nSetup", ColourBlue);
        await SetupExecute();
        await Update();
    }

    public async Task Process()
    {
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
        StepLogger.Log("\nProcess", ColourBlue);
        await ProcessExecute();

        // Ensure all logs from the process execution are captured and persisted
        await Task.Delay(500, CancellationToken.None); // Give time for any async logging to complete
        await Update(); // Force an update to persist any pending logs

        // Re-check cancellation after update
        CancellationTokenSource.Token.ThrowIfCancellationRequested();
    }

    public async Task Succeed()
    {
        StepLogger.LogSuccess();
        if (_buildStep.BuildResult != ModpackBuildResult.Warning)
        {
            _buildStep.BuildResult = ModpackBuildResult.Success;
        }

        await Stop();
    }

    public async Task Fail(Exception exception)
    {
        StepLogger.LogError(exception);
        _buildStep.BuildResult = ModpackBuildResult.Failed;
        await Stop();
    }

    public async Task Cancel()
    {
        StepLogger.LogCancelled();
        _buildStep.BuildResult = ModpackBuildResult.Cancelled;
        await Stop();
    }

    public void Warning(string message)
    {
        StepLogger.LogWarning(message);
        _buildStep.BuildResult = ModpackBuildResult.Warning;
    }

    public async Task Skip()
    {
        StepLogger.LogSkipped();
        _buildStep.BuildResult = ModpackBuildResult.Skipped;
        await Stop();
    }

    protected virtual Task SetupExecute()
    {
        StepLogger.Log("---");
        return Task.CompletedTask;
    }

    protected virtual Task ProcessExecute()
    {
        StepLogger.Log("---");
        return Task.CompletedTask;
    }

    internal string GetBuildEnvironmentPath()
    {
        return GetEnvironmentPath(Build.Environment);
    }

    internal string GetEnvironmentPath(GameEnvironment environment)
    {
        return environment switch
        {
            GameEnvironment.Release     => VariablesService.GetVariable("MODPACK_PATH_RELEASE").AsString(),
            GameEnvironment.Rc          => VariablesService.GetVariable("MODPACK_PATH_RC").AsString(),
            GameEnvironment.Development => VariablesService.GetVariable("MODPACK_PATH_DEV").AsString(),
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }

    internal string GetServerEnvironmentPath(GameEnvironment environment)
    {
        return environment switch
        {
            GameEnvironment.Release     => VariablesService.GetVariable("SERVER_PATH_RELEASE").AsString(),
            GameEnvironment.Rc          => VariablesService.GetVariable("SERVER_PATH_RC").AsString(),
            GameEnvironment.Development => VariablesService.GetVariable("SERVER_PATH_DEV").AsString(),
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }

    internal string GetEnvironmentRepoName()
    {
        return Build.Environment switch
        {
            GameEnvironment.Release     => "UKSF",
            GameEnvironment.Rc          => "UKSF-Rc",
            GameEnvironment.Development => "UKSF-Dev",
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }

    internal string GetBuildSourcesPath()
    {
        return VariablesService.GetVariable("BUILD_PATH_SOURCES").AsString();
    }

    protected async Task<List<string>> RunProcess(
        string workingDirectory,
        string executable,
        string args,
        int timeout,
        bool log = false,
        bool suppressOutput = false,
        bool raiseErrors = true,
        bool errorSilently = false,
        List<string> errorExclusions = null,
        string ignoreErrorGateClose = "",
        string ignoreErrorGateOpen = ""
    )
    {
        List<string> results = [];
        var errorFilter = new ErrorFilter(
            new ProcessErrorHandlingConfig
            {
                ErrorExclusions = errorExclusions?.AsReadOnly() ?? (IReadOnlyList<string>) [],
                IgnoreErrorGateOpen = ignoreErrorGateOpen,
                IgnoreErrorGateClose = ignoreErrorGateClose
            }
        );

        var command = _processCommandFactory.CreateCommand(executable, workingDirectory, args)
                                            .WithProcessId(Build?.Id)
                                            .WithTimeout(TimeSpan.FromMilliseconds(timeout))
                                            .WithLogging(log)
                                            .WithProcessTracker(_processTracker);

        Exception delayedException = null;
        var processExitCode = 0;

        await foreach (var outputLine in command.ExecuteAsync(CancellationTokenSource.Token))
        {
            results.Add(outputLine.Content);

            // Handle output logging based on type and configuration
            switch (outputLine.Type)
            {
                case ProcessOutputType.Output:

                    // Only log standard output if not suppressed
                    if (!suppressOutput)
                    {
                        if (outputLine.IsJson && !string.IsNullOrEmpty(outputLine.Color))
                        {
                            StepLogger.Log(outputLine.Content, outputLine.Color);
                        }
                        else
                        {
                            StepLogger.Log(outputLine.Content);
                        }
                    }

                    break;

                case ProcessOutputType.Error:
                    // Always log error content, regardless of suppressOutput
                    var shouldIgnoreError = errorFilter.ShouldIgnoreError(outputLine.Content);

                    if (!shouldIgnoreError)
                    {
                        // Log the error content if not silently handling errors
                        if (!errorSilently)
                        {
                            StepLogger.LogError(outputLine.Exception ?? new Exception(outputLine.Content));
                        }

                        // Store exception for later throwing if raiseErrors is true
                        if (raiseErrors && outputLine.Exception != null)
                        {
                            delayedException = outputLine.Exception;
                        }
                    }

                    break;

                case ProcessOutputType.ProcessCompleted:
                    // Capture the exit code for later processing
                    processExitCode = outputLine.ExitCode; break;

                case ProcessOutputType.ProcessCancelled:
                    // Process was cancelled - this should trigger an OperationCanceledException
                    // to be handled by the BuildProcessorService's cancellation logic
                    throw new OperationCanceledException("Process execution was cancelled", outputLine.Exception);
            }
        }

        if (processExitCode != 0 && raiseErrors)
        {
            var exitCodeException = new Exception($"Process failed with exit code {processExitCode}");
            if (!errorSilently)
            {
                StepLogger.LogError(exitCodeException);
            }

            throw exitCodeException;
        }

        // Throw delayed exception after all output has been collected and logged
        if (delayedException != null)
        {
            throw delayedException;
        }

        return results;
    }

    internal void SetEnvironmentVariable(string key, object value)
    {
        Build.EnvironmentVariables[key] = value;
        _buildUpdatedCallback(Builders<DomainModpackBuild>.Update.Set(x => x.EnvironmentVariables, Build.EnvironmentVariables));
    }

    internal T GetEnvironmentVariable<T>(string key)
    {
        if (Build.EnvironmentVariables.TryGetValue(key, out var variable))
        {
            return (T)variable;
        }

        return default;
    }

    private void StartUpdatePusher()
    {
        _ = Task.Run(
            async () =>
            {
                try
                {
                    var previousBuildStepState = CreateBuildStepSnapshot();

                    while (!_updatePusherCancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(_updateInterval, _updatePusherCancellationTokenSource.Token);

                            if (_updatePusherCancellationTokenSource.IsCancellationRequested)
                            {
                                return;
                            }

                            var newBuildStepState = CreateBuildStepSnapshot();
                            if (newBuildStepState != previousBuildStepState)
                            {
                                await Update();
                                previousBuildStepState = newBuildStepState;
                            }

                            await Task.Yield();
                        }
                        catch (OperationCanceledException) when (_updatePusherCancellationTokenSource.IsCancellationRequested)
                        {
                            // Expected cancellation - exit gracefully
                            return;
                        }
                        catch (Exception ex)
                        {
                            // Log error but continue the update loop
                            _logger?.LogWarning($"Update pusher encountered error: {ex.Message}");

                            // Wait a bit before retrying to avoid tight error loops
                            try
                            {
                                await Task.Delay(TimeSpan.FromSeconds(5), _updatePusherCancellationTokenSource.Token);
                            }
                            catch (OperationCanceledException)
                            {
                                return;
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected cancellation - exit gracefully
                }
                catch (Exception ex)
                {
                    // Log unexpected errors
                    _logger?.LogError($"Update pusher failed unexpectedly: {ex.Message}", ex);
                }
            },
            CancellationToken.None // Don't use the cancellation token for Task.Run itself
        );
    }

    private string CreateBuildStepSnapshot()
    {
        try
        {
            // Create a snapshot of the build step to avoid collection modification during serialization
            var snapshot = new
            {
                _buildStep.BuildResult,
                _buildStep.EndTime,
                _buildStep.Finished,
                _buildStep.Index,
                _buildStep.Name,
                _buildStep.Running,
                _buildStep.StartTime,
                LogsCount = _buildStep.Logs?.Count ?? 0,
                LastLogText = _buildStep.Logs?.LastOrDefault()?.Text ?? ""
            };

            return JsonSerializer.Serialize(snapshot, DefaultJsonSerializerOptions.Options);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Failed to create build step snapshot: {ex.Message}");
            // Return a simple state representation as fallback
            return $"{_buildStep.Running}_{_buildStep.Finished}_{_buildStep.BuildResult}_{_buildStep.Logs?.Count ?? 0}";
        }
    }

    private void StopUpdatePusher()
    {
        _updatePusherCancellationTokenSource.Cancel();
    }

    private async Task Update()
    {
        try
        {
            // Use a timeout to prevent hanging indefinitely
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(CancellationTokenSource.Token, timeoutCts.Token);

            await _updateSemaphore.WaitAsync(combinedCts.Token);
            try
            {
                await _stepUpdatedCallback();
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }
        catch (OperationCanceledException) when (CancellationTokenSource.Token.IsCancellationRequested)
        {
            // Build was cancelled - log warning but don't re-throw to allow final state persistence
            _logger?.LogWarning("Build step update was cancelled - this may be expected during build cancellation");
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred - log warning but don't fail the build
            _logger?.LogWarning("Build step update timed out after 30 seconds - continuing with build");
        }
        catch (Exception ex)
        {
            // Log error but don't fail the build for update issues
            _logger?.LogWarning($"Build step update failed: {ex.Message}");
        }
    }

    private async Task Stop()
    {
        _buildStep.Running = false;
        _buildStep.Finished = true;
        _buildStep.EndTime = DateTime.UtcNow;

        StopUpdatePusher();

        // Force a final update to ensure the step state is persisted
        try
        {
            await FinalUpdate();
        }
        catch (Exception ex)
        {
            // Log but don't fail the step for update issues
            _logger?.LogWarning($"Failed to update step state during stop: {ex.Message}");
        }
    }

    private async Task FinalUpdate()
    {
        try
        {
            // Use a timeout to prevent hanging indefinitely, but don't use cancellation token
            // This ensures final state is always persisted even during cancellation
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));

            await _updateSemaphore.WaitAsync(timeoutCts.Token);
            try
            {
                await _stepUpdatedCallback();
            }
            finally
            {
                _updateSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            // Timeout occurred - log warning but don't fail the build
            _logger?.LogWarning("Final build step update timed out after 30 seconds");
        }
        catch (Exception ex)
        {
            // Log error but don't fail the build for update issues
            _logger?.LogWarning($"Final build step update failed: {ex.Message}");
        }
    }
}
