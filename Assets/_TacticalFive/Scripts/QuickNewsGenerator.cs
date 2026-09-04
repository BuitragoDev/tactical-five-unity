using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public static class QuickNewsGenerator
{
    // ── Variantes de texto (5 por evento, se eligen al azar) ─────────────

    static readonly string[] HitoMidVariants =
    {
        "¡Mitad de temporada superada! {0} llega al ecuador del calendario regular con {1} victorias y {2} derrotas.",
        "Se cumplen 41 jornadas. {0} afronta la segunda mitad del calendario con {1} triunfos en su casillero.",
        "El ecuador de la temporada deja a {0} con un balance de {1}–{2}. La segunda vuelta decidirá su destino.",
        "¡41 partidos jugados! {0} atraviesa el ecuador de la fase regular con {1} victorias y mucho por decidir.",
        "Media temporada completada. {0} cierra la primera vuelta con {1} triunfos y la mirada puesta en la recta final."
    };

    static readonly string[] HitoEndVariants =
    {
        "La fase regular llega a su fin. {0} cierra con {1} victorias y {2} derrotas en la última jornada.",
        "Se acabó el calendario regular. {0} finaliza la campaña con un balance de {1}–{2} y la vista puesta en la postemporada.",
        "Última jornada completada. {0} dice adiós a la fase regular con {1} triunfos y las sensaciones justas de cara a los playoffs.",
        "¡82 partidos! {0} pone el broche a la temporada regular con {1} victorias en el casillero.",
        "Termina la fase regular. Con {1} victorias, {0} se prepara para lo que viene: todo se decide en la postemporada."
    };

    static readonly string[] WinStreakVariants =
    {
        "{0} no conoce la derrota: {1} victorias consecutivas tras vencer a {2} por {3}.",
        "{0} sigue imparable: su racha alcanza las {1} victorias seguidas después de superar a {2}.",
        "¡En racha! {0} encadena {1} triunfos consecutivos y ya es uno de los equipos más en forma de la liga.",
        "El vestuario de {0} está eufórico: {1} victorias consecutivas y una dinámica que asusta a sus rivales.",
        "{0} ha convertido la victoria en un hábito: {1} triunfos seguidos tras batir a {2} por {3}."
    };

    static readonly string[] LoseStreakVariants =
    {
        "{0} atraviesa su peor momento: {1} derrotas consecutivas y las alarmas encendidas.",
        "La crisis se instala en {0}: ya son {1} derrotas seguidas y el vestuario empieza a resquebrajarse.",
        "Mala racha en {0}: {1} tropiezos consecutivos que le cuestan posiciones en la clasificación.",
        "{0} no levanta cabeza. Con {1} derrotas seguidas, la paciencia de la afición se agota.",
        "Los números no engañan: {0} suma {1} derrotas consecutivas y necesita una reacción inmediata."
    };

    static readonly string[] UpsetVariants =
    {
        "¡Campanada en la liga! {0} ({3} de valoración) sorprende a {1} ({4}) por {2}.",
        "Nadie daba un duro por {0}, pero se lleva el triunfo ante {1} por {2}. ¡Qué golpe sobre la mesa!",
        "El favoritismo no sirvió de nada: {1} ({4}) cae ante {0} ({3}) por {2}.",
        "¡Sorpresa mayúscula! {0}, sin nada que perder, desarbola a {1} por {2} y firma la campanada de la jornada.",
        "Los pronósticos saltaron por los aires: {0} ({3}) supera al favorito {1} ({4}) por {2}."
    };

    static readonly string[] TripleDoubleVariants =
    {
        "{0} firma un triple-doble de ensueño ({1}) en el duelo ante {2}.",
        "Noche histórica para {0}: triple-doble ({1}) contra {2} para el recuerdo.",
        "{0} domina todas las facetas del juego: triple-doble ({1}) ante {2}.",
        "Solo hay una palabra para la actuación de {0}: total. Triple-doble ({1}) frente a {2}.",
        "Hacer de todo y hacerlo bien. {0} logra un triple-doble ({1}) en el duelo contra {2}."
    };

    static readonly string[] ExplosionVariants =
    {
        "{0} se sale: {1} puntos ante {2} en una exhibición ofensiva de otro nivel.",
        "La noche es de {0}: {1} puntos ante {2} para enmarcar.",
        "Anotar hasta la extenuación: {0} firma {1} puntos contra {2}.",
        "{0} pone la liga en alerta con {1} puntos ante {2}.",
        "Exhibición de {0}: {1} puntos frente a {2} y el marcador temblando en cada posesión."
    };

    static string PickVariant(string[] variants, params object[] args)
    {
        return string.Format(variants[UnityEngine.Random.Range(0, variants.Length)], args);
    }

    public static void Generate(ManagerData manager, TeamData myTeam, SeasonData season, List<GameData> gamesToday, int gameDay, string gameDate)
    {
        if (season == null || gamesToday == null || gamesToday.Count == 0) return;
        if (season.phase != "regular") return;

        var allStandingsGames = DatabaseManager.Instance.GetStandingsGames(manager.id);
        var allTeams = DatabaseManager.Instance.GetAllTeams().ToDictionary(t => t.id);
        var teamRealAvg = allTeams.Values.ToDictionary(
            t => t.id,
            t => {
                var players = DatabaseManager.Instance.GetPlayersByTeam(t.id);
                return players.Count > 0 ? (int)Math.Round(players.Average(p => p.GetCalculatedAverage())) : 50;
            });
        int newsCount = 0;

        int myTeamGames = allStandingsGames.Count(g =>
            (g.home_team_id == myTeam.id || g.away_team_id == myTeam.id) && g.is_played == 1);
        if ((myTeamGames == 41 || myTeamGames == 82) && season.phase == "regular")
        {
            int wins = allStandingsGames.Count(g =>
                (g.home_team_id == myTeam.id || g.away_team_id == myTeam.id) && g.is_played == 1
                && ((g.home_team_id == myTeam.id && g.home_score > g.away_score)
                    || (g.away_team_id == myTeam.id && g.away_score > g.home_score)));
            int losses = myTeamGames - wins;
            string body = myTeamGames == 41
                ? PickVariant(HitoMidVariants, myTeam.name, wins, losses)
                : PickVariant(HitoEndVariants, myTeam.name, wins, losses);
            SaveNews(manager, gameDay, gameDate, "Hito", body);
            newsCount++;
        }

        foreach (var game in gamesToday.OrderBy(g => g.id))
        {
            if (newsCount >= 2) break;

            if (!allTeams.TryGetValue(game.home_team_id, out var homeTeam)) continue;
            if (!allTeams.TryGetValue(game.away_team_id, out var awayTeam)) continue;

            int homeStreak = ComputeStreak(allStandingsGames, game.home_team_id);
            int awayStreak = ComputeStreak(allStandingsGames, game.away_team_id);

            if (newsCount < 2 && homeStreak >= 5)
            {
                string score = $"{game.home_score}-{game.away_score}";
                if (SaveNews(manager, gameDay, gameDate, "Racha de Victorias",
                    PickVariant(WinStreakVariants, homeTeam.name, homeStreak, awayTeam.name, score)))
                    { newsCount++; continue; }
            }
            if (newsCount < 2 && awayStreak >= 5)
            {
                string score = $"{game.home_score}-{game.away_score}";
                if (SaveNews(manager, gameDay, gameDate, "Racha de Victorias",
                    PickVariant(WinStreakVariants, awayTeam.name, awayStreak, homeTeam.name, score)))
                    { newsCount++; continue; }
            }

            if (newsCount < 2 && homeStreak <= -5)
            {
                if (SaveNews(manager, gameDay, gameDate, "Mala Racha",
                    PickVariant(LoseStreakVariants, homeTeam.name, -homeStreak)))
                    { newsCount++; continue; }
            }
            if (newsCount < 2 && awayStreak <= -5)
            {
                if (SaveNews(manager, gameDay, gameDate, "Mala Racha",
                    PickVariant(LoseStreakVariants, awayTeam.name, -awayStreak)))
                    { newsCount++; continue; }
            }

            int homeAvg = teamRealAvg[game.home_team_id];
            int awayAvg = teamRealAvg[game.away_team_id];
            int medDiff = Mathf.Abs(homeAvg - awayAvg);
            bool homeFav = homeAvg > awayAvg;
            bool homeWon = game.home_score > game.away_score;
            if (newsCount < 2 && medDiff >= 15 && ((homeFav && !homeWon) || (!homeFav && homeWon)))
            {
                var winner = homeWon ? homeTeam : awayTeam;
                var loser = homeWon ? awayTeam : homeTeam;
                int winAvg = homeWon ? homeAvg : awayAvg;
                int loseAvg = homeWon ? awayAvg : homeAvg;
                string score = $"{game.home_score}-{game.away_score}";
                if (SaveNews(manager, gameDay, gameDate, "Campanada",
                    PickVariant(UpsetVariants, winner.name, loser.name, score, winAvg, loseAvg)))
                    { newsCount++; continue; }
            }

            if (newsCount >= 2) continue;

            var gameStats = DatabaseManager.Instance.GetGamePlayerStats(game.id);
            if (gameStats == null) continue;

            foreach (var ps in gameStats)
            {
                if (newsCount >= 2) break;

                if (ps.triple_double == 1)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                    if (player == null) continue;
                    var opponent = game.home_team_id == player.team_id ? awayTeam : homeTeam;

                    var tdParts = new List<string>();
                    if (ps.points >= 10) tdParts.Add($"{ps.points} pts");
                    if (ps.rebounds >= 10) tdParts.Add($"{ps.rebounds} reb");
                    if (ps.assists >= 10) tdParts.Add($"{ps.assists} ast");
                    if (ps.steals >= 10) tdParts.Add($"{ps.steals} rob");
                    if (ps.blocks >= 10) tdParts.Add($"{ps.blocks} tap");

                    if (SaveNews(manager, gameDay, gameDate, "Triple-Doble",
                        PickVariant(TripleDoubleVariants, $"{player.first_name} {player.last_name}", string.Join(" + ", tdParts.Take(3)), opponent.name)))
                        { newsCount++; continue; }
                }

                if (ps.points >= 40)
                {
                    var player = DatabaseManager.Instance.GetPlayerById(ps.player_id);
                    if (player == null) continue;
                    var opponent = game.home_team_id == player.team_id ? awayTeam : homeTeam;
                    if (SaveNews(manager, gameDay, gameDate, "Explosión",
                        PickVariant(ExplosionVariants, $"{player.first_name} {player.last_name}", ps.points, opponent.name)))
                        { newsCount++; continue; }
                }
            }
        }
    }

    static int ComputeStreak(List<GameData> allGames, int teamId)
    {
        var teamGames = allGames
            .Where(g => g.home_team_id == teamId || g.away_team_id == teamId)
            .OrderByDescending(g => g.game_day)
            .ThenByDescending(g => g.id)
            .ToList();

        if (teamGames.Count == 0) return 0;

        int streak = 0;
        bool firstWon = false;
        bool firstSet = false;

        foreach (var g in teamGames)
        {
            bool isHome = g.home_team_id == teamId;
            int teamScore = isHome ? g.home_score : g.away_score;
            int oppScore = isHome ? g.away_score : g.home_score;
            bool won = teamScore > oppScore;

            if (!firstSet)
            {
                firstWon = won;
                firstSet = true;
                streak = 1;
                continue;
            }

            if (won == firstWon)
                streak++;
            else
                break;
        }

        return firstWon ? streak : -streak;
    }

    static bool SaveNews(ManagerData manager, int gameDay, string gameDate, string title, string body)
    {
        var existing = DatabaseManager.Instance.Db.Table<MessageData>()
            .FirstOrDefault(m => m.title == title && m.body == body && m.game_day == gameDay);
        if (existing != null) return false;

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = manager.id,
            sender_type = 2,
            sender_id = 0,
            title = title,
            body = body,
            game_day = gameDay,
            game_date = gameDate,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        });
        return true;
    }
}