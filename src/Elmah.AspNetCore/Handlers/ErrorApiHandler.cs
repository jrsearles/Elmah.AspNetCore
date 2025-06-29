﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Elmah.AspNetCore.Handlers;

internal static partial class Endpoints
{
    public static IEndpointConventionBuilder MapApiError(this IEndpointRouteBuilder builder, string prefix = "")
    {
        var handler = RequestDelegateFactory.Create(async (
            [FromQuery] string? id,
            [FromServices] ErrorLog errorLog,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid errorGuid))
            {
                return Results.Content("{}", MediaTypeNames.Application.Json);
            }

            var error = await GetErrorAsync(errorLog, errorGuid, cancellationToken);
            return Results.Json(error, DefaultJsonSerializerOptions.ApiSerializerOptions);
        });

        var pipeline = builder.CreateApplicationBuilder();
        pipeline.Run(handler.RequestDelegate);
        return builder.MapMethods($"{prefix}/api/error", new[] { HttpMethods.Get, HttpMethods.Post }, pipeline.Build());
    }

    public static IEndpointConventionBuilder MapApiErrors(this IEndpointRouteBuilder builder, string prefix = "")
    {
        var handler = RequestDelegateFactory.Create(async (
            [FromQuery(Name = "i")] int? errorIndex,
            [FromQuery(Name = "s")] int? pageSize,
            [FromQuery(Name = "q")] string? searchText,
            [FromServices] ErrorLog errorLog,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            var filters = await ReadErrorFilters(request, searchText, cancellationToken);

            var entities = await GetErrorsAsync(errorLog, filters, errorIndex ?? 0, pageSize ?? 0, cancellationToken);
            return Results.Json(entities, DefaultJsonSerializerOptions.ApiSerializerOptions);
        });

        var pipeline = builder.CreateApplicationBuilder();
        pipeline.Run(handler.RequestDelegate);
        return builder.MapMethods($"{prefix}/api/errors", new[] { HttpMethods.Get, HttpMethods.Post }, pipeline.Build());
    }

    public static IEndpointConventionBuilder MapApiNewErrors(this IEndpointRouteBuilder builder, string prefix = "")
    {
        var handler = RequestDelegateFactory.Create(async (
            [FromQuery] string? id,
            [FromQuery(Name = "q")] string? searchText,
            [FromServices] ErrorLog errorLog,
            HttpRequest request,
            CancellationToken cancellationToken) =>
        {
            var filters = await ReadErrorFilters(request, searchText, cancellationToken);

            var newEntities = await GetNewErrorsAsync(errorLog, id, filters, cancellationToken);
            return Results.Json(newEntities, DefaultJsonSerializerOptions.ApiSerializerOptions);
        });

        var pipeline = builder.CreateApplicationBuilder();
        pipeline.Run(handler.RequestDelegate);
        return builder.MapPost($"{prefix}/api/new-errors", pipeline.Build());
    }

    private static async Task<ErrorLogFilterCollection> ReadErrorFilters(HttpRequest request, string? searchText, CancellationToken cancellationToken)
    {
        var filters = new ErrorLogFilterCollection();
        if (!string.IsNullOrEmpty(searchText))
        {
            filters.Add(new ErrorLogSearchFilter(searchText));
        }

        if (HttpMethods.IsPost(request.Method))
        {
            var strings = await JsonSerializer.DeserializeAsync<string[]>(request.Body, cancellationToken: cancellationToken) ?? Array.Empty<string>();
            foreach (var str in strings)
            {
                var filter = ErrorLogPropertyFilter.Parse(str);
                if (filter is not null)
                {
                    filters.Add(filter);
                }
            }
        }

        return filters;
    }

    private static async Task<ErrorLogEntryWrapper?> GetErrorAsync(ErrorLog errorLog, Guid id, CancellationToken cancellationToken)
    {
        var error = await errorLog.GetErrorAsync(id, cancellationToken);
        return error is null ? null : new ErrorLogEntryWrapper(error);
    }

    private static async Task<ErrorsList> GetErrorsAsync(ErrorLog errorLog, ErrorLogFilterCollection errorFilters, int errorIndex, int pageSize, CancellationToken cancellationToken)
    {
        errorIndex = Math.Max(0, errorIndex);
        pageSize = pageSize switch
        {
            < 0 => 0,
            > 100 => 100,
            _ => pageSize
        };

        var entries = new List<ErrorLogEntry>(pageSize);
        var totalCount = await errorLog.GetErrorsAsync(errorFilters, errorIndex, pageSize, entries, cancellationToken);
        return new ErrorsList
        {
            Errors = entries.Select(i => new ErrorLogEntryWrapper(i)).ToList(),
            TotalCount = totalCount
        };
    }

    private static async Task<ErrorsList> GetNewErrorsAsync(ErrorLog errorLog, string? id, ErrorLogFilterCollection errorFilters, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(id) || !Guid.TryParse(id, out Guid errorGuid))
        {
            return await GetErrorsAsync(errorLog, errorFilters, 0, 50, cancellationToken);
        }

        var (totalCount, errors) = await GetNewErrorsAsync(errorLog, errorGuid, errorFilters, cancellationToken);
        return new ErrorsList
        {
            Errors = errors,
            TotalCount = totalCount
        };
    }

    private static async Task<(int, List<ErrorLogEntryWrapper>)> GetNewErrorsAsync(ErrorLog errorLog, Guid errorGuid, ErrorLogFilterCollection errorFilters, CancellationToken cancellationToken)
    {
        const int pageSize = 10;
        int page = 0;
        var buffer = new List<ErrorLogEntry>(10);
        var returnList = new List<ErrorLogEntryWrapper>();

        do
        {
            buffer.Clear();
            int count = await errorLog.GetErrorsAsync(errorFilters, page * pageSize, pageSize, buffer, cancellationToken);

            foreach (var el in buffer)
            {
                if (el.Id == errorGuid)
                {
                    return (count, returnList);
                }

                returnList.Add(new ErrorLogEntryWrapper(el));
            }

            page += 1;
        } while (buffer.Count > 0 && !cancellationToken.IsCancellationRequested);

        return (0, returnList);
    }
}