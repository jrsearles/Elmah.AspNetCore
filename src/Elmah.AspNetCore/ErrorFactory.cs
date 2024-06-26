﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Mime;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Elmah.AspNetCore;

internal sealed class ErrorFactory : IErrorFactory
{
    private static readonly string[] SupportedContentTypes =
{
        MediaTypeNames.Application.Json,
        "application/x-www-form-urlencoded",
        "application/javascript",
        MediaTypeNames.Application.Soap,
        "application/xhtml+xml",
        MediaTypeNames.Application.Xml,
        MediaTypeNames.Text.Html,
        "text/javascript",
        MediaTypeNames.Text.Plain,
        MediaTypeNames.Text.Xml,
        "text/markdown"
    };

    private readonly IOptions<ElmahOptions> _elmahOptions;

    public ErrorFactory(IOptions<ElmahOptions> elmahOptions)
    {
        _elmahOptions = elmahOptions;
    }

    public async Task<Error> CreateAsync(Exception? e, HttpContext? context)
    {
        var options = _elmahOptions.Value;
        var baseException = e?.GetBaseException();
        string? typeName = baseException?.GetType().FullName;
        int statusCode = GetStatusCodeFromExceptionOr500(e);

        //
        // If this is an HTTP exception, then get the status code
        // and detailed HTML message provided by the host.
        //
        if (baseException is HttpRequestException { StatusCode: not null } httpExc)
        {
            statusCode = (int)httpExc.StatusCode;
            baseException = baseException.InnerException;
            if (baseException is null)
            {
                typeName = "HTTP";
            }
        }

        NameValueCollection? form = null;
        if (context is not null)
        {
            form = CopyCollection(context.Request.HasFormContentType && options.LogRequestForm ? context.Request.Form : null);

            if (options.LogRequestBody)
            {
                var body = await ReadBodyAsync(context);
                if (!string.IsNullOrEmpty(body))
                {
                    form ??= new NameValueCollection();
                    form.Add("$request-body", body);
                }
            }
        }

        var feature = context?.Features.Get<IElmahLogFeature>();
        
        var error = new Error
        {
            Time = DateTime.UtcNow,
            Exception = baseException,
            Message = baseException?.Message,
            Source = baseException?.Source,
            Detail = e?.ToString(),
            StatusCode = statusCode,
            ServerVariables = GetServerVariables(context),
            QueryString = CopyCollection(context?.Request.Query),
            Cookies = CopyCollection(options.LogRequestCookies ? context?.Request.Cookies : null),
            Form = form,
            MessageLog = feature?.Log.ToArray(),
            SqlLog = feature?.LogSql.ToArray(),
            Params = feature?.Params
                .Where(x => x.Params.Any())
                .ToArray(),
            Type = typeName
        };

        error.ServerVariables.Add("HttpStatusCode", statusCode.ToString());

        //
        // Load the basic information.
        //
        try
        {
            error.HostName = Environment.MachineName;
        }
        catch (SecurityException)
        {
            // A SecurityException may occur in certain, possibly 
            // user-modified, Medium trust environments.
        }

        string? user = context?.User?.Identity?.Name;
        if (context is not null)
        {
            var webUser = context.User;
            if (webUser is not null && !string.IsNullOrEmpty(webUser.Identity?.Name))
            {
                user = webUser.Identity.Name;
            }
        }

        error.User = user;

        var callerInfo = e?.TryGetCallerInfo() ?? CallerInfo.Empty;
        if (!callerInfo.IsEmpty)
        {
            error.Detail = "# caller: " + callerInfo
                                   + Environment.NewLine
                                   + error.Detail;
        }

        return error;
    }

    public static int GetStatusCodeFromExceptionOr500(Exception? e)
    {
        if (e is BadHttpRequestException bhre)
        {
            return bhre.StatusCode;
        }

        if (e is HttpRequestException { StatusCode: not null } hre)
        {
            return (int)hre.StatusCode;
        }

        return 500;
    }

    private static NameValueCollection GetServerVariables(HttpContext? context)
    {
        var serverVariables = new NameValueCollection();
        if (context is not null)
        {
            LoadServerVariables(serverVariables, () => context.Features, string.Empty);
            LoadServerVariables(serverVariables, () => context.User, "User_");

            var ss = context.RequestServices?.GetService(typeof(ISession));
            if (ss is not null)
            {
                LoadServerVariables(serverVariables, () => context.Session, "Session_");
            }

            LoadServerVariables(serverVariables, () => context.Items, "Items_");
            LoadServerVariables(serverVariables, () => context.Connection, "Connection_");
        }

        return serverVariables;
    }

