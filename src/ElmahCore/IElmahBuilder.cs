﻿using System;
using Microsoft.Extensions.DependencyInjection;

namespace ElmahCore;

public interface IElmahBuilder
{
    IServiceCollection Services { get; }

    void PersistTo<T>() where T : ErrorLog;

    void PersistTo(Func<IServiceProvider, ErrorLog> factory);

    void PersistTo(ErrorLog log);
}