using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;

public class PremiosController : UIScreenController
{
    VisualElement _tableHeader;
    VisualElement _tableBody;
    Button _tabManager;
    Button _tabPlayer;
    Button _tabRookie;
    string _activeTab = "manager";

    protected override GameScreen ScreenId => GameScreen.Premios;

    protected override void CacheReferences()
    {
        _tableHeader = _root.Q<VisualElement>("PremiosTableHeader");
        _tableBody = _root.Q<VisualElement>("PremiosTableBody");
        _tabManager = _root.Q<Button>("TabManager");
        _tabPlayer = _root.Q<Button>("TabPlayer");
        _tabRookie = _root.Q<Button>("TabRookie");
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();
        _tabManager?.RegisterCallback<ClickEvent>(OnTabManager);
        _tabPlayer?.RegisterCallback<ClickEvent>(OnTabPlayer);
        _tabRookie?.RegisterCallback<ClickEvent>(OnTabRookie);
    }

    void OnTabManager(ClickEvent evt) => SwitchTab("manager");
    void OnTabPlayer(ClickEvent evt) => SwitchTab("player");
    void OnTabRookie(ClickEvent evt) => SwitchTab("rookie");

    void SwitchTab(string tab)
    {
        PlayClick();
        _activeTab = tab;
        _tabManager?.EnableInClassList("standings-tab--active", tab == "manager");
        _tabPlayer?.EnableInClassList("standings-tab--active", tab == "player");
        _tabRookie?.EnableInClassList("standings-tab--active", tab == "rookie");
        Refresh();
    }

    protected override void Refresh()
    {
        if (_season == null || _tableBody == null) return;

        var title = _root.Q<Label>("PremiosTitle");
        if (title != null)
            title.text = $"PREMIOS MENSUALES {_season.year_start}-{_season.year_end}";

        var awards = DatabaseManager.Instance.GetMonthlyAwardsForSeason(_season.id);

        string[] monthOrder = { "Noviembre", "Diciembre", "Enero", "Febrero", "Marzo", "Abril" };

        BuildHeader();

        _tableBody.Clear();

        foreach (var month in monthOrder)
        {
            var monthAwards = awards.Where(a =>
                string.Equals(a.month_name, month, System.StringComparison.OrdinalIgnoreCase)).ToList();

            BuildRow(month, monthAwards);
        }
    }

    void BuildHeader()
    {
        _tableHeader.Clear();

        AddHeaderCell(_tableHeader, "MES", "premios-th premios-th-month");
        AddHeaderCell(_tableHeader, "", "premios-th premios-th-photo");
        AddHeaderCell(_tableHeader, "NOMBRE", "premios-th premios-th-name");
        AddHeaderCell(_tableHeader, "EQUIPO", "premios-th premios-th-team");

        if (_activeTab == "manager")
        {
            AddHeaderCell(_tableHeader, "V", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "D", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "%", "premios-th premios-th-stat");
        }
        else
        {
            AddHeaderCell(_tableHeader, "PTOS", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "REB", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "ASIS", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "ROB", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "TAP", "premios-th premios-th-stat");
            AddHeaderCell(_tableHeader, "VAL", "premios-th premios-th-stat");
        }
    }

    void AddHeaderCell(VisualElement parent, string text, string cls)
    {
        var label = new Label(text);
        foreach (var c in cls.Split(' '))
            label.AddToClassList(c);
        parent.Add(label);
    }

