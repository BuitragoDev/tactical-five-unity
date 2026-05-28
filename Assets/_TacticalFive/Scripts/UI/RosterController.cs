using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class RosterController : MonoBehaviour
{
    private UIDocument _doc;
    private VisualElement _root;

    // Header
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerSeason;
    private Label _headerDate;
    private Button _btnAction;

    // Summary
    private Label _summaryPlayers;
    private Label _summaryOverall;
    private Label _summaryBudget;

    // Roster list
    private VisualElement _rosterBody;

    // Detail
    private VisualElement _detailEmpty;
    private ScrollView _detailScroll;
    private VisualElement _detailContent;
    private Label _detailPosBadge;
    private Label _detailPlayerName;
    private Label _detailPlayerMeta;
    private Label _detailOvr;
    private Label _detailHealth;
    private VisualElement _detailAttrs;
    private VisualElement _detailSeasonStats;
    private Label _statPts;
    private Label _statReb;
    private Label _statAst;
    private Label _detailContract;
    private Label _detailPotential;
    private Button _btnDismiss;

    // Modal despido
    private VisualElement _dismissOverlay;
    private VisualElement _dismissBox;
    private Label _dismissText1;
    private Label _dismissText2;
    private Button _btnDismissCancel;
    private Button _btnDismissConfirm;

    // Datos
    private ManagerData _manager;
    private TeamData _myTeam;
    private SeasonData _season;
    private List<PlayerData> _players;
    private PlayerData _selectedPlayer;

    private Dictionary<string, Sprite> _logoSprites = new();

    private static readonly Dictionary<string, string> PosLabels = new()
    {
        { "PG", "BASE" },
        { "SG", "ESCOLTA" },
        { "SF", "ALERO" },
        { "PF", "ALA-PIVOT" },
        { "C",  "PIVOT" }
    };

    private static readonly List<string> PosOrder =
        new() { "PG", "SG", "SF", "PF", "C" };

    void OnEnable()
    {
        _doc = GetComponent<UIDocument>();
        _root = _doc.rootVisualElement;

        // Forzar root a ocupar toda la pantalla
        _root.style.position = Position.Absolute;
        _root.style.left = 0; _root.style.right = 0;
        _root.style.top = 0; _root.style.bottom = 0;
        _root.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
        _root.style.height = new StyleLength(new Length(100, LengthUnit.Percent));

        CacheReferences();
        SetupScrollViews();
        LoadData();
        RegisterCallbacks();
        Refresh();
    }

    void CacheReferences()
    {
        // Header
        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerSeason = _root.Q<Label>("HeaderSeason");
        _headerDate = _root.Q<Label>("HeaderDate");
        _btnAction = _root.Q<Button>("BtnAction");

        // Summary
        _summaryPlayers = _root.Q<Label>("SummaryPlayers");
        _summaryOverall = _root.Q<Label>("SummaryOverall");
        _summaryBudget = _root.Q<Label>("SummaryBudget");

        // Roster
        _rosterBody = _root.Q<VisualElement>("RosterBody");

        // Detail
        _detailEmpty = _root.Q<VisualElement>("DetailEmpty");
        _detailScroll = _root.Q<ScrollView>("DetailScroll");
        _detailContent = _root.Q<VisualElement>("DetailContent");
        _detailPosBadge = _root.Q<Label>("DetailPosBadge");
        _detailPlayerName = _root.Q<Label>("DetailPlayerName");
        _detailPlayerMeta = _root.Q<Label>("DetailPlayerMeta");
        _detailOvr = _root.Q<Label>("DetailOvr");
        _detailHealth = _root.Q<Label>("DetailHealth");
        _detailAttrs = _root.Q<VisualElement>("DetailAttrs");
        _detailSeasonStats = _root.Q<VisualElement>("DetailSeasonStats");
        _statPts = _root.Q<Label>("StatPts");
        _statReb = _root.Q<Label>("StatReb");
        _statAst = _root.Q<Label>("StatAst");
        _detailContract = _root.Q<Label>("DetailContract");
        _detailPotential = _root.Q<Label>("DetailPotential");
        _btnDismiss = _root.Q<Button>("BtnDismiss");

        // Modal
        _dismissOverlay = _root.Q<VisualElement>("DismissOverlay");
        _dismissBox = _root.Q<VisualElement>("DismissBox");
        _dismissText1 = _root.Q<Label>("DismissText1");
        _dismissText2 = _root.Q<Label>("DismissText2");
        _btnDismissCancel = _root.Q<Button>("BtnDismissCancel");
        _btnDismissConfirm = _root.Q<Button>("BtnDismissConfirm");
    }

    void SetupScrollViews()
    {
        var rosterScroll = _root.Q<ScrollView>("RosterScroll");
        if (rosterScroll != null)
            rosterScroll.contentContainer.style.flexDirection = FlexDirection.Column;

        if (_detailScroll != null)
            _detailScroll.contentContainer.style.flexDirection = FlexDirection.Column;
    }

    void LoadData()
    {
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        _manager = DatabaseManager.Instance.GetActiveManager();
        _myTeam = DatabaseManager.Instance.GetTeamById(_manager.team_id);
        _season = DatabaseManager.Instance.GetActiveSeason(_manager.id);
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
    }

    void RegisterCallbacks()
    {
        // Sidebar navegación
        _root.Q<Button>("NavDashboard")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));
        _root.Q<Button>("NavRoster")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Roster));
        _root.Q<Button>("NavCalendar")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Calendar));
        _root.Q<Button>("NavStandings")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Standings));
        _root.Q<Button>("NavPalmares")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Palmares));
        _root.Q<Button>("NavResults")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Results));
        _root.Q<Button>("NavPlayoffs")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Playoffs));
        _root.Q<Button>("NavStats")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Stats));
        _root.Q<Button>("NavRecords")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Records));
        _root.Q<Button>("NavMarket")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Market));
        _root.Q<Button>("NavFinances")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Finances));
        _root.Q<Button>("NavSponsors")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Sponsors));
        _root.Q<Button>("NavTV")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.TV));
        _root.Q<Button>("NavArena")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Arena));
        _root.Q<Button>("NavMessages")?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Messages));

        _btnAction?.RegisterCallback<ClickEvent>(_ =>
            ScreenManager.Instance.GoTo(GameScreen.Dashboard));

        // Despido
        _btnDismiss?.RegisterCallback<ClickEvent>(_ => OpenDismissModal());
        _btnDismissCancel?.RegisterCallback<ClickEvent>(_ => CloseDismissModal());
        _btnDismissConfirm?.RegisterCallback<ClickEvent>(_ => ConfirmDismiss());

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(_btnAction);
            CursorManager.Instance.RegisterHandCursor(_btnDismiss);
            CursorManager.Instance.RegisterHandCursor(_btnDismissCancel);
            CursorManager.Instance.RegisterHandCursor(_btnDismissConfirm);
        }
    }

    void Refresh()
    {
        RefreshHeader();
        RefreshSummary();
        BuildRosterList();

        _detailEmpty.style.display = DisplayStyle.Flex;
        _detailScroll.style.display = DisplayStyle.None;
        _dismissOverlay.style.display = DisplayStyle.None;
        _dismissBox.style.display = DisplayStyle.None;
    }

    // ── HEADER ───────────────────────────────────────────

    void RefreshHeader()
    {
        if (_myTeam == null || _manager == null) return;

        if (_logoSprites.TryGetValue(_myTeam.logo, out var sprite))
            _headerTeamLogo.style.backgroundImage = new StyleBackground(sprite);

        _headerTeamName.text = _myTeam.name.ToUpper();
        _headerManagerName.text = $"Manager: {_manager.name}";
        _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";

        long totalPayroll = _players.Sum(p => p.salary);
        _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

        var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
        long salaryCap = leagueSettings?.salary_cap ?? 155_000_000;
        long margin = salaryCap - totalPayroll;

        _headerMargin.text = margin >= 0
            ? $"+${margin / 1_000_000}M"
            : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";

        _headerMargin.RemoveFromClassList("header-stat-value--negative");
        if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");

        if (_season != null)
        {
            _headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            try
            {
                var next = DatabaseManager.Instance.GetNextGame(_manager.id, _myTeam.id);
                _headerDate.text = next != null
                    ? System.DateTime.Parse(next.game_date).ToString("dd/MM/yyyy")
                    : "";
            }
            catch { _headerDate.text = ""; }
        }

        _btnAction.text = "← DASHBOARD";
    }

    // ── SUMMARY ──────────────────────────────────────────

    void RefreshSummary()
    {
        _summaryPlayers.text = _players.Count.ToString();
        int avgOverall = _players.Count > 0
            ? (int)_players.Average(p => p.overall) : 0;
        _summaryOverall.text = avgOverall.ToString();
        _summaryBudget.text = $"${_myTeam.budget / 1_000_000}M";
    }

    // ── ROSTER LIST ──────────────────────────────────────

    void BuildRosterList()
    {
        _rosterBody.Clear();

        foreach (var pos in PosOrder)
        {
            var posPlayers = _players
                .Where(p => p.position == pos)
                .OrderByDescending(p => p.overall)
                .ToList();

            if (posPlayers.Count == 0) continue;

            // Cabecera posición
            var posHeader = new VisualElement();
            posHeader.AddToClassList("pos-header");

            var badge = new Label();
            badge.AddToClassList("pos-badge");
            badge.text = pos;

            var label = new Label();
            label.AddToClassList("pos-label");
            label.text = PosLabels.TryGetValue(pos, out var lbl) ? lbl : pos;

            posHeader.Add(badge);
            posHeader.Add(label);

            var section = new VisualElement();
            section.AddToClassList("pos-section");
            section.Add(posHeader);

            // Filas de jugadores
            for (int i = 0; i < posPlayers.Count; i++)
            {
                var player = posPlayers[i];
                var row = CreatePlayerRow(i + 1, player);
                section.Add(row);
            }

            _rosterBody.Add(section);
        }
    }

    VisualElement CreatePlayerRow(int num, PlayerData player)
    {
        var row = new VisualElement();
        row.AddToClassList("player-row");
        if (player.injury_days > 0)
            row.AddToClassList("player-row--injured");

        var numLbl = new Label();
        numLbl.AddToClassList("player-num");
        numLbl.text = num.ToString("D2");

        var nameLbl = new Label();
        nameLbl.AddToClassList("player-name");
        if (player.injury_days > 0)
            nameLbl.AddToClassList("player-name--injured");
        nameLbl.text = $"{player.first_name} {player.last_name}";

        var ovrLbl = new Label();
        ovrLbl.AddToClassList("player-ovr");
        ovrLbl.text = player.overall.ToString();

        var metaLbl = new Label();
        metaLbl.AddToClassList("player-meta");
        metaLbl.text = $"{player.age} años · {player.height_cm / 100f:F2}m";

        row.Add(numLbl);
        row.Add(nameLbl);
        row.Add(ovrLbl);
        row.Add(metaLbl);

        // Icono último año de contrato
        if (player.contract_years <= 1)
        {
            var icon = new Label();
            icon.AddToClassList("player-icon");
            icon.text = "⚠";
            row.Add(icon);
        }

        // Tag lesión
        if (player.injury_days > 0)
        {
            var injTag = new Label();
            injTag.AddToClassList("player-injury-tag");
            injTag.text = $"🏥 {player.injury_days}d";
            row.Add(injTag);
        }

        // Click
        row.RegisterCallback<ClickEvent>(_ => OnPlayerSelected(player, row));
        if (CursorManager.Instance != null)
        {
            row.RegisterCallback<MouseEnterEvent>(_ =>
                CursorManager.Instance.SetHandCursor());
            row.RegisterCallback<MouseLeaveEvent>(_ =>
                CursorManager.Instance.SetDefaultCursor());
        }

        return row;
    }

    // ── DETALLE JUGADOR ──────────────────────────────────

    void OnPlayerSelected(PlayerData player, VisualElement row)
    {
        _selectedPlayer = player;

        // Quitar selección anterior
        _rosterBody.Query<VisualElement>(className: "player-row--selected")
                   .ForEach(e => e.RemoveFromClassList("player-row--selected"));
        row.AddToClassList("player-row--selected");

        ShowPlayerDetail(player);
    }

    void ShowPlayerDetail(PlayerData p)
    {
        _detailEmpty.style.display = DisplayStyle.None;
        _detailScroll.style.display = DisplayStyle.Flex;

        // Cabecera
        _detailPosBadge.text = p.position;
        _detailPlayerName.text = $"{p.first_name} {p.last_name}".ToUpper();
        _detailPlayerMeta.text = $"{p.age} años · {p.nationality} · {p.height_cm / 100f:F2}m · {p.weight_kg}kg";
        _detailOvr.text = p.overall.ToString();

        // Salud
        if (p.injury_days > 0)
        {
            _detailHealth.text = $"🏥 {p.injury_type} — {p.injury_days} días de baja";
            _detailHealth.RemoveFromClassList("detail-health-ok");
            _detailHealth.AddToClassList("detail-health-injured");
        }
        else
        {
            _detailHealth.text = "✓  DISPONIBLE";
            _detailHealth.RemoveFromClassList("detail-health-injured");
            _detailHealth.AddToClassList("detail-health-ok");
        }

        // Atributos
        BuildAttrBars(p);

        // Stats temporada (por ahora vacías)
        _statPts.text = "0.0";
        _statReb.text = "0.0";
        _statAst.text = "0.0";

        // Contrato y potencial
        _detailContract.text = $"${p.salary / 1_000_000}M/año · {p.contract_years} año{(p.contract_years != 1 ? "s" : "")}";
        _detailPotential.text = p.potential.ToString();
    }

    void BuildAttrBars(PlayerData p)
    {
        _detailAttrs.Clear();

        var attrs = new[]
        {
            ("TIRO",      p.shooting),
            ("TRIPLE",    p.three_point),
            ("PASE",      p.passing),
            ("BOTE",      p.dribbling),
            ("DEFENSA",   p.defense),
            ("REBOTE",    p.rebounding),
            ("VELOCIDAD", p.speed),
            ("ATLETISMO", p.athleticism),
            ("IQ",        p.iq),
            ("ROBOS",     p.steals),
            ("TAPONES",   p.blocks),
        };

        foreach (var (label, val) in attrs)
        {
            var row = new VisualElement();
            row.AddToClassList("attr-row");

            var lbl = new Label();
            lbl.AddToClassList("attr-label");
            lbl.text = label;

            var barBg = new VisualElement();
            barBg.AddToClassList("attr-bar-bg");

            var barFill = new VisualElement();
            barFill.AddToClassList("attr-bar-fill");
            if (val < 50) barFill.AddToClassList("attr-bar-fill--low");
            else if (val < 70) barFill.AddToClassList("attr-bar-fill--mid");

            barFill.style.width = new StyleLength(new Length(val, LengthUnit.Percent));
            barBg.Add(barFill);

            var valLbl = new Label();
            valLbl.AddToClassList("attr-val");
            valLbl.text = val.ToString();

            row.Add(lbl);
            row.Add(barBg);
            row.Add(valLbl);
            _detailAttrs.Add(row);
        }
    }

    // ── MODAL DESPIDO ─────────────────────────────────────

    void OpenDismissModal()
    {
        if (_selectedPlayer == null) return;

        long penalty = (long)(_selectedPlayer.salary * _selectedPlayer.contract_years * 0.5f);

        _dismissText1.text = $"Estás a punto de despedir a {_selectedPlayer.first_name} {_selectedPlayer.last_name}.";
        _dismissText2.text = $"Penalización por despido: ${penalty / 1_000_000}M (50% salario × años restantes).";

        _dismissOverlay.style.display = DisplayStyle.Flex;
        _dismissBox.style.display = DisplayStyle.Flex;
    }

    void CloseDismissModal()
    {
        _dismissOverlay.style.display = DisplayStyle.None;
        _dismissBox.style.display = DisplayStyle.None;
    }

    void ConfirmDismiss()
    {
        if (_selectedPlayer == null) return;

        // Mover jugador a agentes libres (team_id = 0)
        _selectedPlayer.team_id = 0;
        DatabaseManager.Instance.UpdatePlayer(_selectedPlayer);

        Debug.Log($"[Roster] {_selectedPlayer.first_name} {_selectedPlayer.last_name} despedido.");

        CloseDismissModal();

        // Recargar datos y refrescar
        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        _selectedPlayer = null;
        Refresh();
    }
}