using System;
using Microsoft.Extensions.Logging;

namespace Elmah.AspNetCore;

/// <summary>
/// Represents a log message which can be included within contextual information stored by Elmah.
/// </summary>
public interface IElmahLogMessage
{
    DateTime TimeStamp { get; }
    string? Exception { get; }
    string? Scope { get; }
    LogLevel? Level { get; }
    string? Render();
}