    void BuildRow(string month, List<MonthlyAwardData> awards)
    {
        var row = new VisualElement();
        row.AddToClassList("premios-data-row");

        var winner = awards.FirstOrDefault(a => a.rank == 1 && a.award_type == _activeTab);

        // Month
        AddDataCell(row, month.ToUpper(), "premios-td premios-td-month");

        // Photo / Logo
        var photoCell = new VisualElement();
        photoCell.AddToClassList("premios-td-photo");
        if (winner != null)
        {
            if (_activeTab == "manager" && winner.team_id.HasValue)
            {
                var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64");
                var team = DatabaseManager.Instance.GetTeamById(winner.team_id.Value);
                if (team != null)
                {
                    var logo = logos.FirstOrDefault(s => s.name == team.logo);
                    if (logo != null)
                    {
                        var img = new Image();
                        img.sprite = logo;
                        img.AddToClassList("premios-td-photo-img");
                        photoCell.Add(img);
                    }
                }
            }
            else if (winner.player_id.HasValue)
            {
                var player = DatabaseManager.Instance.GetPlayerById(winner.player_id.Value);
                if (player != null)
                {
                    Texture2D tex = PlayerPhotoHelper.Load(player.id, player.photo);
                    if (tex != null)
                    {
                        var img = new Image();
                        img.image = tex;
                        img.AddToClassList("premios-td-photo-img");
                        photoCell.Add(img);
                    }
                }
            }
        }
        row.Add(photoCell);

        // Name
        if (winner != null)
        {
            string name;
            if (_activeTab == "manager")
            {
                name = DatabaseManager.Instance.GetManagerNameByTeamId(winner.team_id ?? 0) ?? winner.team_name;
            }
            else if (winner.player_id.HasValue)
            {
                var player = DatabaseManager.Instance.GetPlayerById(winner.player_id.Value);
                name = player != null ? $"{player.first_name} {player.last_name}" : winner.player_name;
            }
            else
            {
                name = winner.player_name;
            }
            AddDataCell(row, name ?? "—", "premios-td premios-td-name");
        }
        else
        {
            AddDataCell(row, "—", "premios-td premios-td-empty");
        }

        // Team
        AddDataCell(row, winner?.team_name ?? "—", "premios-td premios-td-team");

        // Stats
        if (_activeTab == "manager")
        {
            if (winner != null && winner.team_id.HasValue)
            {
                var record = GetManagerMonthRecord(winner.team_id.Value, month);
                AddDataCell(row, record.wins.ToString(), "premios-td premios-td-stat");
                AddDataCell(row, record.losses.ToString(), "premios-td premios-td-stat");
                AddDataCell(row, record.winPct.ToString("F1", CultureInfo.InvariantCulture), "premios-td premios-td-stat");
            }
            else
            {
                AddDataCell(row, "—", "premios-td premios-td-empty");
                AddDataCell(row, "—", "premios-td premios-td-empty");
                AddDataCell(row, "—", "premios-td premios-td-empty");
            }
        }
        else
        {
            if (winner != null && winner.player_id.HasValue)
            {
                var stats = GetPlayerMonthStats(winner.player_id.Value, month);
                AddStatCell(row, stats.points);
                AddStatCell(row, stats.rebounds);
                AddStatCell(row, stats.assists);
                AddStatCell(row, stats.steals);
                AddStatCell(row, stats.blocks);
                AddStatCell(row, stats.rating);
            }
            else
            {
                for (int i = 0; i < 6; i++)
                    AddDataCell(row, "—", "premios-td premios-td-empty");
            }
        }

        _tableBody.Add(row);
    }

    void AddDataCell(VisualElement parent, string text, string cls)
    {
        var label = new Label(text);
        foreach (var c in cls.Split(' '))
            label.AddToClassList(c);
        parent.Add(label);
    }

    void AddStatCell(VisualElement parent, float value)
    {
        var cell = new Label(value.ToString("F1", CultureInfo.InvariantCulture));
        cell.AddToClassList("premios-td");
        cell.AddToClassList("premios-td-stat");
        cell.AddToClassList("premios-td-stat-value");
        parent.Add(cell);
    }

    struct MonthRecord
    {
        public int wins;
        public int losses;
        public float winPct;
    }

