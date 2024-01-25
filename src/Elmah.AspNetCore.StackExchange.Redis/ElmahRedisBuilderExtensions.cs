using Elmah.AspNetCore.StackExchange.Redis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Elmah.AspNetCore;

public static class ElmahRedisBuilderExtensions
{
    /// <summary>
    /// Configures Elmah to use Redis for the error persistence. This will use an instance of <see cref="IConnectionMultiplexer"/> 
    /// using DI (dependency injection). Options will be defaults unless configured through DI.
    /// </summary>
    /// <param name="builder">The Elmah builder</param>
    public static void PersistToRedis(this IElmahBuilder builder) => builder.PersistToRedis(o => { });

    /// <summary>
    /// Configures Elmah to use Redis for the error persistence. This will use an instance of <see cref="IConnectionMultiplexer"/> 
    /// using DI (dependency injection).
    /// </summary>
    /// <param name="builder">The Elmah builder</param>
    /// <param name="configureOptions">Action to configure options</param>
    public static void PersistToRedis(this IElmahBuilder builder, Action<RedisErrorLogOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        builder.PersistTo<RedisErrorLog>();
    }

    /// <summary>
    /// Configures Elmah to use Redis for error persistence. The provided <see cref="IConnectionMultiplexer"/> will be used for
    /// Redis connectivity. Options will be defaults unless configured through DI.
    /// </summary>
    /// <param name="builder">The Elmah builder</param>
    /// <param name="server">The Redis connection</param>
    public static void PersistToRedis(this IElmahBuilder builder, IConnectionMultiplexer server) => builder.PersistToRedis(server, o => { });

    /// <summary>
    /// Configures Elmah to use Redis for error persistence. The provided <see cref="IConnectionMultiplexer"/> will be used for
    /// Redis connectivity. Options will be defaults unless configured through DI.
    /// </summary>
    /// <param name="builder">The Elmah builder</param>
    /// <param name="server">The Redis connection</param>
    /// <param name="configureOptions">Action to configure options</param>
    public static void PersistToRedis(this IElmahBuilder builder, IConnectionMultiplexer server, Action<RedisErrorLogOptions> configureOptions)
    {
        builder.Services.Configure(configureOptions);
        builder.PersistTo(sp => new RedisErrorLog(server, sp.GetRequiredService<IOptions<RedisErrorLogOptions>>()));
    }
}
