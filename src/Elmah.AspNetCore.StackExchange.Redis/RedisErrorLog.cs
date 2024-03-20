using System.Net.Sockets;
using Elmah.AspNetCore.Xml;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Elmah.AspNetCore.StackExchange.Redis;

public class RedisErrorLog : ErrorLog
{
    private readonly Lazy<Task<IConnectionMultiplexer>> _redis;
    private readonly ILogger<RedisErrorLog> _logger;
    private readonly RedisErrorLogOptions _options;

    private readonly RedisKey _listKey;
    private readonly RedisKey _keyPrefix;

    public RedisErrorLog(IOptions<RedisErrorLogOptions> options)
        : this(null, options, NullLogger<RedisErrorLog>.Instance)
    {
    }

    public RedisErrorLog(IConnectionMultiplexer redis, IOptions<RedisErrorLogOptions> options)
        : this(redis, options, NullLogger<RedisErrorLog>.Instance)
    {
    }

    public RedisErrorLog(IConnectionMultiplexer? redis, IOptions<RedisErrorLogOptions> options, ILogger<RedisErrorLog> logger)
    {
        var factory = redis is null ? options.Value.ConnectionMultiplexerFactory : (() => Task.FromResult(redis));
        if (factory is null)
        {
            throw new ArgumentNullException("The IConnectionMultiplexer must exist in dependency injection or as a factory method on RedisErrorLogOptions.ConnectionMultiplexerFactory.", nameof(redis));
        }

        _redis = new Lazy<Task<IConnectionMultiplexer>>(factory, LazyThreadSafetyMode.PublicationOnly);
        _logger = logger;
        _options = options.Value;

        _listKey = $"{options.Value.RedisListKeyPrefix}{this.ApplicationName}";
        _keyPrefix = options.Value.RedisKeyPrefix;
    }

    public override async Task<ErrorLogEntry?> GetErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        string? value;

        try
        {
            var server = await _redis.Value;
            var db = server.GetDatabase();
            var key = this.GetErrorKey(id);

            value = await db.StringGetAsync(key);
        }
        catch (Exception ex) when (ex is RedisConnectionException or SocketException)
        {
            _logger.LogError(ex, "Unable to read error from Redis");
            return null;
        }

        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return this.ReadErrorFromRedis(value);
    }
    
    public override async Task<int> GetErrorsAsync(ErrorLogFilterCollection errorLogFilters, int errorIndex, int pageSize, ICollection<ErrorLogEntry> entries, CancellationToken cancellationToken)
    {
        int count = 0;

        try
        {
            var server = await _redis.Value;
            var db = server.GetDatabase();
            var keys = await db.ListRangeAsync(_listKey, errorIndex, errorIndex + pageSize - 1);

            if (keys is null || keys.Length == 0)
            {
                return 0;
            }

            var values = await db.StringGetAsync(keys.Select(x => new RedisKey(x)).ToArray());
            foreach (string? value in values)
            {
                if (value is null)
                {
                    continue;
                }

                entries.Add(this.ReadErrorFromRedis(value));
            }

            count = Convert.ToInt32(await db.ListLengthAsync(_listKey));
        }
        catch (Exception ex) when (ex is RedisConnectionException or SocketException)
        {
            _logger.LogError(ex, "Unable to read errors from Redis");
        }

        return count;
    }

    public override async Task LogAsync(Error error, CancellationToken cancellationToken)
    {
        try
        {
            var server = await _redis.Value;
            IDatabase db = server.GetDatabase();

            // append key so we can easily rehydrate
            RedisValue errorXml = $"{error.Id:N}{ErrorXml.EncodeString(error)}";
            RedisKey key = this.GetErrorKey(error.Id);
            await db.StringSetAsync(key, errorXml);

            var len = await db.ListLeftPushAsync(_listKey, key.ToString());
            if (len > _options.MaximumSize)
            {
                await this.TrimAsync(db);
            }
        }
        catch (Exception ex) when (ex is RedisConnectionException or SocketException)
        {
            _logger.LogError(ex, "Failed to write error to Redis");
        }
    }

    private async Task TrimAsync(IDatabase db)
    {
        // need to get last entries to delete actual records
        var keys = await db.ListRangeAsync(_listKey, _options.MaximumSize, -1);
        if (keys is not null)
        {
            foreach (var key in keys)
            {
                await db.StringGetDeleteAsync(key.ToString());
            }
        }

        await db.ListTrimAsync(_listKey, 0, _options.MaximumSize - 1);
    }

    private ErrorLogEntry ReadErrorFromRedis(string value)
    {
        // first part is id (Guid = 32 digits)
        var id = value[..32];
        var xml = value[32..];

        var error = ErrorXml.DecodeString(Guid.ParseExact(id, "N"), xml);
        return new ErrorLogEntry(this, error);
    }

    private RedisKey GetErrorKey(Guid id)
    {
        return _keyPrefix.Append(id.ToString("N"));
    }
}
