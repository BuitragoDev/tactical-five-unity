using SQLite;

/// <summary>
/// Resultado de cada intento de migración (F8-06). Las migraciones aplicadas
/// siguen viviendo en `schema_migrations`; esta tabla conserva también los fallos,
/// que antes solo aparecían en el log.
/// </summary>
[Table("schema_migration_attempts")]
public class SchemaMigrationAttemptData
{
    [PrimaryKey, AutoIncrement]
    public int id { get; set; }
    [Indexed]
    public string name { get; set; }
    public string status { get; set; }
    public string error { get; set; }
    public string attempted_at { get; set; }
}

/// <summary>Fallo bloqueante de una migración obligatoria de la base de datos.</summary>
public class DatabaseMigrationException : System.Exception
{
    public string MigrationName { get; }

    public DatabaseMigrationException(string migrationName, System.Exception inner)
        : base($"Migración obligatoria '{migrationName}' falló: {inner?.Message}", inner)
    {
        MigrationName = migrationName;
    }
}
