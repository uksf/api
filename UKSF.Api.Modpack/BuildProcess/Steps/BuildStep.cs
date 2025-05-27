using System.Text.Json;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.BuildProcess.Modern;
using UKSF.Api.Modpack.Models;

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
    private BuilderProcessExecutor _processExecutor;
    private IBuildProcessHelperFactory _processHelperFactory;
    private Func<Task> _stepUpdatedCallback;
    private TimeSpan _updateInterval;
    protected DomainModpackBuild Build;
    protected CancellationTokenSource CancellationTokenSource;
    protected IServiceProvider ServiceProvider;
    protected IStepLogger StepLogger;
    protected IVariablesService VariablesService;

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
        _processHelperFactory = ServiceProvider.GetService<IBuildProcessHelperFactory>();
        _processExecutor = ServiceProvider.GetService<BuilderProcessExecutor>();
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

    protected List<string> RunProcess(
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
        using var processHelper = _processHelperFactory.Create(
            StepLogger,
            _logger,
            CancellationTokenSource,
            suppressOutput,
            raiseErrors,
            errorSilently,
            errorExclusions,
            ignoreErrorGateClose,
            ignoreErrorGateOpen,
            Build?.Id
        );
        return processHelper.Run(workingDirectory, executable, args, timeout, log);
    }

    protected async Task<List<string>> RunProcessModern(
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
        var results = new List<string>();
        var errorFilter = new ErrorFilter(
            new ProcessErrorHandlingConfig
            {
                ErrorExclusions = errorExclusions?.AsReadOnly() ?? (IReadOnlyList<string>)[],
                IgnoreErrorGateOpen = ignoreErrorGateOpen,
                IgnoreErrorGateClose = ignoreErrorGateClose
            }
        );

        var command = _processExecutor.CreateCommand(executable, workingDirectory, args)
                                      .WithBuildId(Build?.Id)
                                      .WithTimeout(TimeSpan.FromMilliseconds(timeout))
                                      .WithLogging(log);

        Exception delayedException = null;

        await foreach (var outputLine in command.ExecuteAsync(CancellationTokenSource.Token))
        {
            // Collect all output for return value
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
            }
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
                    var previousBuildStepState = JsonSerializer.Serialize(_buildStep, DefaultJsonSerializerOptions.Options);

                    while (!_updatePusherCancellationTokenSource.IsCancellationRequested)
                    {
                        try
                        {
                            await Task.Delay(_updateInterval, _updatePusherCancellationTokenSource.Token);

                            if (_updatePusherCancellationTokenSource.IsCancellationRequested)
                            {
                                return;
                            }

                            var newBuildStepState = JsonSerializer.Serialize(_buildStep, DefaultJsonSerializerOptions.Options);
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
            using var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(
                CancellationTokenSource.Token, 
                timeoutCts.Token
            );

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
            // Build was cancelled - this is expected, don't log as error
            throw;
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

        try
        {
            // Ensure final state is persisted before stopping update pusher
            await Update();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Failed to update build step during stop: {ex.Message}");
        }

        StopUpdatePusher();

        try
        {
            // One final update after stopping the pusher to ensure completion state is saved
            await Update();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning($"Failed final update during build step stop: {ex.Message}");
        }
    }
}