    private static void LoadServerVariables(NameValueCollection serverVariables, Func<object?> getObject, string prefix)
    {
        object? obj;
        try
        {
            obj = getObject();
            if (obj is null)
            {
                return;
            }
        }
        catch
        {
            return;
        }

        var props = obj.GetType().GetProperties();
        foreach (var prop in props)
        {
            object? value = null;
            try
            {
                value = prop.GetValue(obj);
            }
            catch
            {
                // ignored
            }

            var isProcessed = false;
            if (value is IEnumerable en && en is not string)
            {
                if (value is IDictionary<object, object> { Keys.Count: 0 })
                {
                    continue;
                }

                foreach (var item in en)
                {
                    try
                    {
                        var keyProp = item.GetType().GetProperty("Key");
                        var valueProp = item.GetType().GetProperty("Value");

                        if (keyProp is not null && valueProp is not null)
                        {
                            isProcessed = true;
                            var val = valueProp.GetValue(item);
                            if (val is not null && val.GetType().ToString() != val.ToString() &&
                                !val.GetType().IsSubclassOf(typeof(Stream)))
                            {
                                var keyName = keyProp.GetValue(item)?.ToString();
                                var propName =
                                    prop.Name.StartsWith("RequestHeaders",
                                        StringComparison.InvariantCultureIgnoreCase)
                                        ? "Header_"
                                        : prop.Name + "_";

                                if (propName == "Header_" && "Cookie".Equals(keyName, StringComparison.OrdinalIgnoreCase))
                                {
                                    // Cookies are loaded separately
                                    continue;
                                }

                                serverVariables.Add(prefix + propName + keyProp.GetValue(item), val.ToString());
                            }
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }

            if (isProcessed)
            {
                continue;
            }

            try
            {
                if (value is not null && value.GetType().ToString() != value.ToString() &&
                    !value.GetType().IsSubclassOf(typeof(Stream)))
                {
                    serverVariables.Add(prefix + prop.Name, value?.ToString());
                }
            }
            catch
            {
                // ignored
            }
        }
    }

    private static NameValueCollection? CopyCollection(IEnumerable<KeyValuePair<string, StringValues>>? collection)
    {
        // ReSharper disable once PossibleMultipleEnumeration
        if (collection is null || !collection.Any())
        {
            return null;
        }

        // ReSharper disable once PossibleMultipleEnumeration
        var keyValuePairs = collection as KeyValuePair<string, StringValues>[] ?? collection.ToArray();
        if (!keyValuePairs.Any())
        {
            return null;
        }

        var col = new NameValueCollection();
        foreach (var pair in keyValuePairs)
        {
            col.Add(pair.Key, pair.Value);
        }

        return col;
    }

    private static NameValueCollection? CopyCollection(IRequestCookieCollection? cookies)
    {
        if (cookies is null || cookies.Count == 0)
        {
            return null;
        }

        var copy = new NameValueCollection(cookies.Count);

        foreach (var cookie in cookies)
        {
            //
            // NOTE: We drop the Path and Domain properties of the 
            // cookie for sake of simplicity.
            //
            copy.Add(cookie.Key, cookie.Value);
        }

        return copy;
    }

    public static async Task<string?> ReadBodyAsync(HttpContext context)
    {
        var ct = context.Request.ContentType?.ToLower();
        var tEnc = string.Join(",", context.Request.Headers[HeaderNames.TransferEncoding].ToArray());
        if (string.IsNullOrEmpty(ct) || tEnc.Contains("chunked") || !SupportedContentTypes.Any(i => ct.Contains(ct)))
        {
            return null;
        }

        context.Request.EnableBuffering();
        var body = context.Request.Body;
        var buffer = new byte[Convert.ToInt32(context.Request.ContentLength)];

        // ReSharper disable once MustUseReturnValue
        await context.Request.Body.ReadAsync(buffer);
        var bodyAsText = Encoding.UTF8.GetString(buffer);
        body.Seek(0, SeekOrigin.Begin);
        context.Request.Body = body;

        return bodyAsText;
    }
}
