using System.Text.RegularExpressions;
using MongoDB.Driver;
using Microsoft.AspNetCore.SignalR;
using UKSF.Api.ArmaServer.DataContext;
using UKSF.Api.ArmaServer.Models;
using UKSF.Api.ArmaServer.Signalr.Clients;
using UKSF.Api.ArmaServer.Signalr.Hubs;
using UKSF.Api.Core;
using UKSF.Api.Core.Context;
using UKSF.Api.Core.Extensions;
using UKSF.Api.Core.Models.Request;
using UKSF.Api.Core.Services;

namespace UKSF.Api.ArmaServer.Services;

public interface IGameServersService
{
    List<DomainGameServer> GetServers();
    DomainGameServer GetServer(string id);
    DomainGameServer CheckServer(string check, string excludeId = null);
    Task AddServerAsync(DomainGameServer server);
    Task<bool> EditServerAsync(DomainGameServer server);
    Task DeleteServerAsync(string id);
    List<GameServerMod> GetAvailableMods(string id);
    List<GameServerMod> GetEnvironmentMods(GameEnvironment environment);
    Task SetServerModsAsync(string id, DomainGameServer server);
    GameServerModsDataset ResetServerMods(string id);
    Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate);
    bool GetDisabledState();
    Task SetDisabledStateAsync(bool state);
}

