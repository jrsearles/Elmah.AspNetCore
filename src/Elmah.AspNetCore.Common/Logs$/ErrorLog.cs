using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable VirtualMemberNeverOverridden.Global
// ReSharper disable UnusedParameter.Global
namespace Elmah.AspNetCore;

/// <summary>
///     Represents an error log capable of storing and retrieving errors
///     generated in an ASP.NET Web application.
/// </summary>
public abstract class ErrorLog
{
    private string? _appName;
    private bool _appNameInitialized;

    /// <summary>
    ///     Get the name of this log.
    /// </summary>
    public virtual string Name => GetType().Name;

    /// <summary>
    ///     Gets the name of the application to which the log is scoped.
    /// </summary>
    public string ApplicationName
    {
        get => _appName ?? Assembly.GetEntryAssembly()?.GetName().Name!;

        set
        {
            if (_appNameInitialized)
            {
                throw new InvalidOperationException("The application name cannot be reset once initialized.");
            }

            _appName = value;
            _appNameInitialized = (value ?? string.Empty).Length > 0;
        }
    }

    public string[]? SourcePaths { get; set; }

    /// <summary>
    ///     When overridden in a subclass, starts a task that asynchronously
    ///     does the same as <see cref="Log(Error)" />. An additional parameter
    ///     specifies a <see cref="CancellationToken" /> to use.
    /// </summary>
    public abstract Task LogAsync(Error error, CancellationToken cancellationToken);

    /// <summary>
    ///     When overridden in a subclass, starts a task that asynchronously
    ///     does the same as <see cref="GetError" />. An additional parameter
    ///     specifies a <see cref="CancellationToken" /> to use.
    /// </summary>
    // ReSharper disable once UnusedParameter.Global
    public abstract Task<ErrorLogEntry?> GetErrorAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    ///     When overridden in a subclass, starts a task that asynchronously
    ///     does the same as <see cref="GetErrors" />.
    /// </summary>
    public abstract Task<int> GetErrorsAsync(ErrorLogFilterCollection errorLogFilters, int errorIndex, int pageSize, ICollection<ErrorLogEntry> entries, CancellationToken cancellationToken);
}