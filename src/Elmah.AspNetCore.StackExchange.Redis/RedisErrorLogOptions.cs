using StackExchange.Redis;

namespace Elmah.AspNetCore.StackExchange.Redis;

public class RedisErrorLogOptions
{
    /// <summary>
    /// Gets or sets the key prefix used for the list of errors. By default this will use "urn:elmah:error_list:" and
    /// append the application name for the key.
    /// </summary>
    public string RedisListKeyPrefix { get; set; } = "urn:elmah:error_list:";

    /// <summary>
    /// Gets or sets the key prefix used for individual error payloads. By default this will use "urn:elmah:error:" and
    /// append the error identifier.
    /// </summary>
    public string RedisKeyPrefix { get; set; } = "urn:elmah:error:";

    /// <summary>
    /// Gets or sets a the number of errors that will be stored in Redis. Older errors will be removed once the limit is reached.
    /// The default value is 200.
    /// </summary>
    public int MaximumSize { get; set; } = 200;

    /// <summary>
    /// Gets or sets the time to live for the error record. This sets the expiration on the redis key.
    /// </summary>
    public TimeSpan ErrorLogTtl { get; set; } = TimeSpan.FromDays(1);

    /// <summary>
    /// Gets or sets a Redis connection to be used when accessing the redis server.
    /// </summary>
    public Func<Task<IConnectionMultiplexer>>? ConnectionMultiplexerFactory { get; set; }
}
