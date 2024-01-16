using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Elmah.AspNetCore;

internal class ElmahLogFeature : IElmahLogFeature
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 0
    };

    private readonly ConcurrentDictionary<Guid, ElmahLogSqlEntry> _map = new();
    private readonly ConcurrentBag<IElmahLogMessage> _logs = new();
    private readonly ConcurrentBag<ElmahLogParamEntry> _params = new();

    public IReadOnlyCollection<IElmahLogMessage> Log => _logs.ToList();
    public IReadOnlyCollection<ElmahLogParamEntry> Params => _params.ToList();
    public IReadOnlyCollection<ElmahLogSqlEntry> LogSql => _map.Values.OrderBy(i => i.TimeStamp).ToList();

    public void AddMessage(IElmahLogMessage entry)
    {
        _logs.Add(entry);
    }

    public void AddSql(Guid id, ElmahLogSqlEntry entry)
    {
        _map.TryAdd(id, entry);
    }

    public void SetSqlDuration(Guid id)
    {
        if (_map.TryGetValue(id, out ElmahLogSqlEntry? data))
        {
            data.DurationMs = StopwatchExtensions.GetElapsedTime(data.TimerStart).TotalMilliseconds;
        }
    }

    public void LogParameters((string name, object? value)[] list, string typeName, string memberName,
        string file, int line)
    {
        var paramList = list.Where(x => x != default).Select(x => new KeyValuePair<string, string>(x.name, ValueToString(x.value))).ToArray();
        _params.Add(new ElmahLogParamEntry(DateTime.UtcNow, paramList, typeName, memberName, file, line));
    }

    private static string ValueToString(object? paramValue)
    {
        if (paramValue is null)
        {
            return "null";
        }

        try
        {
            return JsonSerializer.Serialize(paramValue, SerializerOptions);
        }
        catch
        {
            return paramValue.ToString()!;
        }
    }
}
