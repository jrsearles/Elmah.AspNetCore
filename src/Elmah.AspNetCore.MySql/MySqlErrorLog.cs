﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elmah.AspNetCore.Xml;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;

namespace Elmah.AspNetCore.MySql;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses MySQL
///     as its backing store.
/// </summary>
public class MySqlErrorLog : ErrorLog
{
    private volatile bool _checkTableExists = false;

    /// <summary>
    ///     Initializes a new instance of the <see cref="MySqlErrorLog" /> class
    ///     using a dictionary of configured settings.
    /// </summary>
    public MySqlErrorLog(IOptions<MySqlErrorLogOptions> option) : this(option.Value.ConnectionString, option.Value.CreateTablesIfNotExist)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="MySqlErrorLog" /> class
    ///     to use a specific connection string for connecting to the database.
    /// </summary>
    public MySqlErrorLog(string connectionString, bool checkTableExists)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        ConnectionString = connectionString;
        _checkTableExists = checkTableExists;
    }

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>
    public override string Name => "MySQL Error Log";

    /// <summary>
    ///     Gets the connection string used by the log to connect to the database.
    /// </summary>
    public virtual string ConnectionString { get; }

    public override async Task LogAsync(Error error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);

        await this.EnsureTableExistsAsync(cancellationToken);

        var errorXml = ErrorXml.EncodeString(error);

        using var connection = new MySqlConnection(ConnectionString);
        using var command = CommandExtension.LogError(error.Id, ApplicationName, error.HostName, error.Type, error.Source, error.Message, error.User, error.StatusCode, error.Time, errorXml);
        await connection.OpenAsync(cancellationToken);
        command.Connection = connection;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<ErrorLogEntry?> GetErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        await this.EnsureTableExistsAsync(cancellationToken);

        string? errorXml;

        using var connection = new MySqlConnection(ConnectionString);
        using var command = CommandExtension.GetErrorXml(ApplicationName, id);

        command.Connection = connection;
        await connection.OpenAsync(cancellationToken);
        errorXml = (string?)await command.ExecuteScalarAsync(cancellationToken);

        if (errorXml == null)
        {
            return null;
        }

        var error = ErrorXml.DecodeString(id, errorXml);
        return new ErrorLogEntry(this, error);
    }

    public override async Task<int> GetErrorsAsync(ErrorLogFilterCollection filters, int errorIndex, int pageSize,
        ICollection<ErrorLogEntry> errorEntryList, CancellationToken cancellationToken)
    {
        if (errorIndex < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(errorIndex), errorIndex, null);
        }

        if (pageSize < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, null);
        }

        await this.EnsureTableExistsAsync(cancellationToken);

        using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using (var command = CommandExtension.GetErrorsXml(ApplicationName, errorIndex, pageSize))
        {
            command.Connection = connection;

            using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var id = reader.GetGuid(0);
                var xml = reader.GetString(1);
                var error = ErrorXml.DecodeString(id, xml);
                errorEntryList.Add(new ErrorLogEntry(this, error));
            }
        }

        return await GetTotalErrorsXml(connection, cancellationToken);
    }

    /// <summary>
    ///     Creates the necessary tables used by this implementation
    /// </summary>
    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        if (!_checkTableExists)
        {
            return;
        }

        _checkTableExists = false;

        using var connection = new MySqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        var databaseName = connection.Database;

        using var commandCheck = CommandExtension.CheckTable(databaseName);
        commandCheck.Connection = connection;
        var exists = Convert.ToBoolean(await commandCheck.ExecuteScalarAsync(cancellationToken));

        if (!exists)
        {
            using var commandCreate = CommandExtension.CreateTable();
            commandCreate.Connection = connection;
            await commandCreate.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<int> GetTotalErrorsXml(MySqlConnection connection, CancellationToken cancellationToken)
    {
        using var command = CommandExtension.GetTotalErrorsXml(ApplicationName);
        command.Connection = connection;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }
}