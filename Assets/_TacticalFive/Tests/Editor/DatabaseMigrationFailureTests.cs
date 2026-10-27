using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SQLite;
using UnityEngine;

/// <summary>
/// F8-06: una migración obligatoria que falla no debe avanzar user_version ni
/// quedar como éxito silencioso. El intento fallido queda registrado.
/// </summary>
public class DatabaseMigrationFailureTests
{
    string _dir;
    SQLiteConnection _db;
    GameObject _go;
    DatabaseManager _manager;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tf_migration_failure_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _db = new SQLiteConnection(Path.Combine(_dir, "broken.db"));
        _db.CreateTable<SchemaMigrationAttemptData>();

        // Una vista con el nombre players hace fallar el primer ALTER TABLE.
        _db.Execute("CREATE VIEW players AS SELECT 1 AS id");

        _go = new GameObject("DatabaseMigrationFailureTest");
        _manager = _go.AddComponent<DatabaseManager>();
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, _manager);
        typeof(DatabaseManager)
            .GetField("_dbField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.SetValue(_manager, _db);
    }

    [TearDown]
    public void TearDown()
    {
        try { _db?.Close(); } catch { }
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, null);
        if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    [Test]
    public void RunMigrations_Failure_DoesNotAdvanceUserVersionAndLogsAttempt()
    {
        var runMigrations = typeof(DatabaseManager)
            .GetMethod("RunMigrations", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.That(runMigrations, Is.Not.Null);

        var ex = Assert.Throws<TargetInvocationException>(() => runMigrations.Invoke(_manager, null));
        Assert.That(ex.InnerException, Is.Not.Null);
        Assert.That(_manager.LastMigrationError, Is.Not.Null.And.Not.Empty);
        Assert.That(_db.ExecuteScalar<int>("PRAGMA user_version"), Is.EqualTo(0),
            "Una migración fallida no puede avanzar la versión de esquema");
        Assert.That(_db.ExecuteScalar<int>(
            "SELECT COUNT(*) FROM schema_migration_attempts WHERE status = 'failed'"), Is.GreaterThan(0),
            "El intento fallido debe quedar registrado");
    }
}
