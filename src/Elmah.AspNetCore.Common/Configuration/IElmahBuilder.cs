using System;
using Microsoft.Extensions.DependencyInjection;

namespace Elmah.AspNetCore;

/// <summary>
/// Builder for configuring Elmah.
/// </summary>
public interface IElmahBuilder
{
    /// <summary>
    /// Gets the service collection.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Registers an <see cref="ErrorLog"/> to be used with Elmah.
    /// </summary>
    /// <typeparam name="T">The <see cref="ElmahLog"/> concrete type</typeparam>
    void PersistTo<T>() where T : ErrorLog;

    /// <summary>
    /// Registers an <see cref="ErrorLog"/> factory method to be used with Elmah.
    /// </summary>
    /// <param name="factory">The factory method</param>
    void PersistTo(Func<IServiceProvider, ErrorLog> factory);

    /// <summary>
    /// Registers an <see cref="ErrorLog"/> instance to be used with Elmah.
    /// </summary>
    /// <param name="log">The <see cref="ErrorLog"/> instance</param>
    void PersistTo(ErrorLog log);
}
