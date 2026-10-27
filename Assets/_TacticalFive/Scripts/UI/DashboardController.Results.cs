using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Resultados y clasificación: premios, último/próximo partido y standings.
public partial class DashboardController
{
    void EvaluateMonthlyAwards(System.DateTime evaluationDate)
    {
        string prevMonthStart, prevMonthEnd;
        int prevMonth = evaluationDate.Month - 1;
        int prevYear = evaluationDate.Year;
        if (prevMonth == 0) { prevMonth = 12; prevYear--; }

        // Awards are for the PREVIOUS month's games, so use the previous month's name
        string monthName = new System.DateTime(prevYear, prevMonth, 1).ToString("MMMM", new System.Globalization.CultureInfo("es-ES"));

        Debug.Log($"[Dashboard] EvaluateMonthlyAwards: evalDate={evaluationDate:yyyy-MM-dd}, prevMonth={prevMonth}, prevYear={prevYear}, monthName={monthName}");

        if (prevMonth == 4 && evaluationDate.Month == 5)
        {
            // End-of-season April evaluation: Apr 1 to end of regular season
            prevMonthStart = $"{prevYear}-04-01";
            prevMonthEnd = _season.current_date;
        }
        else
        {
            prevMonthStart = $"{prevYear}-{prevMonth:D2}-01";
            prevMonthEnd = $"{prevYear}-{prevMonth:D2}-{System.DateTime.DaysInMonth(prevYear, prevMonth)}";
        }

        var winners = DatabaseManager.Instance.EvaluateMonthlyAwards(
            _season.id, monthName, prevMonthStart, prevMonthEnd, _manager.id, _myTeam.id);

        if (winners.Count == 0) return;

        // Build news message
        var managers = winners.Where(w => w.award_type == "manager").OrderBy(w => w.rank).ToList();
        var players = winners.Where(w => w.award_type == "player").OrderBy(w => w.rank).ToList();
        var rookies = winners.Where(w => w.award_type == "rookie").OrderBy(w => w.rank).ToList();

        var body = new System.Text.StringBuilder();
        body.AppendLine($"<b>MANAGER DEL MES - {monthName}</b>");
        foreach (var m in managers)
        {
            string mgrName = m.team_id.HasValue
                ? DatabaseManager.Instance.GetManagerNameByTeamId(m.team_id.Value)
                : null;
            if (string.IsNullOrEmpty(mgrName))
                mgrName = m.team_name;
            body.AppendLine($"{m.rank}. {mgrName} ({(m.value * 100).ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}%)");
        }
        body.AppendLine();
        body.AppendLine($"<b>JUGADOR DEL MES - {monthName}</b>");
        foreach (var p in players)
            body.AppendLine($"{p.rank}. {p.player_name} ({p.team_name}) - {p.value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} VAL");
        body.AppendLine();
        body.AppendLine($"<b>ROOKIE DEL MES - {monthName}</b>");
        foreach (var r in rookies)
            body.AppendLine($"{r.rank}. {r.player_name} ({r.team_name}) - {r.value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)} VAL");

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 2,
            sender_id = 0,
            title = $"Premios del Mes de {monthName}",
            body = body.ToString(),
            game_day = _season.current_game_day,
            game_date = _season.current_date,
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            date_sent = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0,
            category = NewsCategory.LIGA,
            news_type = NewsType.PREMIOS_MES
        });
    }

    // ── ÚLTIMO PARTIDO ───────────────────────────────────

    void RefreshLastGame()
    {
        var last = DatabaseManager.Instance.GetLastPlayedGame(_manager.id, _myTeam.id);

        if (last == null)
        {
            _noLastGame.style.display = DisplayStyle.Flex;
            _lastGameResult.style.display = DisplayStyle.None;
            _lastGameDate.text = "";
            if (_lastGameMeta != null) _lastGameMeta.style.display = DisplayStyle.None;
            return;
        }

        _noLastGame.style.display = DisplayStyle.None;
        _lastGameResult.style.display = DisplayStyle.Flex;
        if (_lastGameMeta != null) _lastGameMeta.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == last.home_team_id);
        var away = _allTeams.Find(t => t.id == last.away_team_id);

        SetTeamLogo(_lastHomeLog, home?.logo, "64x64");
        SetTeamLogo(_lastAwayLog, away?.logo, "64x64");
        _lastHomeName.text = home?.name.ToUpper() ?? "";
        _lastAwayName.text = away?.name.ToUpper() ?? "";
        _lastHomeScore.text = last.home_score.ToString();
        _lastAwayScore.text = last.away_score.ToString();

        if (_lastHomeRecord != null) _lastHomeRecord.text = GetTeamRecordShort(last.home_team_id);
        if (_lastAwayRecord != null) _lastAwayRecord.text = GetTeamRecordShort(last.away_team_id);

        // Force layout recalculation after scores are set to prevent overflow on first load
        _lastGameResult?.MarkDirtyRepaint();

        try
        {
            _lastGameDate.text = System.DateTime.Parse(last.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _lastGameDate.text = last.game_date;
        }

        // Badge victoria/derrota
        bool myTeamIsHome = last.home_team_id == _myTeam.id;
        int myScore = myTeamIsHome ? last.home_score : last.away_score;
        int oppScore = myTeamIsHome ? last.away_score : last.home_score;
        bool won = myScore > oppScore;

        _lastResultBadge.text = won ? "VICTORIA" : "DERROTA";
        _lastResultBadge.RemoveFromClassList("badge-win");
        _lastResultBadge.RemoveFromClassList("badge-loss");
        _lastResultBadge.AddToClassList(won ? "badge-win" : "badge-loss");

        // Meta
        var lastHomeTeam = _allTeams.Find(t => t.id == last.home_team_id);

        _lastGameArena.text = lastHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(lastHomeTeam.id)?.arena ?? ""
            : "";
    }

    string GetTeamRecordShort(int teamId)
    {
        if (_teamRecordCache == null)
        {
            _teamRecordCache = new Dictionary<int, (int w, int l)>();
            void Add(int id, bool won)
            {
                var cur = _teamRecordCache.TryGetValue(id, out var r) ? r : (0, 0);
                _teamRecordCache[id] = won ? (cur.Item1 + 1, cur.Item2) : (cur.Item1, cur.Item2 + 1);
            }
            foreach (var g in _allGames)
            {
                if (g.is_played != 1 || g.game_type != "regular") continue;
                Add(g.home_team_id, g.home_score > g.away_score);
                Add(g.away_team_id, g.away_score > g.home_score);
            }
        }
        var rec = _teamRecordCache.TryGetValue(teamId, out var r2) ? r2 : (0, 0);
        return $"{rec.Item1} - {rec.Item2}";
    }

    // ── PRÓXIMO PARTIDO ──────────────────────────────────

    void RefreshNextGame()
    {
        var next = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);

        if (next == null)
        {
            _noNextGame.style.display = DisplayStyle.Flex;
            _nextGameResult.style.display = DisplayStyle.None;
            _nextGameDate.text = "";
            if (_nextGameMeta != null) _nextGameMeta.style.display = DisplayStyle.None;
            return;
        }

        _noNextGame.style.display = DisplayStyle.None;
        _nextGameResult.style.display = DisplayStyle.Flex;
        if (_nextGameMeta != null) _nextGameMeta.style.display = DisplayStyle.Flex;

        var home = _allTeams.Find(t => t.id == next.home_team_id);
        var away = _allTeams.Find(t => t.id == next.away_team_id);

        SetTeamLogo(_nextHomeLog, home?.logo, "64x64");
        SetTeamLogo(_nextAwayLog, away?.logo, "64x64");
        _nextHomeName.text = home?.name.ToUpper() ?? "";
        _nextAwayName.text = away?.name.ToUpper() ?? "";
        if (_nextHomeRecord != null) _nextHomeRecord.text = GetTeamRecordShort(next.home_team_id);
        if (_nextAwayRecord != null) _nextAwayRecord.text = GetTeamRecordShort(next.away_team_id);
        try
        {
            _nextGameDate.text = System.DateTime.Parse(next.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            _nextGameDate.text = next.game_date;
        }

        // Meta
        var nextHomeTeam = _allTeams.Find(t => t.id == next.home_team_id);

        string arenaText = nextHomeTeam != null
            ? DatabaseManager.Instance.GetTeamById(nextHomeTeam.id)?.arena ?? ""
            : "";

        // F5-03: el jugador ve el viaje y el descanso desde el último partido.
        var previous = DatabaseManager.Instance.GetLastPlayedGame(_manager.id, _myTeam.id);
        if (previous != null && previous.game_type != "preseason"
            && next.game_day > previous.game_day)
        {
            int restDays = TravelHelper.RestDays(previous.game_day, next.game_day);
            int kilometers = TravelHelper.DistanceKilometers(previous.home_team_id, next.home_team_id,
                _allTeams.ToDictionary(t => t.id));
            arenaText += $" · {TravelHelper.Describe(kilometers, restDays)}";
        }

        // F5-03: tramo de gira (partidos consecutivos fuera).
        if (next.away_team_id == _myTeam.id)
        {
            var schedule = DatabaseManager.Instance.GetTeamSchedule(_manager.id, _myTeam.id);
            int index = schedule.FindIndex(g => g.id == next.id);
            if (index >= 0)
            {
                int start = index;
                while (start > 0 && schedule[start - 1].away_team_id == _myTeam.id) start--;
                int end = index;
                while (end + 1 < schedule.Count && schedule[end + 1].away_team_id == _myTeam.id) end++;
                int tripGames = end - start + 1;
                if (tripGames > 1) arenaText += $" · GIRA: {tripGames} partidos fuera";
            }
        }

        _nextGameArena.text = arenaText;
    }

    // ── CLASIFICACIÓN ────────────────────────────────────

    void ShowStandings(string conf)
    {
        _currentConf = conf;

        _tabEast.RemoveFromClassList("conf-tab--active");
        _tabWest.RemoveFromClassList("conf-tab--active");
        if (conf == "East") _tabEast.AddToClassList("conf-tab--active");
        else _tabWest.AddToClassList("conf-tab--active");

        var confTeams = _allTeams.Where(t => t.conference == conf).ToList();
        var standings = BuildStandings(confTeams).Take(10).ToList();

        _standingsBody.Clear();

        foreach (var row in standings)
        {
            var team = _allTeams.Find(t => t.id == row.teamId);
            var rowElem = CreateStandingsRow(row, team);
            _standingsBody.Add(rowElem);
        }
    }

    List<StandingRow> BuildStandings(List<TeamData> confTeams)
    {
        return StandingsHelper.Build(confTeams, _allGames);
    }

    VisualElement CreateStandingsRow(StandingRow row, TeamData team)
    {
        var elem = new VisualElement();
        elem.AddToClassList("standings-row");

        bool isMyTeam = team != null && team.id == _myTeam.id;
        if (isMyTeam) elem.AddToClassList("standings-row--my-team");
        else if (row.rank <= 6) elem.AddToClassList("standings-row--playoff");
        else if (row.rank <= 10) elem.AddToClassList("standings-row--playin");

        int gp = row.wins + row.losses;
        float pct = gp > 0 ? (float)row.wins / gp : 0f;
        var streak = CalcStreak(row.games);

        // Rank
        var rankLbl = new Label();
        rankLbl.AddToClassList("col-rank");
        rankLbl.text = row.rank.ToString();

        // Logo (32x32 desde carpeta específica, con crop)
        var logoElem = new VisualElement();
        logoElem.AddToClassList("col-team-logo");
        if (team != null)
        {
            var logo32 = TeamImages.Logo(32, team.logo);
            if (logo32 != null)
                logoElem.style.backgroundImage = new StyleBackground(logo32);
            else
                SetTeamLogo(logoElem, team.logo);
        }

        // Nombre
        var nameLbl = new Label();
        nameLbl.AddToClassList("col-team-name");
        nameLbl.text = team?.name ?? "???";

        // PJ
        var gpLbl = new Label();
        gpLbl.AddToClassList("col-stat");
        gpLbl.text = gp.ToString();

        // Victorias
        var wLbl = new Label();
        wLbl.AddToClassList("col-stat");
        wLbl.AddToClassList("col-wins");
        wLbl.text = row.wins.ToString();

        // Derrotas
        var lLbl = new Label();
        lLbl.AddToClassList("col-stat");
        lLbl.AddToClassList("col-losses");
        lLbl.text = row.losses.ToString();

        // %
        var pctLbl = new Label();
        pctLbl.AddToClassList("col-stat");
        pctLbl.text = pct.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

        // Racha
        var streakLbl = new Label();
        streakLbl.AddToClassList("col-streak");
        streakLbl.text = streak.text;
        streakLbl.AddToClassList(streak.type == "win" ? "streak-win" :
                                  streak.type == "loss" ? "streak-loss" : "streak-none");

        elem.Add(rankLbl);
        elem.Add(logoElem);
        elem.Add(nameLbl);
        elem.Add(gpLbl);
        elem.Add(wLbl);
        elem.Add(lLbl);
        elem.Add(pctLbl);
        elem.Add(streakLbl);

        return elem;
    }

    (string text, string type) CalcStreak(List<bool> games)
    {
        if (games == null || games.Count == 0) return ("-", "none");
        bool last = games[games.Count - 1];
        int count = 0;
        for (int i = games.Count - 1; i >= 0; i--)
        {
            if (games[i] == last) count++;
            else break;
        }
        return last ? ($"{count}V", "win") : ($"{count}D", "loss");
    }

}
