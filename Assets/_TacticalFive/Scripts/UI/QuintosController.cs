using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System;

public class QuintosController : UIScreenController
{
    // Position code → UI slot prefix
    private static readonly Dictionary<string, string> _posToSlot = new()
    {
        { "C",  "P" },   // Pívot
        { "PF", "AP" },  // Ala-Pívot
        { "SF", "AL" },  // Alero
        { "PG", "B" },   // Base
        { "SG", "E" },   // Escolta
    };

    private static readonly Dictionary<string, string> _posBadge = new()
    {
        { "C",  "P" },
        { "PF", "AP" },
        { "SF", "AL" },
        { "PG", "B" },
        { "SG", "E" },
    };

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerSeason;
    private Label _headerDate;

    protected override void OnEnable()
    {
        base.OnEnable();
        CursorManager.Instance?.SetDefaultCursor();
    }

    protected override void CacheReferences()
    {
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
    }

    protected override void RegisterCallbacks()
    {
        var btnEnd = _root.Q<Button>("BtnEndSeason");
        if (btnEnd != null)
        {
            btnEnd.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.EndSeason); });
            CursorManager.Instance?.RegisterHandCursor(btnEnd);
        }
    }

    protected override void Refresh()
    {
        if (_season == null || _manager == null) return;
        RefreshHeader();
        RefreshQuintets();
    }

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        var logos = TeamImages.Logos(64);
        var logoDict = new Dictionary<string, Sprite>();
        foreach (var s in logos) logoDict[s.name] = s;
        if (logoDict.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
        if (!string.IsNullOrEmpty(_season.current_date))
        {
            if (DateTime.TryParse(_season.current_date, out var dt))
                _headerDate.text = dt.ToString("dd/MM/yyyy");
        }
    }

    void RefreshQuintets()
    {
        int seasonId = _season.id;
        int managerId = _manager.id;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var teamByName = new Dictionary<string, TeamData>();
        foreach (var t in allTeams)
            teamByName[t.name] = t;

        // All-NBA quintet
        var allStar = DatabaseManager.Instance.GetAllStarTeam(seasonId, managerId);
        foreach (var p in allStar)
            PopulatePlayer("Five", p, teamByName);

        // Rookie quintet
        var allRookie = DatabaseManager.Instance.GetAllRookieTeam(seasonId, managerId);
        foreach (var p in allRookie)
            PopulatePlayer("Rook", p, teamByName);
    }

    void PopulatePlayer(string prefix, PlayerAwardInfo player, Dictionary<string, TeamData> teamByName)
    {
        if (!_posToSlot.TryGetValue(player.Position, out string slot))
            return;

        // Photo
        var photo = _root.Q<VisualElement>($"{prefix}Photo_{slot}");
        if (photo != null)
        {
            Texture2D tex = PlayerPhotoHelper.Load(player.PlayerId, player.Photo);
            if (tex != null)
                photo.style.backgroundImage = new StyleBackground(tex);
        }

        // Name
        var nameLbl = _root.Q<Label>($"{prefix}Name_{slot}");
        if (nameLbl != null)
            nameLbl.text = player.PlayerName.ToUpper();

        // Team
        var teamLbl = _root.Q<Label>($"{prefix}Team_{slot}");
        if (teamLbl != null)
            teamLbl.text = player.TeamName.ToUpper();

        // Stats
        var pts = _root.Q<Label>($"{prefix}Pts_{slot}");
        var reb = _root.Q<Label>($"{prefix}Reb_{slot}");
        var ast = _root.Q<Label>($"{prefix}Ast_{slot}");
        if (pts != null) pts.text = player.AvgPts.ToString("F1");
        if (reb != null) reb.text = player.AvgReb.ToString("F1");
        if (ast != null) ast.text = player.AvgAst.ToString("F1");
    }
}
