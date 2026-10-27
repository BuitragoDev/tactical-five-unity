using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// F8-08: las relaciones declaradas se validan con LEFT JOIN y el borrado de un
/// slot elimina su base de datos y sus artefactos, sin dejar huérfanos.
/// </summary>
public class ReferentialIntegrityTests
{
    string _root;
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tf_integrity_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        GameSaveManager.BaseDirOverride = Path.Combine(_root, "saves");
        DatabaseManager.DataRootOverride = Path.Combine(_root, "data");
        CustomScopes.RootOverride = Path.Combine(_root, "custom");

        _go = new GameObject("DatabaseIntegrityTest");
        var db = _go.AddComponent<DatabaseManager>();
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, db);
    }

    [TearDown]
    public void TearDown()
    {
        DatabaseManager.Instance?.CloseActiveSlot();
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, null);
        if (_go != null) Object.DestroyImmediate(_go);
        GameSaveManager.BaseDirOverride = null;
        DatabaseManager.DataRootOverride = null;
        CustomScopes.RootOverride = null;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    [Test]
    public void SeededSlot_HasNoDeclaredOrphans()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var orphans = db.ValidateReferentialIntegrity();
        Assert.That(orphans, Is.Empty,
            "El seed no debe dejar relaciones huérfanas:\n" + string.Join("\n", orphans));
    }

    [Test]
    public void Validator_DetectsAnOrphanPlayerTeam()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        var orphan = new PlayerData
        {
            id = 999999,
            team_id = 999999,
            first_name = "Test",
            last_name = "Orphan",
            position = "PG",
            overall = 70
        };
        db.Db.Insert(orphan);

        var issue = db.ValidateReferentialIntegrity()
            .FirstOrDefault(o => o.childTable == "players" && o.childColumn == "team_id");
        Assert.That(issue, Is.Not.Null);
        Assert.That(issue.count, Is.GreaterThanOrEqualTo(1));

        db.Db.Delete<PlayerData>(orphan.id);
    }

    [Test]
    public void DeleteSave_RemovesTheSlotDatabase()
    {
        const int slot = 999;
        string path = GameSaveManager.GetSaveDbPath(slot);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
        File.WriteAllText(Path.Combine(GameSaveManager.BaseDirOverride ?? Path.GetDirectoryName(path), "saves.json"),
            "{\"slots\":[{\"slotNumber\":999,\"exists\":true,\"managerName\":\"Temp\"}]}");

        GameSaveManager.DeleteSave(slot);

        Assert.That(File.Exists(path), Is.False, "El borrado del slot debe eliminar su .db");
        Assert.That(GameSaveManager.GetSlot(slot)?.exists ?? false, Is.False,
            "El metadata del slot borrado no debe seguir existiendo");
    }
}
