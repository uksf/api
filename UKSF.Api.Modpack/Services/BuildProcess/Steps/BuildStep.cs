using System.Text.Json;
using MongoDB.Driver;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.Modpack.Models;
using UKSF.Api.Shared;
using UKSF.Api.Shared.Extensions;
using UKSF.Api.Shared.Services;

namespace UKSF.Api.Modpack.Services.BuildProcess.Steps;

public interface IBuildStep
{
    void Init(
        IServiceProvider serviceProvider,
        ModpackBuild modpackBuild,
        ModpackBuildStep modpackBuildStep,
        Func<UpdateDefinition<ModpackBuild>, Task> buildUpdateCallback,
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
    private readonly TimeSpan _updateInterval = TimeSpan.FromSeconds(2);
    private readonly CancellationTokenSource _updatePusherCancellationTokenSource = new();
    private readonly SemaphoreSlim _updateSemaphore = new(1);
    private ModpackBuildStep _buildStep;
    private Func<UpdateDefinition<ModpackBuild>, Task> _updateBuildCallback;
    private Func<Task> _updateStepCallback;
    protected ModpackBuild Build;
    protected CancellationTokenSource CancellationTokenSource;
    protected IServiceProvider ServiceProvider;
    protected IStepLogger StepLogger;
    protected IVariablesService VariablesService;

    public void Init(
        IServiceProvider newServiceProvider,
        ModpackBuild modpackBuild,
        ModpackBuildStep modpackBuildStep,
        Func<UpdateDefinition<ModpackBuild>, Task> buildUpdateCallback,
        Func<Task> stepUpdateCallback,
        CancellationTokenSource newCancellationTokenSource
    )
    {
        ServiceProvider = newServiceProvider;
        VariablesService = ServiceProvider.GetService<IVariablesService>();
        Build = modpackBuild;
        _buildStep = modpackBuildStep;
        _updateBuildCallback = buildUpdateCallback;
        _updateStepCallback = stepUpdateCallback;
        CancellationTokenSource = newCancellationTokenSource;
        StepLogger = new StepLogger(_buildStep);
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
        await Update();
    }

    public async Task Succeed()
    {
        StepLogger.LogSuccess();
        if (_buildStep.BuildResult != ModpackBuildResult.WARNING)
        {
            _buildStep.BuildResult = ModpackBuildResult.SUCCESS;
        }

        await Stop();
    }

    public async Task Fail(Exception exception)
    {
        StepLogger.LogError(exception);
        _buildStep.BuildResult = ModpackBuildResult.FAILED;
        await Stop();
    }

    public async Task Cancel()
    {
        StepLogger.LogCancelled();
        _buildStep.BuildResult = ModpackBuildResult.CANCELLED;
        await Stop();
    }

    public void Warning(string message)
    {
        StepLogger.LogWarning(message);
        _buildStep.BuildResult = ModpackBuildResult.WARNING;
    }

    public async Task Skip()
    {
        StepLogger.LogSkipped();
        _buildStep.BuildResult = ModpackBuildResult.SKIPPED;
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
            GameEnvironment.RELEASE     => VariablesService.GetVariable("MODPACK_PATH_RELEASE").AsString(),
            GameEnvironment.RC          => VariablesService.GetVariable("MODPACK_PATH_RC").AsString(),
            GameEnvironment.DEVELOPMENT => VariablesService.GetVariable("MODPACK_PATH_DEV").AsString(),
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }

    internal string GetServerEnvironmentPath(GameEnvironment environment)
    {
        return environment switch
        {
            GameEnvironment.RELEASE     => VariablesService.GetVariable("SERVER_PATH_RELEASE").AsString(),
            GameEnvironment.RC          => VariablesService.GetVariable("SERVER_PATH_RC").AsString(),
            GameEnvironment.DEVELOPMENT => VariablesService.GetVariable("SERVER_PATH_DEV").AsString(),
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }

    internal string GetEnvironmentRepoName()
    {
        return Build.Environment switch
        {
            GameEnvironment.RELEASE     => "UKSF",
            GameEnvironment.RC          => "UKSF-Rc",
            GameEnvironment.DEVELOPMENT => "UKSF-Dev",
            _                           => throw new ArgumentException("Invalid build environment")
        };
    }

    internal string GetBuildSourcesPath()
    {
        return VariablesService.GetVariable("BUILD_PATH_SOURCES").AsString();
    }

    internal void SetEnvironmentVariable(string key, object value)
    {
        Build.EnvironmentVariables[key] = value;
        _updateBuildCallback(Builders<ModpackBuild>.Update.Set(x => x.EnvironmentVariables, Build.EnvironmentVariables));
    }

    internal T GetEnvironmentVariable<T>(string key)
    {
        if (Build.EnvironmentVariables.ContainsKey(key))
        {
            var value = Build.EnvironmentVariables[key];
            return (T)value;
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

                        var newBuildStepState = JsonSerializer.Serialize(_buildStep, DefaultJsonSerializerOptions.Options);
                        if (newBuildStepState != previousBuildStepState)
                        {
                            await Update();
                            previousBuildStepState = newBuildStepState;
                        }
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
        StopUpdatePusher();
        await Update();
    }
}
