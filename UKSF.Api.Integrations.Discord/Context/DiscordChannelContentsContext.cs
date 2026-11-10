using UKSF.Api.Core.Context.Base;
using UKSF.Api.Core.Events;
using UKSF.Api.Integrations.Discord.Models;

namespace UKSF.Api.Integrations.Discord.Context;

public interface IDiscordChannelContentsContext : IMongoContext<DomainDiscordChannelContent>;

public class DiscordChannelContentsContext(IMongoCollectionFactory mongoCollectionFactory, IEventBus eventBus)
    : MongoContext<DomainDiscordChannelContent>(mongoCollectionFactory, eventBus, "discordChannelContents"), IDiscordChannelContentsContext;
