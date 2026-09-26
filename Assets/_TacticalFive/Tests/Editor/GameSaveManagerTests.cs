using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>
/// Tests del sistema de slots (Fase 1 de la auditoría). Usan una carpeta temporal
/// vía GameSaveManager.BaseDirOverride, así que no tocan las partidas del jugador
/// ni necesitan una DatabaseManager viva.
/// </summary>
public class GameSaveManagerTests
{
    string _dir;

    [SetUp]
    public void SetUp()
    {
        _dir = Path.Combine(Path.GetTempPath(), "tf_saves_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        GameSaveManager.BaseDirOverride = _dir;
        // Estos tests provocan a propósito la rama de "metadata ilegible", que
        // registra un Debug.LogError; el framework lo contaría como fallo.
        LogAssert.ignoreFailingMessages = true;
    }

    [TearDown]
    public void TearDown()
    {
        LogAssert.ignoreFailingMessages = false;
        GameSaveManager.BaseDirOverride = null;
        try { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); } catch { }
    }

    static void WriteDb(string path) => File.WriteAllBytes(path, new byte[] { 1, 2, 3 });
    string DbPath(int slot) => GameSaveManager.GetSaveDbPath(slot);

    // ── SAV-01: un saves.json ilegible NO debe borrar partidas ──

    [Test]
    public void CleanupAllOrphanDbs_CorruptMeta_KeepsDatabases()
    {
        WriteDb(DbPath(1));
        WriteDb(DbPath(2));
        File.WriteAllText(Path.Combine(_dir, "saves.json"), "{ esto no es json valido");

        GameSaveManager.CleanupAllOrphanDbs();

        Assert.That(File.Exists(DbPath(1)), Is.True, "No debe borrar DBs si el metadata es ilegible");
        Assert.That(File.Exists(DbPath(2)), Is.True, "No debe borrar DBs si el metadata es ilegible");
    }

    [Test]
    public void CleanupAllOrphanDbs_CorruptMetaWithoutBackup_ReportsUnreadable()
    {
        WriteDb(DbPath(1));
        File.WriteAllText(Path.Combine(_dir, "saves.json"), "roto");

        bool ok = GameSaveManager.TryGetAllSlots(out var slots);

        Assert.That(ok, Is.False, "Debe indicar que el metadata no se pudo leer");
        Assert.That(slots, Is.Empty);
    }

    // ── Comportamiento original conservado cuando el metadata es válido ──

    [Test]
    public void CleanupAllOrphanDbs_ValidMeta_DeletesOnlyOrphans()
    {
        WriteDb(DbPath(1));
        WriteDb(DbPath(2));
        File.WriteAllText(Path.Combine(_dir, "saves.json"),
            "{\"slots\":[{\"slotNumber\":1,\"exists\":true,\"managerName\":\"GM\"}]}");

        GameSaveManager.CleanupAllOrphanDbs();

        Assert.That(File.Exists(DbPath(1)), Is.True, "El slot con metadata válido se conserva");
        Assert.That(File.Exists(DbPath(2)), Is.False, "El slot sin metadata sí es huérfano");
    }

    // ── QA-04: se borran también los sidecars de WAL ──

    [Test]
    public void CleanupOrphanDb_RemovesWalAndShm()
    {
        WriteDb(DbPath(3));
        File.WriteAllBytes(DbPath(3) + "-wal", new byte[] { 1 });
        File.WriteAllBytes(DbPath(3) + "-shm", new byte[] { 1 });

        GameSaveManager.CleanupOrphanDb(3);

        Assert.That(File.Exists(DbPath(3)), Is.False);
        Assert.That(File.Exists(DbPath(3) + "-wal"), Is.False, "Debe borrar el -wal");
        Assert.That(File.Exists(DbPath(3) + "-shm"), Is.False, "Debe borrar el -shm");
    }

    // ── Escritura atómica con backup y recuperación ──

    [Test]
    public void SaveSlotInfo_WritesMetaAndKeepsBackup()
    {
        GameSaveManager.SaveSlotInfo(new SaveSlotInfo { slotNumber = 1, exists = true, managerName = "A" });
        GameSaveManager.SaveSlotInfo(new SaveSlotInfo { slotNumber = 2, exists = true, managerName = "B" });

        Assert.That(File.Exists(Path.Combine(_dir, "saves.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(_dir, "saves.json.bak")), Is.True, "La segunda escritura deja un .bak");

        var slots = GameSaveManager.GetAllSlots();
        Assert.That(slots.Count, Is.EqualTo(2), "Los dos slots deben persistir");
    }

    [Test]
    public void SaveSlotInfo_CorruptMeta_IsPreservedAndRebuilt()
    {
        WriteDb(DbPath(1));
        File.WriteAllText(Path.Combine(_dir, "saves.json"), "no json");

        GameSaveManager.SaveSlotInfo(new SaveSlotInfo { slotNumber = 1, exists = true, managerName = "A" });

        Assert.That(Directory.GetFiles(_dir, "saves.json.corrupt-*").Length, Is.EqualTo(1),
            "El metadata dañado se conserva aparte para diagnóstico");
        Assert.That(File.Exists(Path.Combine(_dir, "saves.json")), Is.True, "Se reescribe un metadata nuevo");
        Assert.That(GameSaveManager.GetAllSlots().Count, Is.EqualTo(1));
    }

    [Test]
    public void TryGetAllSlots_CorruptMetaWithBackup_UsesBackup()
    {
        File.WriteAllText(Path.Combine(_dir, "saves.json"), "roto");
        File.WriteAllText(Path.Combine(_dir, "saves.json.bak"),
            "{\"slots\":[{\"slotNumber\":7,\"exists\":false,\"managerName\":\"X\"}]}");

        bool ok = GameSaveManager.TryGetAllSlots(out var slots);

        Assert.That(ok, Is.True, "Con un backup válido debe poder leerse");
        Assert.That(slots.Exists(s => s.slotNumber == 7), Is.True);
    }
}
