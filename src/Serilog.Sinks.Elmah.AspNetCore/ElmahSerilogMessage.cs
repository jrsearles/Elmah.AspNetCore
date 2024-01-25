using Elmah.AspNetCore;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace Serilog.Sinks.Elmah.AspNetCore;

internal sealed class ElmahSerilogMessage : IElmahLogMessage
{
    public MessageTemplate Template { get; init; } = default!;

    public IReadOnlyDictionary<string, LogEventPropertyValue> Properties { get; init; } = default!;

    public DateTime TimeStamp { get; init; }

    public string? Exception { get; init; }

    public string? Scope { get; init; }

    public LogLevel? Level { get; init; }

    public string? Render() => this.Template.Render(this.Properties, null);
}
