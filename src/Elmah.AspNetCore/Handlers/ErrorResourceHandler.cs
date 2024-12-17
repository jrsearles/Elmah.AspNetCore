using System;
using System.IO;
using System.Net.Mime;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;

namespace Elmah.AspNetCore.Handlers;

internal static partial class Endpoints
{
    private static readonly Assembly ThisAssembly = typeof(Endpoints).Assembly;
    private static readonly EmbeddedFileProvider StaticFiles = new(ThisAssembly, $"{ThisAssembly.GetName().Name}.wwwroot");

    private static async Task<IResult> ReturnIndex([FromServices] ILoggerFactory loggerFactory, HttpContext context)
    {
        var indexFile = StaticFiles.GetFileInfo("index.html");
        if (indexFile is not { Exists: true })
        {
            var logger = loggerFactory.CreateLogger("Elmah.AspNetCore");
            logger.LogError("{page} is not found for Elmah - has static content been generated? See 'Running Source Locally' in README.md for more details.", "index.html");
            return Results.NotFound();
        }

        using var stream = indexFile.CreateReadStream();
        using var reader = new StreamReader(stream);

        var elmahRoot = context.GetElmahRelativeRoot();
        var html = await reader.ReadToEndAsync();
        html = html.Replace("ELMAH_ROOT", elmahRoot);
        return Results.Content(html, MediaTypeNames.Text.Html);
    }

    public static IEndpointConventionBuilder MapRoot(this IEndpointRouteBuilder builder, string prefix = "")
    {
        var handler = RequestDelegateFactory.Create(ReturnIndex);

        var pipeline = builder.CreateApplicationBuilder();
        pipeline.Run(handler.RequestDelegate);
        return builder.MapGet(prefix, pipeline.Build());
    }

    public static IEndpointConventionBuilder MapResources(this IEndpointRouteBuilder builder, string prefix = "")
    {
        var contentTypeProvider = new FileExtensionContentTypeProvider();
        
        var handler = RequestDelegateFactory.Create(async ([FromRoute] string path, [FromServices] ILoggerFactory loggerFactory, HttpContext context) =>
        {
            if (!path.Contains('.', StringComparison.Ordinal))
            {
                return await ReturnIndex(loggerFactory, context);
            }

            var fileInfo = StaticFiles.GetFileInfo(path);
            if (fileInfo is not { Exists: true })
            {
                return Results.NotFound();
            }

            contentTypeProvider.TryGetContentType(path, out string? contentType);
            return Results.Stream(fileInfo.CreateReadStream(), contentType);
        });

        var pipeline = builder.CreateApplicationBuilder();
        pipeline.Run(handler.RequestDelegate);
        return builder.Map($"{prefix}/{{*path}}", pipeline.Build());
    }
}
