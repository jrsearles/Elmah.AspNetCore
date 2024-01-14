namespace Elmah.AspNetCore.MySql;

public class MySqlErrorLogOptions
{
    /// <summary>
    ///     Database connection string.
    /// </summary>
    public string ConnectionString { get; set; } = default!;

    /// <summary>
    /// Indicate if the CreateTables check should be run when initializing the logger.
    /// Defaults to true
    /// </summary>
    public bool CreateTablesIfNotExist { get; set; } = true;
}
