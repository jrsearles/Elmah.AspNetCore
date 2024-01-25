using Elmah.AspNetCore.Xml;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Elmah.AspNetCore.StackExchange.Redis;

public class RedisErrorLog : ErrorLog
{
    private readonly IConnectionMultiplexer _redis;
    private readonly RedisErrorLogOptions _options;

    private readonly RedisKey _listKey;
    private readonly RedisKey _keyPrefix;

    public RedisErrorLog(IConnectionMultiplexer redis, IOptions<RedisErrorLogOptions> options)
    {
        _redis = redis;
        _options = options.Value;

        _listKey = $"{options.Value.RedisListKeyPrefix}{this.ApplicationName}";
        _keyPrefix = options.Value.RedisKeyPrefix;
    }

    public override async Task<ErrorLogEntry?> GetErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
        var key = this.GetErrorKey(id);

        string? value = await db.StringGetAsync(key);
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        return this.ReadErrorFromRedis(value);
    }
    
    public override async Task<int> GetErrorsAsync(ErrorLogFilterCollection errorLogFilters, int errorIndex, int pageSize, ICollection<ErrorLogEntry> entries, CancellationToken cancellationToken)
    {
        var db = _redis.GetDatabase();
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

        long count = await db.ListLengthAsync(_listKey);
        return Convert.ToInt32(count);
    }

    public override async Task LogAsync(Error error, CancellationToken cancellationToken)
    {
        IDatabase db = _redis.GetDatabase();
        
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
