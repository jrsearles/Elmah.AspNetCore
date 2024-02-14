using Microsoft.Extensions.DependencyInjection;
using Serilog.Core;
using Serilog.Sinks.Elmah.AspNetCore;

namespace Elmah.AspNetCore;

public static class ElmahSerilogBuilderExtensions
{
    /// <summary>
    /// Captures log messages sent through Serilog to be presented in Elmah UI along with error context.
    /// </summary>
    /// <param name="builder"></param>
    public static void CaptureSerilogMessages(this IElmahBuilder builder)
    {
        builder.Services.AddSingleton<ILogEventSink, ElmahSink>();
    }
}