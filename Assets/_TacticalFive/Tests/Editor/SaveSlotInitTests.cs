using System.IO;
using System.Reflection;
using NUnit.Framework;
using SQLite;
using UnityEngine;

/// <summary>
/// Regresión del alta de partida en una instalación nueva (máquina recién formateada,
/// sin `template.db`, que hasta ahora solo generaba el Editor).
///
/// `InitSaveSlot` sembraba los datos estáticos dentro de su propia transacción y
/// `SeedPlayers`/`SeedFreeAgents` abrían otra: sqlite-net lanza
/// InvalidOperationException al anidar, así que el alta devolvía false y en el menú
/// "no pasaba nada" al pulsar Liga Manager.
///
/// Todo el trabajo se hace en una carpeta temporal (los tres overrides de test), de
/// modo que no toca ni las partidas ni las imágenes del jugador.
/// </summary>
public class SaveSlotInitTests
{
    string _root;
    GameObject _go;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "tf_slotinit_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);

        GameSaveManager.BaseDirOverride  = Path.Combine(_root, "saves");
        DatabaseManager.DataRootOverride = Path.Combine(_root, "data");
        CustomScopes.RootOverride        = Path.Combine(_root, "custom");

        _go = new GameObject("DatabaseManagerTest");
        var db = _go.AddComponent<DatabaseManager>();
        // En EditMode no se ejecuta Awake (solo al entrar en Play), así que el
        // singleton se registra a mano para que los helpers internos lo encuentren.
        SetInstance(db);
    }

    static void SetInstance(DatabaseManager db) =>
        typeof(DatabaseManager)
            .GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)
            ?.SetValue(null, db);

    [TearDown]
    public void TearDown()
    {
        DatabaseManager.Instance?.CloseActiveSlot();
        SetInstance(null);
        if (_go != null) Object.DestroyImmediate(_go);
        GameSaveManager.BaseDirOverride  = null;
        DatabaseManager.DataRootOverride = null;
        CustomScopes.RootOverride        = null;
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    // ── SAV-02: partida nueva SIN template.db ────────────────────────────────

    [Test]
    public void InitSaveSlot_SinTemplate_CreaLaPartidaCompleta()
    {
        var db = DatabaseManager.Instance;
        Assert.That(File.Exists(db.TemplateDbPath), Is.False, "El escenario del bug es NO tener template.db");

        bool ok = db.InitSaveSlot(1);

        Assert.That(ok, Is.True, $"Debe poder crear la partida sin template ({db.LastInitError})");
        Assert.That(db.GetAllTeams().Count, Is.EqualTo(30), "Deben quedar los 30 equipos");
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0), "Deben quedar los jugadores sembrados");
        Assert.That(db.GetGLeagueTeams().Count, Is.EqualTo(30), "Deben quedar las 30 filiales G-League");
        Assert.That(File.Exists(db.TemplateDbPath), Is.True,
            "El template debe generarse al vuelo para las siguientes partidas");
    }

    // ── SAV-03: la segunda partida reutiliza el template ────────────────────

    [Test]
    public void InitSaveSlot_SegundaPartida_ClonaDelTemplate()
    {
        var db = DatabaseManager.Instance;

        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);
        Assert.That(db.InitSaveSlot(2), Is.True, db.LastInitError);

        Assert.That(db.GetAllTeams().Count, Is.EqualTo(30));
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0), "El clon debe traer los jugadores");
        Assert.That(db.Db.Table<LeagueSettingsData>().Count(), Is.GreaterThan(0), "El clon debe traer el convenio");
    }

    // ── SAV-04: los seeders no abren transacciones anidadas ─────────────────

    [Test]
    public void SeedFreeAgents_DentroDeUnaTransaccion_NoLanza()
    {
        var db = DatabaseManager.Instance;
        Assert.That(db.InitSaveSlot(1), Is.True, db.LastInitError);

        // La partida nueva se siembra dentro de _db.RunInTransaction: los seeders
        // deben participar en ella, no abrir otra (sqlite-net lanza al anidar).
        db.Db.DeleteAll<PlayerData>();

        Assert.DoesNotThrow(() => db.Db.RunInTransaction(() => db.SeedFreeAgents()));
        Assert.That(db.Db.Table<PlayerData>().Count(), Is.GreaterThan(0));
    }
}