public class GameServersService(
    IGameServersContext gameServersContext,
    IGameServerHelpers gameServerHelpers,
    IVariablesService variablesService,
    IVariablesContext variablesContext,
    IHubContext<ServersHub, IServersClient> serversHub,
    IUksfLogger logger
) : IGameServersService
{
    public List<DomainGameServer> GetServers()
    {
        return gameServersContext.Get().ToList();
    }

    public DomainGameServer GetServer(string id)
    {
        return gameServersContext.GetSingle(id);
    }

    public DomainGameServer CheckServer(string check, string excludeId = null)
    {
        if (excludeId is not null)
        {
            return gameServersContext.GetSingle(x => x.Id != excludeId && (x.Name == check || x.ApiPort.ToString() == check));
        }

        return gameServersContext.GetSingle(x => x.Name == check || x.ApiPort.ToString() == check);
    }

    public async Task AddServerAsync(DomainGameServer server)
    {
        server.Order = gameServersContext.Get().Count();
        await gameServersContext.Add(server);
        logger.LogAudit($"Server added '{server}'");
    }

    public async Task<bool> EditServerAsync(DomainGameServer server)
    {
        var oldGameServer = gameServersContext.GetSingle(server.Id);
        logger.LogAudit($"Game server '{server.Name}' updated:{oldGameServer.Changes(server)}");
        var environmentChanged = false;
        if (oldGameServer.Environment != server.Environment)
        {
            environmentChanged = true;
            server.Mods = GetEnvironmentMods(server.Environment);
            server.ServerMods = new List<GameServerMod>();
        }

        await gameServersContext.Update(
            server.Id,
            Builders<DomainGameServer>.Update.Set(x => x.Name, server.Name)
                                      .Set(x => x.Port, server.Port)
                                      .Set(x => x.ApiPort, server.ApiPort)
                                      .Set(x => x.NumberHeadlessClients, server.NumberHeadlessClients)
                                      .Set(x => x.ProfileName, server.ProfileName)
                                      .Set(x => x.HostName, server.HostName)
                                      .Set(x => x.Password, server.Password)
                                      .Set(x => x.AdminPassword, server.AdminPassword)
                                      .Set(x => x.Environment, server.Environment)
                                      .Set(x => x.ServerOption, server.ServerOption)
                                      .Set(x => x.Mods, server.Mods)
                                      .Set(x => x.ServerMods, server.ServerMods)
        );

        return environmentChanged;
    }

    public async Task DeleteServerAsync(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        logger.LogAudit($"Game server deleted '{gameServer.Name}'");
        await gameServersContext.Delete(id);
    }

    public List<GameServerMod> GetAvailableMods(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        Uri serverExecutable = new(gameServerHelpers.GetGameServerExecutablePath(gameServer));

        IEnumerable<string> availableModsFolders = [gameServerHelpers.GetGameServerModsPaths(gameServer.Environment)];
        availableModsFolders = availableModsFolders.Concat(gameServerHelpers.GetGameServerExtraModsPaths());

        var dlcModFoldersRegexString = gameServerHelpers.GetDlcModFoldersRegexString();
        Regex allowedPaths = new($"@.*|{dlcModFoldersRegexString}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        Regex allowedExtensions = new("[ep]bo", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        List<GameServerMod> mods = [];
        foreach (var modsPath in availableModsFolders)
        {
            var modFolders = new DirectoryInfo(modsPath).EnumerateDirectories("*.*", SearchOption.AllDirectories).Where(x => allowedPaths.IsMatch(x.Name));
            foreach (var modFolder in modFolders)
            {
                if (mods.Any(x => x.Path == modFolder.FullName))
                {
                    continue;
                }

                var hasModFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.*", SearchOption.AllDirectories)
                                                                       .Any(x => allowedExtensions.IsMatch(x.Extension));
                if (!hasModFiles)
                {
                    continue;
                }

                GameServerMod mod = new() { Name = modFolder.Name, Path = modFolder.FullName };
                Uri modFolderUri = new(mod.Path);
                if (serverExecutable.IsBaseOf(modFolderUri))
                {
                    mod.PathRelativeToServerExecutable = Uri.UnescapeDataString(serverExecutable.MakeRelativeUri(modFolderUri).ToString());
                }

                mods.Add(mod);
            }
        }

        foreach (var mod in mods)
        {
            if (mods.Any(x => x.Name == mod.Name && x.Path != mod.Path))
            {
                mod.IsDuplicate = true;
            }

            foreach (var duplicate in mods.Where(x => x.Name == mod.Name && x.Path != mod.Path))
            {
                duplicate.IsDuplicate = true;
            }
        }

        return mods;
    }

    public List<GameServerMod> GetEnvironmentMods(GameEnvironment environment)
    {
        var repoModsFolder = gameServerHelpers.GetGameServerModsPaths(environment);
        var modFolders = new DirectoryInfo(repoModsFolder).EnumerateDirectories("@*", SearchOption.TopDirectoryOnly);
        return modFolders
               .Select(modFolder => new { modFolder, modFiles = new DirectoryInfo(modFolder.FullName).EnumerateFiles("*.pbo", SearchOption.AllDirectories) })
               .Where(x => x.modFiles.Any())
               .Select(x => new GameServerMod { Name = x.modFolder.Name, Path = x.modFolder.FullName })
               .ToList();
    }

    public async Task SetServerModsAsync(string id, DomainGameServer server)
    {
        var oldGameServer = gameServersContext.GetSingle(id);
        await gameServersContext.Update(id, Builders<DomainGameServer>.Update.Unset(x => x.Mods).Unset(x => x.ServerMods));
        await gameServersContext.Update(id, Builders<DomainGameServer>.Update.Set(x => x.Mods, server.Mods).Set(x => x.ServerMods, server.ServerMods));
        logger.LogAudit($"Game server '{server.Name}' updated:{oldGameServer.Changes(server)}");
    }

    public GameServerModsDataset ResetServerMods(string id)
    {
        var gameServer = gameServersContext.GetSingle(id);
        return new GameServerModsDataset
        {
            AvailableMods = GetAvailableMods(id),
            Mods = GetEnvironmentMods(gameServer.Environment),
            ServerMods = new List<GameServerMod>()
        };
    }

    public async Task UpdateGameServerOrder(OrderUpdateRequest orderUpdate)
    {
        foreach (var server in gameServersContext.Get())
        {
            if (server.Order == orderUpdate.PreviousIndex)
            {
                await gameServersContext.Update(server.Id, x => x.Order, orderUpdate.NewIndex);
            }
            else if (server.Order > orderUpdate.PreviousIndex && server.Order <= orderUpdate.NewIndex)
            {
                await gameServersContext.Update(server.Id, x => x.Order, server.Order - 1);
            }
            else if (server.Order < orderUpdate.PreviousIndex && server.Order >= orderUpdate.NewIndex)
            {
                await gameServersContext.Update(server.Id, x => x.Order, server.Order + 1);
            }
        }
    }

    public bool GetDisabledState()
    {
        return variablesService.GetVariable("SERVER_CONTROL_DISABLED").AsBool();
    }

    public async Task SetDisabledStateAsync(bool state)
    {
        await variablesContext.Update("SERVER_CONTROL_DISABLED", state);
        await serversHub.Clients.All.ReceiveDisabledState(state);
    }
}
