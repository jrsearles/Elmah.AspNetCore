using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Elmah.AspNetCore.Xml;
using Microsoft.Extensions.Options;
using Npgsql;
using NpgsqlTypes;

namespace Elmah.AspNetCore.Postgresql;

/// <summary>
///     An <see cref="ErrorLog" /> implementation that uses PostgreSQL
///     as its backing store.
/// </summary>
public class PgsqlErrorLog : ErrorLog
{
    private const int MaxAppNameLength = 60;
    private volatile bool _checkTableExists = false;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PgsqlErrorLog" /> class
    ///     using a dictionary of configured settings.
    /// </summary>
    public PgsqlErrorLog(IOptions<PgsqlErrorLogOptions> option) : this(option.Value.ConnectionString, option.Value.CreateTablesIfNotExist)
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="PgsqlErrorLog" /> class
    ///     to use a specific connection string for connecting to the database.
    /// </summary>
    public PgsqlErrorLog(string connectionString, bool createTablesIfNotExist)
    {
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new ArgumentNullException(nameof(connectionString));
        }

        ConnectionString = connectionString;

        _checkTableExists = createTablesIfNotExist;
    }

    /// <summary>
    ///     Gets the name of this error log implementation.
    /// </summary>
    public override string Name => "PostgreSQL Error Log";

    /// <summary>
    ///     Gets the connection string used by the log to connect to the database.
    /// </summary>
    public virtual string ConnectionString { get; }

    /// <summary>
    ///     Gets the connection string used by the log to connect to the database.
    /// </summary>
    public override async Task LogAsync(Error error, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(error);

        await this.EnsureTableExistsAsync(cancellationToken);
        
        var errorXml = ErrorXml.EncodeString(error);

        using var connection = new NpgsqlConnection(ConnectionString);
        using var command = Commands.LogError(error.Id, ApplicationName, error.HostName, error.Type, error.Source,
            error.Message, error.User, error.StatusCode, error.Time, errorXml);
        command.Connection = connection;

        await connection.OpenAsync(cancellationToken);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public override async Task<ErrorLogEntry?> GetErrorAsync(Guid id, CancellationToken cancellationToken)
    {
        await this.EnsureTableExistsAsync(cancellationToken);

        string? errorXml;

        using var connection = new NpgsqlConnection(ConnectionString);
        using (var command = Commands.GetErrorXml(ApplicationName, id))
        {
            command.Connection = connection;
            await connection.OpenAsync(cancellationToken);
            errorXml = (string?)await command.ExecuteScalarAsync(cancellationToken);
        }

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

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using (var command = Commands.GetErrorsXml(ApplicationName, errorIndex, pageSize))
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

        using (var command = Commands.GetErrorsXmlTotal(ApplicationName))
        {
            command.Connection = connection;
            return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
        }
    }

    /// <summary>
    ///     Creates the necessary tables and sequences used by this implementation
    /// </summary>
    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        if (!_checkTableExists)
        {
            return;
        }

        _checkTableExists = false;

        using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);

        using var cmdCheck = Commands.CheckTable();
        cmdCheck.Connection = connection;

        // ReSharper disable once PossibleNullReferenceException
        var exists = (bool)(await cmdCheck.ExecuteScalarAsync(cancellationToken))!;

        if (!exists)
        {
            using var cmdCreate = Commands.CreateTable();
            cmdCreate.Connection = connection;
            await cmdCreate.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static class Commands
    {
        public static NpgsqlCommand CheckTable()
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
SELECT EXISTS (
   SELECT 1
   FROM   information_schema.tables 
   WHERE  table_schema = 'public'
   AND    table_name = 'elmah_error'
   )
";
            return command;
        }

        public static NpgsqlCommand CreateTable()
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
CREATE SEQUENCE ELMAH_Error_SEQUENCE;
CREATE TABLE ELMAH_Error
(
    ErrorId		UUID NOT NULL,
    Application	VARCHAR(60) NOT NULL,
    Host 		VARCHAR(50) NOT NULL,
    Type		VARCHAR(100) NOT NULL,
    Source		VARCHAR(60)  NOT NULL,
    Message		VARCHAR(500) NOT NULL,
    ""User""		VARCHAR(50)  NOT NULL,
    StatusCode	INT NOT NULL,
    TimeUtc		TIMESTAMP NOT NULL,
    Sequence	INT NOT NULL DEFAULT NEXTVAL('ELMAH_Error_SEQUENCE'),
    AllXml		TEXT NOT NULL
);

ALTER TABLE ELMAH_Error ADD CONSTRAINT PK_ELMAH_Error PRIMARY KEY (ErrorId);

CREATE INDEX IX_ELMAH_Error_App_Time_Seq ON ELMAH_Error USING BTREE
(
    Application   ASC,
    TimeUtc       DESC,
    Sequence      DESC
);
";

            return command;
        }

        public static NpgsqlCommand LogError(
            Guid id,
            string appName,
            string hostName,
            string typeName,
            string source,
            string message,
            string user,
            int statusCode,
            DateTime time,
            string xml)
        {
            var command = new NpgsqlCommand();
            command.CommandText =
                @"
/* elmah */
INSERT INTO Elmah_Error (ErrorId, Application, Host, Type, Source, Message, ""User"", StatusCode, TimeUtc, AllXml)
VALUES (@ErrorId, @Application, @Host, @Type, @Source, @Message, @User, @StatusCode, @TimeUtc, @AllXml)
";
            command.Parameters.Add(new NpgsqlParameter("ErrorId", id));
            command.Parameters.Add(new NpgsqlParameter("Application", appName));
            command.Parameters.Add(new NpgsqlParameter("Host", hostName));
            command.Parameters.Add(new NpgsqlParameter("Type", typeName));
            command.Parameters.Add(new NpgsqlParameter("Source", source));
            command.Parameters.Add(new NpgsqlParameter("Message", message));
            command.Parameters.Add(new NpgsqlParameter("User", user));
            command.Parameters.Add(new NpgsqlParameter("StatusCode", statusCode));
            command.Parameters.Add(new NpgsqlParameter("TimeUtc", time.ToUniversalTime()));
            command.Parameters.Add(new NpgsqlParameter("AllXml", xml));

            return command;
        }

        public static NpgsqlCommand GetErrorXml(string appName, Guid id)
        {
            var command = new NpgsqlCommand();

            command.CommandText =
                @"
SELECT AllXml FROM Elmah_Error 
WHERE 
    Application = @Application 
    AND ErrorId = @ErrorId
";

            command.Parameters.Add(new NpgsqlParameter("Application", appName));
            command.Parameters.Add(new NpgsqlParameter("ErrorId", id));

            return command;
        }

        public static NpgsqlCommand GetErrorsXml(string appName, int errorIndex, int pageSize)
        {
            var command = new NpgsqlCommand();

            command.CommandText =
                @"
SELECT ErrorId, AllXml FROM Elmah_Error
WHERE
    Application = @Application
ORDER BY Sequence DESC
OFFSET @offset
LIMIT @limit
";

            command.Parameters.Add("@Application", NpgsqlDbType.Text, MaxAppNameLength).Value = appName;
            command.Parameters.Add("@offset", NpgsqlDbType.Integer).Value = errorIndex;
            command.Parameters.Add("@limit", NpgsqlDbType.Integer).Value = pageSize;

            return command;
        }

        public static NpgsqlCommand GetErrorsXmlTotal(string appName)
        {
            var command = new NpgsqlCommand();
            command.CommandText = "SELECT COUNT(*) FROM Elmah_Error WHERE Application = @Application";
            command.Parameters.Add("@Application", NpgsqlDbType.Text, MaxAppNameLength).Value = appName;
            return command;
        }
    }
}