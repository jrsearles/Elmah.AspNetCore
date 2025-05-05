﻿using System.Diagnostics.CodeAnalysis;
using Elmah.AspNetCore;
using Elmah.AspNetCore.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Elmah.AspNetCore;

public static class EndpointRouteBuilderExtensions
{
    public static IEndpointConventionBuilder MapElmah(this IEndpointRouteBuilder endpoints) => endpoints.MapElmah("/elmah");

    public static IEndpointConventionBuilder MapElmah(this IEndpointRouteBuilder endpoints, [StringSyntax("Route")] string prefix)
    {
        var elmahEnvironment = endpoints.ServiceProvider.GetRequiredService<ElmahEnvironment>();
        var elmahOptions = endpoints.ServiceProvider.GetRequiredService<IOptions<ElmahOptions>>().Value;
        elmahEnvironment.Path = prefix;
        elmahEnvironment.MaxUiErrors = elmahOptions.MaxUiErrors;

#if NET7_0_OR_GREATER
        var group = endpoints.MapGroup(prefix);
        group.MapRoot();
        group.MapApiError();
        group.MapApiErrors();
        group.MapApiNewErrors();
        group.MapRss();
        group.MapDigestRss();
        group.MapMsdn();
        group.MapMsdnStatus();
        group.MapJson();
        group.MapXml();
        group.MapDownload();
        group.MapTest();
        group.MapResources();
        return group
            .WithDisplayName("Elmah.AspNetCore");
#else
        var routes = new[]
        {
            endpoints.MapRoot(prefix),
            endpoints.MapApiError(prefix),
            endpoints.MapApiErrors(prefix),
            endpoints.MapApiNewErrors(prefix),
            endpoints.MapRss(prefix),
            endpoints.MapDigestRss(prefix),
            endpoints.MapMsdn(prefix),
            endpoints.MapMsdnStatus(prefix),
            endpoints.MapXml(prefix),
            endpoints.MapJson(prefix),
            endpoints.MapDownload(prefix),
            endpoints.MapTest(prefix),
            endpoints.MapResources(prefix)
        };

        return new ElmahEndpointCollection(routes)
            .WithDisplayName("Elmah.AspNetCore");
#endif
    }
}
