﻿using ElmahCore.Mvc.Handlers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Diagnostics.CodeAnalysis;

namespace ElmahCore;

public static class ElmahEndpoints
{
    public static IEndpointConventionBuilder MapElmah(this IEndpointRouteBuilder builder) => builder.MapElmah("/elmah");

    public static IEndpointConventionBuilder MapElmah(this IEndpointRouteBuilder builder, [StringSyntax("Route")]string prefix)
    {
        // HACK: we're using the options instance as global configuration. It might make more sense to create our
        // own object to store configuration context that is shared.
        var options = builder.ServiceProvider.GetRequiredService<IOptions<ElmahOptions>>().Value;
        options.Path = prefix;

#if NET7_0_OR_GREATER
        var group = builder.MapGroup(prefix);
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
        return group;
#else
        var routes = new[]
        {
            builder.MapRoot(prefix),
            builder.MapApiError(prefix),
            builder.MapApiErrors(prefix),
            builder.MapApiNewErrors(prefix),
            builder.MapRss(prefix),
            builder.MapDigestRss(prefix),
            builder.MapMsdn(prefix),
            builder.MapMsdnStatus(prefix),
            builder.MapXml(prefix),
            builder.MapJson(prefix),
            builder.MapDownload(prefix),
            builder.MapTest(prefix),
            builder.MapResources(prefix)
        };

        return new ElmahEndpointCollection(routes);
#endif
    }
}

#if NET6_0
internal class ElmahEndpointCollection : IEndpointConventionBuilder
{
    private readonly IEndpointConventionBuilder[] _routes;

    public ElmahEndpointCollection(IEndpointConventionBuilder[] routes)
    {
        _routes = routes;
    }

    public void Add(Action<EndpointBuilder> convention)
    {
        foreach (var route in _routes)
        {
            route.Add(convention);
        }
    }
}
#endif