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

    // Toggle 1er / 2º quinteto
    private int _quintetIndex;                       // 0 = primer quinteto, 1 = segundo
    private Button _btnFirstQuintet, _btnSecondQuintet;
    private Label _fivePanelTitle, _fivePanelSubtitle, _fivePanelBadge;
    private Label _rookPanelTitle, _rookPanelSubtitle, _rookPanelBadge;
    private List<PlayerAwardInfo> _allStar1 = new(), _allStar2 = new();
    private List<PlayerAwardInfo> _allRookie1 = new(), _allRookie2 = new();

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

        _btnFirstQuintet = _root.Q<Button>("BtnFirstQuintet");
        _btnSecondQuintet = _root.Q<Button>("BtnSecondQuintet");
        _fivePanelTitle = _root.Q<Label>("FivePanelTitle");
        _fivePanelSubtitle = _root.Q<Label>("FivePanelSubtitle");
        _fivePanelBadge = _root.Q<Label>("FivePanelBadge");
        _rookPanelTitle = _root.Q<Label>("RookPanelTitle");
        _rookPanelSubtitle = _root.Q<Label>("RookPanelSubtitle");
        _rookPanelBadge = _root.Q<Label>("RookPanelBadge");
    }

    protected override void RegisterCallbacks()
    {
        var btnEnd = _root.Q<Button>("BtnEndSeason");
        if (btnEnd != null)
        {
            btnEnd.RegisterCallback<ClickEvent>(_ => { PlayClick(); ScreenManager.Instance.GoTo(GameScreen.EndSeason); });
            CursorManager.Instance?.RegisterHandCursor(btnEnd);
        }

        // Toggle 1er / 2º quinteto (ambos paneles cambian a la vez).
        if (_btnFirstQuintet != null)
        {
            _btnFirstQuintet.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectQuintet(0); });
            CursorManager.Instance?.RegisterHandCursor(_btnFirstQuintet);
        }
        if (_btnSecondQuintet != null)
        {
            _btnSecondQuintet.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectQuintet(1); });
            CursorManager.Instance?.RegisterHandCursor(_btnSecondQuintet);
        }
    }

    /// <summary>Cambia entre el primer y el segundo quinteto (y repinta).</summary>
    void SelectQuintet(int index)
    {
        _quintetIndex = Mathf.Clamp(index, 0, 1);
        PaintQuintets();
    }

    protected override void Refresh()
    {
        if (_season == null || _manager == null) return;
        RefreshHeader();
        _quintetIndex = 0;      // al entrar siempre se muestra el primer quinteto
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

        // Primeros y segundos quintetos (el segundo es solo informativo: no se persiste).
        var allStar = DatabaseManager.Instance.GetAllStarTeams(seasonId, managerId);
        var allRookie = DatabaseManager.Instance.GetAllRookieTeams(seasonId, managerId);

        _allStar1 = allStar.Count > 0 ? allStar[0] : new List<PlayerAwardInfo>();
        _allStar2 = allStar.Count > 1 ? allStar[1] : new List<PlayerAwardInfo>();
        _allRookie1 = allRookie.Count > 0 ? allRookie[0] : new List<PlayerAwardInfo>();
        _allRookie2 = allRookie.Count > 1 ? allRookie[1] : new List<PlayerAwardInfo>();

        PaintQuintets();
    }

    /// <summary>Pinta el quinteto seleccionado (1º o 2º) en los dos paneles.</summary>
    void PaintQuintets()
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var teamByName = new Dictionary<string, TeamData>();
        foreach (var t in allTeams)
            teamByName[t.name] = t;

        bool second = _quintetIndex == 1;
        var allStar = second ? _allStar2 : _allStar1;
        var allRookie = second ? _allRookie2 : _allRookie1;

        ClearCards("Five");
        ClearCards("Rook");
        foreach (var p in allStar) PopulatePlayer("Five", p, teamByName);
        foreach (var p in allRookie) PopulatePlayer("Rook", p, teamByName);

        // Títulos, subtítulos y badges del panel según el quinteto mostrado.
        if (_fivePanelTitle != null) _fivePanelTitle.text = second ? "SEGUNDO QUINTETO" : "MEJOR QUINTETO";
        if (_fivePanelSubtitle != null) _fivePanelSubtitle.text = second ? "SEGUNDO EQUIPO" : "DE LA TEMPORADA";
        if (_fivePanelBadge != null) _fivePanelBadge.text = second ? "ALL-NBA 2º" : "ALL-NBA";
        if (_rookPanelTitle != null) _rookPanelTitle.text = second ? "SEGUNDO QUINTETO" : "MEJOR QUINTETO";
        if (_rookPanelSubtitle != null) _rookPanelSubtitle.text = second ? "SEGUNDO EQUIPO ROOKIE" : "DE ROOKIES";
        if (_rookPanelBadge != null) _rookPanelBadge.text = second ? "ROOKIE TEAM 2º" : "ROOKIE TEAM";

        // Estado activo del toggle.
        _btnFirstQuintet?.EnableInClassList("qt-toggle-btn--active", !second);
        _btnSecondQuintet?.EnableInClassList("qt-toggle-btn--active", second);
    }

    /// <summary>Deja las tarjetas de un panel en blanco antes de repintarlas.</summary>
    void ClearCards(string prefix)
    {
        foreach (var slot in _posToSlot.Values)
        {
            var photo = _root.Q<VisualElement>($"{prefix}Photo_{slot}");
            if (photo != null) photo.style.backgroundImage = new StyleBackground();

            var nameLbl = _root.Q<Label>($"{prefix}Name_{slot}");
            if (nameLbl != null) nameLbl.text = "";

            var teamLbl = _root.Q<Label>($"{prefix}Team_{slot}");
            if (teamLbl != null) teamLbl.text = "";

            var pts = _root.Q<Label>($"{prefix}Pts_{slot}");
            var reb = _root.Q<Label>($"{prefix}Reb_{slot}");
            var ast = _root.Q<Label>($"{prefix}Ast_{slot}");
            if (pts != null) pts.text = "--";
            if (reb != null) reb.text = "--";
            if (ast != null) ast.text = "--";
        }
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
