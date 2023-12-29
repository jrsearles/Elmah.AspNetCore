﻿using System;
using System.Diagnostics;
using Elmah.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Elmah;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseElmahMiddleware(this IApplicationBuilder app)
    {
        // We don't use an option for this, but registration of the service is opt-in
        var observer = app.ApplicationServices.GetService<ElmahSqlDiagnosticObserver>();
        if (observer is not null)
        {
            DiagnosticListener.AllListeners.Subscribe(observer);
        }

        app.UseMiddleware<ErrorLogMiddleware>();
        return app;
    }

    public static IHostBuilder UseElmah(this IHostBuilder host)
    {
        return host.UseElmah((Action<HostBuilderContext, ElmahBuilder>?)null);
    }

    public static IHostBuilder UseElmah(this IHostBuilder host, Action<ElmahBuilder>? configureElmah)
    {
        return host.UseElmah((_, elmah) => configureElmah?.Invoke(elmah));
    }

    public static IHostBuilder UseElmah(this IHostBuilder host, Action<HostBuilderContext, ElmahBuilder>? configureElmah)
    {
        host.ConfigureServices((builderContext, services) =>
        {
            services.AddElmahCoreServices();

            var elmah = new ElmahBuilder(services);

            // Set as default because it is required - consumer can replace in configure delegate
            elmah.PersistToMemory();

            configureElmah?.Invoke(builderContext, elmah);
        });

        return host;
    }

    public static IWebHostBuilder UseElmah(this IWebHostBuilder host)
    {
        return host.UseElmah(null);
    }

    public static IWebHostBuilder UseElmah(this IWebHostBuilder host, Action<WebHostBuilderContext, ElmahBuilder>? configureElmah)
    {
        host.ConfigureServices((builderContext, services) =>
        {
            services.AddElmahCoreServices();

            var elmah = new ElmahBuilder(services);

            // Set as default because it is required - consumer can replace in configure delegate
            elmah.PersistToMemory();

            configureElmah?.Invoke(builderContext, elmah);
        });

        return host;
    }
}