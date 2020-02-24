using UKSF.Api.Models.Game;

namespace UKSF.Api.Interfaces.Data.Cached {
    public interface IGameServersDataService : IDataService<GameServer, IGameServersDataService>, ICachedDataService { }
}
