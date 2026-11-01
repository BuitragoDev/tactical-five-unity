using System.Collections.Generic;
using NUnit.Framework;

public class ObjectiveHelperTests
{
    [Test]
    public void IsObjectiveMet_RankZero_NeverMet()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Zona tranquila", 0), Is.False);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 0), Is.False);
        Assert.That(ObjectiveHelper.IsObjectiveMet(null, 0), Is.False);
    }

    [Test]
    public void IsObjectiveMet_ZonaTranquila_Threshold12()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Zona tranquila", 12), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Zona tranquila", 13), Is.False);
    }

    [Test]
    public void IsObjectiveMet_PlayIn_Threshold10()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Play-In", 10), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Play-In", 11), Is.False);
    }

    [Test]
    public void IsObjectiveMet_Playoffs_Threshold6()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Playoffs", 6), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Playoffs", 7), Is.False);
    }

    [Test]
    public void IsObjectiveMet_Campeonato_Threshold2()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 1), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 2), Is.True);
        Assert.That(ObjectiveHelper.IsObjectiveMet("Campeonato", 3), Is.False);
    }

    [Test]
    public void IsObjectiveMet_UnknownObjective_NeverMet()
    {
        Assert.That(ObjectiveHelper.IsObjectiveMet("Inexistente", 1), Is.False);
    }

    // ── Objetivo dinámico de temporada (opción B: plantilla + resultado) ──

    [Test]
    public void ObjectiveForStrengthRank_UsaTramosDe30Equipos()
    {
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(1), Is.EqualTo(ObjectiveHelper.CAMPEONATO));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(5), Is.EqualTo(ObjectiveHelper.CAMPEONATO));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(6), Is.EqualTo(ObjectiveHelper.PLAYOFFS));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(12), Is.EqualTo(ObjectiveHelper.PLAYOFFS));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(13), Is.EqualTo(ObjectiveHelper.PLAY_IN));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(22), Is.EqualTo(ObjectiveHelper.PLAY_IN));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(23), Is.EqualTo(ObjectiveHelper.ZONA_TRANQUILA));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(30), Is.EqualTo(ObjectiveHelper.ZONA_TRANQUILA));
        Assert.That(ObjectiveHelper.ObjectiveForStrengthRank(0), Is.Null);
    }

    [Test]
    public void NextObjective_Campeon_DefiendeTituloAunqueLaPlantillaSeaDebil()
    {
        string next = ObjectiveHelper.NextObjective(
            ObjectiveHelper.ZONA_TRANQUILA, 10, 28, champion: true, finalist: false);
        Assert.That(next, Is.EqualTo(ObjectiveHelper.CAMPEONATO));
    }

    [Test]
    public void NextObjective_Finalista_ComoMinimoPlayoffs()
    {
        string next = ObjectiveHelper.NextObjective(
            ObjectiveHelper.ZONA_TRANQUILA, 5, 25, champion: false, finalist: true);
        Assert.That(next, Is.EqualTo(ObjectiveHelper.PLAYOFFS));
    }

    [Test]
    public void NextObjective_CumplioObjetivo_NoBajaElListon()
    {
        // Campeonato cumplido (2º) aunque la plantilla caiga al puesto 25.
        string next = ObjectiveHelper.NextObjective(
            ObjectiveHelper.CAMPEONATO, 2, 25, champion: false, finalist: false);
        Assert.That(next, Is.EqualTo(ObjectiveHelper.CAMPEONATO));
    }

    [Test]
    public void NextObjective_FalloObjetivo_UsaLaBaseDePlantilla()
    {
        // Playoffs fallado (12º): manda la fuerza real de la plantilla.
        Assert.That(ObjectiveHelper.NextObjective(
            ObjectiveHelper.PLAYOFFS, 12, 20, false, false), Is.EqualTo(ObjectiveHelper.PLAY_IN));
        Assert.That(ObjectiveHelper.NextObjective(
            ObjectiveHelper.PLAYOFFS, 12, 3, false, false), Is.EqualTo(ObjectiveHelper.CAMPEONATO));
    }

    [Test]
    public void NextObjective_SinDatosDePlantilla_DevuelveNull()
    {
        Assert.That(ObjectiveHelper.NextObjective(
            ObjectiveHelper.PLAYOFFS, 6, 0, false, false), Is.Null);
    }

    [Test]
    public void Higher_EligeElMasExigente()
    {
        Assert.That(ObjectiveHelper.Higher(ObjectiveHelper.PLAY_IN, ObjectiveHelper.CAMPEONATO),
            Is.EqualTo(ObjectiveHelper.CAMPEONATO));
        Assert.That(ObjectiveHelper.Higher(ObjectiveHelper.PLAYOFFS, null),
            Is.EqualTo(ObjectiveHelper.PLAYOFFS));
        Assert.That(ObjectiveHelper.Higher(null, null), Is.Null);
    }

    // Un East con 2 equipos; B ganó 1:0.
    static List<TeamData> TwoEastTeams()
    {
        return new List<TeamData>
        {
            new TeamData { id = 1, conference = "East", name = "EastA" },
            new TeamData { id = 2, conference = "East", name = "EastB" },
            new TeamData { id = 3, conference = "West", name = "WestA" }
        };
    }

    static GameData Regular(int home, int away, int hs, int as_)
    {
        return new GameData
        {
            home_team_id = home,
            away_team_id = away,
            home_score = hs,
            away_score = as_,
            is_played = 1,
            game_type = "regular"
        };
    }

    [Test]
    public void GetConferenceRank_TopTeam_IsRank1()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>
        {
            Regular(1, 2, 110, 100)
        };

        Assert.That(ObjectiveHelper.GetConferenceRank(1, "East", teams, games), Is.EqualTo(1));
        Assert.That(ObjectiveHelper.GetConferenceRank(2, "East", teams, games), Is.EqualTo(2));
    }

    [Test]
    public void GetConferenceRank_WestTeam_MatchesWestRank()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>
        {
            Regular(1, 2, 110, 100),
            Regular(3, 1, 120, 90)
        };
        // El equipo 3 es el único del Oeste con partido jugado -> rank 1 en su conferencia.
        Assert.That(ObjectiveHelper.GetConferenceRank(3, "West", teams, games), Is.EqualTo(1));
    }

    [Test]
    public void GetConferenceRank_TeamNotInConference_ReturnsZero()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>();
        Assert.That(ObjectiveHelper.GetConferenceRank(999, "East", teams, games), Is.EqualTo(0));
    }

    [Test]
    public void GetConferenceRank_IgnoresNonRegularAndUnplayed()
    {
        var teams = TwoEastTeams();
        var games = new List<GameData>
        {
            Regular(1, 2, 110, 100),
            new GameData { home_team_id = 2, away_team_id = 1, home_score = 99, away_score = 98, is_played = 1, game_type = "playoff" },
            new GameData { home_team_id = 2, away_team_id = 1, home_score = 99, away_score = 98, is_played = 0, game_type = "regular" }
        };
        // Solo cuenta el partido regular jugado (EastA ganó) -> EastA rank 1.
        Assert.That(ObjectiveHelper.GetConferenceRank(1, "East", teams, games), Is.EqualTo(1));
        Assert.That(ObjectiveHelper.GetConferenceRank(2, "East", teams, games), Is.EqualTo(2));
    }
}