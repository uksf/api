﻿using System.Text.Json;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Core;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Services;
using UKSF.Api.Modpack.Models;

namespace UKSF.Api.Modpack.BuildProcess.Steps;

public interface IBuildStep
{
    void Init(
        IServiceProvider serviceProvider,
        IUksfLogger logger,
        DomainModpackBuild modpackBuild,
        ModpackBuildStep modpackBuildStep,
        Func<UpdateDefinition<DomainModpackBuild>, Task> buildUpdateCallback,
        Func<Task> stepUpdateCallback,
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
    private IUksfLogger _logger;
    private IBuildProcessHelperFactory _processHelperFactory;
    private Func<UpdateDefinition<DomainModpackBuild>, Task> _updateBuildCallback;
    private TimeSpan _updateInterval;
    private Func<Task> _updateStepCallback;
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
        Func<UpdateDefinition<DomainModpackBuild>, Task> buildUpdateCallback,
        Func<Task> stepUpdateCallback,
        CancellationTokenSource newCancellationTokenSource
    )
    {
        ServiceProvider = newServiceProvider;
        _logger = logger;
        VariablesService = ServiceProvider.GetService<IVariablesService>();
        _processHelperFactory = ServiceProvider.GetService<IBuildProcessHelperFactory>();
        Build = modpackBuild;
        _buildStep = modpackBuildStep;
        _updateBuildCallback = buildUpdateCallback;
        _updateStepCallback = stepUpdateCallback;
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
        await Task.Delay(500, CancellationTokenSource.Token); // Give time for any async logging to complete
        await Update(); // Force an update to persist any pending logs
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

    internal void SetEnvironmentVariable(string key, object value)
    {
        Build.EnvironmentVariables[key] = value;
        _updateBuildCallback(Builders<DomainModpackBuild>.Update.Set(x => x.EnvironmentVariables, Build.EnvironmentVariables));
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
        try
        {
            _ = Task.Run(
                async () =>
                {
                    var previousBuildStepState = JsonSerializer.Serialize(_buildStep, DefaultJsonSerializerOptions.Options);

                    do
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
                    while (!_updatePusherCancellationTokenSource.IsCancellationRequested);
                },
                _updatePusherCancellationTokenSource.Token
            );
        }
        catch (OperationCanceledException)
        {
            Console.Out.WriteLine("cancelled");
        }
        catch (Exception exception)
        {
            Console.Out.WriteLine(exception);
        }
    }

    private void StopUpdatePusher()
    {
        _updatePusherCancellationTokenSource.Cancel();
    }

    private async Task Update()
    {
        await _updateSemaphore.WaitAsync();
        await _updateStepCallback();
        _updateSemaphore.Release();
    }

    private async Task Stop()
    {
        _buildStep.Running = false;
        _buildStep.Finished = true;
        _buildStep.EndTime = DateTime.UtcNow;

        // Ensure final state is persisted before stopping update pusher
        await Update();
        StopUpdatePusher();

        // One final update after stopping the pusher to ensure completion state is saved
        await Update();
    }
}