    MonthRecord GetManagerMonthRecord(int teamId, string monthName)
    {
        var result = new MonthRecord();
        if (_season == null) return result;

        string startDate = GetMonthStartDate(monthName);
        string endDate = GetMonthEndDate(monthName);

        if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate)) return result;

        var db = DatabaseManager.Instance.Db;
        var games = db.Query<GameData>(@"
            SELECT * FROM games
            WHERE season_id = ? AND is_played = 1 AND game_type = 'regular'
              AND game_date >= ? AND game_date <= ?
              AND (home_team_id = ? OR away_team_id = ?)",
            _season.id, startDate, endDate, teamId, teamId);

        foreach (var g in games)
        {
            bool won = (g.home_team_id == teamId && g.home_score > g.away_score) ||
                       (g.away_team_id == teamId && g.away_score > g.home_score);
            if (won) result.wins++;
            else result.losses++;
        }

        int total = result.wins + result.losses;
        result.winPct = total > 0 ? (float)result.wins / total * 100f : 0f;
        return result;
    }

    struct MonthPlayerStats
    {
        public float points;
        public float rebounds;
        public float assists;
        public float steals;
        public float blocks;
        public float rating;
    }

    MonthPlayerStats GetPlayerMonthStats(int playerId, string monthName)
    {
        var result = new MonthPlayerStats();
        if (_season == null) return result;

        string startDate = GetMonthStartDate(monthName);
        string endDate = GetMonthEndDate(monthName);

        if (string.IsNullOrEmpty(startDate) || string.IsNullOrEmpty(endDate)) return result;

        var db = DatabaseManager.Instance.Db;
        var row = db.Query<MonthlyPlayerStatRow>(@"
            SELECT AVG(ps.points) AS points, AVG(ps.rebounds) AS rebounds,
                   AVG(ps.assists) AS assists, AVG(ps.steals) AS steals,
                   AVG(ps.blocks) AS blocks, AVG(ps.rating) AS rating
            FROM player_game_stats ps
            JOIN games g ON ps.game_id = g.id
            WHERE g.season_id = ? AND g.is_played = 1 AND g.game_type = 'regular'
              AND g.game_date >= ? AND g.game_date <= ?
              AND ps.player_id = ?",
            _season.id, startDate, endDate, playerId).FirstOrDefault();

        if (row != null)
        {
            result.points = (float)row.points;
            result.rebounds = (float)row.rebounds;
            result.assists = (float)row.assists;
            result.steals = (float)row.steals;
            result.blocks = (float)row.blocks;
            result.rating = (float)row.rating;
        }

        return result;
    }

    public class MonthlyPlayerStatRow
    {
        public double points { get; set; }
        public double rebounds { get; set; }
        public double assists { get; set; }
        public double steals { get; set; }
        public double blocks { get; set; }
        public double rating { get; set; }
    }

    string GetMonthStartDate(string monthName)
    {
        if (_season == null) return null;
        int year = monthName == "Noviembre" || monthName == "Diciembre"
            ? _season.year_start : _season.year_end;

        return monthName.ToLower() switch
        {
            "noviembre" => $"{year}-11-01",
            "diciembre" => $"{year}-12-01",
            "enero"     => $"{year}-01-01",
            "febrero"   => $"{year}-02-01",
            "marzo"     => $"{year}-03-01",
            "abril"     => $"{year}-04-01",
            _ => null
        };
    }

    string GetMonthEndDate(string monthName)
    {
        if (_season == null) return null;
        int year = monthName == "Noviembre" || monthName == "Diciembre"
            ? _season.year_start : _season.year_end;

        return monthName.ToLower() switch
        {
            "noviembre" => $"{year}-11-30",
            "diciembre" => $"{year}-12-31",
            "enero"     => $"{year}-01-31",
            "febrero"   => $"{year}-02-28",
            "marzo"     => $"{year}-03-31",
            "abril"     => $"{year}-04-30",
            _ => null
        };
    }
}
