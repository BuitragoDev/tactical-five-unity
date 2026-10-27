using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class HistorialController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Historial;
    private VisualElement _historialBody;
    private VisualElement _headerTeamLogo;
    private Label _headerTeamName;
    private Label _headerManagerName;
    private Label _headerBudget;
    private Label _headerPayroll;
    private Label _headerMargin;
    private Label _headerChemistry;
    private List<TradeData> _trades = new();

    // Filtros y orden de la lista (usabilidad)
    private string _typeFilter = "all";      // all | trade | fa | snt
    private bool _orderDesc = true;          // más recientes primero
    private Button _filterAllBtn, _filterTradeBtn, _filterFaBtn, _filterSnTBtn, _orderBtn;
    private List<TradeData> _sortedTrades = new();
    private Dictionary<int, TeamData> _teamCache = new();
    private Dictionary<int, PlayerData> _playerCache = new();
    private Dictionary<int, DraftPickData> _pickCache = new();

    // ── Scrollbar ──
    private VisualElement _historialViewport, _historialScrollTrack, _historialScrollThumb;
    private ScrollBarData _historialBar;

    protected override void CacheReferences()
    {
        _historialBody = _root.Q<VisualElement>("HistorialBody");
        _historialViewport = _root.Q<VisualElement>("HistorialViewport");
        _historialScrollTrack = _root.Q<VisualElement>("HistorialScrollTrack");
        _historialScrollThumb = _root.Q<VisualElement>("HistorialScrollThumb");
        _filterAllBtn = _root.Q<Button>("HistFilterAll");
        _filterTradeBtn = _root.Q<Button>("HistFilterTrade");
        _filterFaBtn = _root.Q<Button>("HistFilterFA");
        _filterSnTBtn = _root.Q<Button>("HistFilterSnT");
        _orderBtn = _root.Q<Button>("HistOrderBtn");
        if (_historialViewport != null) _historialViewport.style.overflow = Overflow.Hidden;

        _headerTeamLogo = _root.Q<VisualElement>("HeaderTeamLogo");
        _headerTeamName = _root.Q<Label>("HeaderTeamName");
        _headerManagerName = _root.Q<Label>("HeaderManagerName");
        _headerBudget = _root.Q<Label>("HeaderBudget");
        _headerPayroll = _root.Q<Label>("HeaderPayroll");
        _headerMargin = _root.Q<Label>("HeaderMargin");
        _headerChemistry = _root.Q<Label>("HeaderChemistry");

        var headerSeason = _root.Q<Label>("HeaderSeason");
        if (headerSeason != null) headerSeason.text = "";
        var headerDate = _root.Q<Label>("HeaderDate");
        if (headerDate != null) headerDate.text = "";
    }

    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("HistorialScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/traspasos_icon");
            if (iconTex != null) screenIcon.style.backgroundImage = new StyleBackground(iconTex);
        }

        if (_season == null) return;

        _trades = DatabaseManager.Instance.GetTradesBySeason(_season.id);

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        foreach (var t in allTeams)
            _teamCache[t.id] = t;

        foreach (var tr in _trades)
        {
            if (!_playerCache.ContainsKey(tr.player_id))
            {
                var p = DatabaseManager.Instance.GetPlayerById(tr.player_id);
                if (p != null)
                    _playerCache[tr.player_id] = p;
            }

            if (tr.pick_id > 0 && !_pickCache.ContainsKey(tr.pick_id))
            {
                var pk = DatabaseManager.Instance.GetDraftPickById(tr.pick_id);
                if (pk != null)
                    _pickCache[tr.pick_id] = pk;
            }
        }
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        _filterAllBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTypeFilter("all"); });
        _filterTradeBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTypeFilter("trade"); });
        _filterFaBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTypeFilter("fa"); });
        _filterSnTBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); SetTypeFilter("snt"); });
        _orderBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _orderDesc = !_orderDesc; ApplyFilterAndOrder(); });
    }

    void SetTypeFilter(string filter)
    {
        _typeFilter = filter;
        ApplyFilterAndOrder();
    }

    /// <summary>Aplica el filtro de tipo y el orden elegidos y repinta (sin recargar de BD).</summary>
    void ApplyFilterAndOrder()
    {
        if (_filterAllBtn != null)
        {
            _filterAllBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "all");
            _filterTradeBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "trade");
            _filterFaBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "fa");
            _filterSnTBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "snt");
        }
        if (_orderBtn != null)
            _orderBtn.text = _orderDesc ? "MÁS RECIENTES ↓" : "MÁS ANTIGUOS ↑";

        _sortedTrades = FilteredTrades().ToList();
        RenderPage();
    }

    /// <summary>Movimientos de la temporada según el filtro de tipo (puro sobre la lista cargada).</summary>
    IEnumerable<TradeData> FilteredTrades()
    {
        IEnumerable<TradeData> filtered = _trades ?? new List<TradeData>();
        filtered = _typeFilter switch
        {
            "trade" => filtered.Where(t => t.trade_type == "trade" || t.trade_type == "pick_trade"),
            "fa"    => filtered.Where(t => t.trade_type == "free_agent"),
            "snt"   => filtered.Where(t => t.trade_type == "sign_and_trade"),
            _       => filtered
        };

        return _orderDesc
            ? filtered.OrderByDescending(t => t.game_date).ThenByDescending(t => t.id)
            : filtered.OrderBy(t => t.game_date).ThenBy(t => t.id);
    }

    protected override void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        _root.Q<Button>("SubmenuHistorial")?.AddToClassList("nav-submenu-item--active");
        if (_btnAction != null) _btnAction.text = LocalizationService.T("ui.code.historial_controller.menu_principal");

        if (_myTeam != null)
        {
            if (_headerTeamName != null)
                _headerTeamName.text = _myTeam.name.ToUpper();
            if (_headerManagerName != null)
                _headerManagerName.text = LocalizationService.T("ui.common.manager", _manager?.name ?? "");
            if (_headerTeamLogo != null)
            {
                var logos = TeamImages.Logos(64);
                foreach (var s in logos)
                {
                    if (s.name == _myTeam.logo)
                    {
                        _headerTeamLogo.style.backgroundImage = new StyleBackground(s);
                        break;
                    }
                }
            }

            if (_headerBudget != null)
            {
                var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                long totalPayroll = players.Sum(p => p.salary);

                _headerBudget.text = $"${_myTeam.budget / 1_000_000}M";
                _headerBudget.style.color = _myTeam.budget < 0
                    ? new StyleColor(new Color32(192, 57, 43, 255))
                    : new StyleColor(new Color32(39, 174, 96, 255));
                _headerPayroll.text = $"${totalPayroll / 1_000_000}M";

                var leagueSettings = DatabaseManager.Instance.GetLeagueSettings();
                long salaryCap = leagueSettings?.salary_cap ?? TradeHelper.SALARY_CAP;
                long margin = salaryCap - totalPayroll;
                string marginText = margin >= 0 ? $"+${margin / 1_000_000}M" : $"-${Mathf.Abs((int)(margin / 1_000_000))}M";
                _headerMargin.text = marginText;

                int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
                _headerChemistry.text = $"{chemistry.ToString()}%";
                _headerChemistry.RemoveFromClassList("header-stat-value--gold");
                _headerChemistry.RemoveFromClassList("header-stat-value--negative");
                if (chemistry < 40)
                    _headerChemistry.AddToClassList("header-stat-value--negative");
                else if (chemistry < 70)
                    _headerChemistry.AddToClassList("header-stat-value--gold");

                _headerMargin.RemoveFromClassList("header-stat-value--negative");
                if (margin < 0) _headerMargin.AddToClassList("header-stat-value--negative");
            }
        }

        if (_season != null)
        {
            var headerSeason = _root.Q<Label>("HeaderSeason");
            if (headerSeason != null) headerSeason.text = LocalizationService.T("ui.common.season", _season.year_start, _season.year_end);
            var headerDate = _root.Q<Label>("HeaderDate");
            if (headerDate != null) headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        // Filtro de tipo + orden elegidos (por defecto: todo, más recientes primero).
        if (_filterAllBtn != null)
        {
            _filterAllBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "all");
            _filterTradeBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "trade");
            _filterFaBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "fa");
            _filterSnTBtn.EnableInClassList("historial-filter-btn--active", _typeFilter == "snt");
        }
        if (_orderBtn != null)
            _orderBtn.text = _orderDesc ? "MÁS RECIENTES ↓" : "MÁS ANTIGUOS ↑";

        _sortedTrades = FilteredTrades().ToList();   // "yyyy-MM-dd" -> orden lexicográfico = cronológico
        RenderPage();
    }

    void RenderPage()
    {
        _historialBody.Clear();

        if (_sortedTrades == null || _sortedTrades.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("historial-empty");
            var lbl = new Label(_typeFilter == "all"
                ? "No hay movimientos esta temporada."
                : "No hay movimientos de este tipo esta temporada.");
            lbl.AddToClassList("historial-empty-text");
            empty.Add(lbl);
            _historialBody.Add(empty);
        }
        else
        {
            foreach (var trade in _sortedTrades)
                _historialBody.Add(BuildTradeRow(trade));
        }

        CustomScrollbar.Reset(_historialBar);
        _historialBar = BindScrollBar(_historialViewport, _historialBody, _historialScrollTrack, _historialScrollThumb);
    }

    VisualElement BuildTradeRow(TradeData trade)
    {
        var row = new VisualElement();
        row.AddToClassList("historial-row");

        _playerCache.TryGetValue(trade.player_id, out var player);

        var dateLabel = new Label();
        dateLabel.AddToClassList("historial-col");
        dateLabel.AddToClassList("historial-col-first");
        try
        {
            dateLabel.text = System.DateTime.Parse(trade.game_date).ToString("dd/MM/yyyy");
        }
        catch
        {
            dateLabel.text = trade.game_date ?? "";
        }
        row.Add(dateLabel);

        var itemLabel = new Label();
        itemLabel.AddToClassList("historial-col");
        itemLabel.AddToClassList("historial-col-player");
        itemLabel.AddToClassList("historial-col-wide");
        if (trade.trade_type == "pick_trade" && trade.pick_id > 0
            && _pickCache.TryGetValue(trade.pick_id, out var pick)
            && _teamCache.TryGetValue(pick.original_team_id, out var origTeam))
        {
            itemLabel.text = pick.draft_year > 0
                ? $"Ronda {pick.round} · {pick.draft_year} · {origTeam.abbreviation}"
                : $"Ronda {pick.round} · {origTeam.abbreviation}";
            itemLabel.AddToClassList("historial-pick-label");
        }
        else if (player != null)
        {
            itemLabel.text = $"{player.first_name} {player.last_name}";
        }
        else
        {
            itemLabel.text = $"ID {trade.player_id}";
        }
        row.Add(itemLabel);

        var sellerLogo = new VisualElement();
        sellerLogo.AddToClassList("historial-col");
        sellerLogo.AddToClassList("historial-col-logo");
        TeamData fromTeam = null;
        if (trade.trade_type != "free_agent")
            _teamCache.TryGetValue(trade.team_id_from, out fromTeam);
        if (fromTeam != null)
        {
            var logos = TeamImages.Logos(32);
            foreach (var s in logos)
            {
                if (s.name == fromTeam.logo)
                {
                    sellerLogo.style.backgroundImage = new StyleBackground(s);
                    break;
                }
            }
        }
        row.Add(sellerLogo);

        var sellerName = new Label();
        sellerName.AddToClassList("historial-col");
        sellerName.AddToClassList("historial-col-wide");
        if (trade.trade_type != "free_agent" && fromTeam != null)
            sellerName.text = fromTeam.name;
        else
            sellerName.text = "";
        row.Add(sellerName);

        var buyerLogo = new VisualElement();
        buyerLogo.AddToClassList("historial-col");
        buyerLogo.AddToClassList("historial-col-logo");
        if (_teamCache.TryGetValue(trade.team_id_to, out var toTeam))
        {
            var logos = TeamImages.Logos(32);
            foreach (var s in logos)
            {
                if (s.name == toTeam.logo)
                {
                    buyerLogo.style.backgroundImage = new StyleBackground(s);
                    break;
                }
            }
        }
        row.Add(buyerLogo);

        var buyerName = new Label();
        buyerName.AddToClassList("historial-col");
        buyerName.AddToClassList("historial-col-wide");
        buyerName.text = toTeam != null ? toTeam.name : $"ID {trade.team_id_to}";
        row.Add(buyerName);

        var posLabel = new Label();
        posLabel.AddToClassList("historial-col");
        posLabel.AddToClassList("historial-col-center");
        posLabel.AddToClassList("historial-col-bold");
        posLabel.AddToClassList("historial-col-narrow");
        posLabel.text = PositionCodes.GetShort(player?.position);
        row.Add(posLabel);

        var ovrLabel = new Label();
        ovrLabel.AddToClassList("historial-col");
        ovrLabel.AddToClassList("historial-col-center");
        ovrLabel.AddToClassList("historial-col-bold");
        ovrLabel.AddToClassList("historial-col-ovr");
        ovrLabel.AddToClassList("historial-col-narrow");
        ovrLabel.text = player?.overall.ToString() ?? "";
        row.Add(ovrLabel);

        var salLabel = new Label();
        salLabel.AddToClassList("historial-col");
        salLabel.AddToClassList("historial-col-center");
        salLabel.AddToClassList("historial-col-bold");
        salLabel.AddToClassList("historial-col-sal");
        salLabel.text = player != null ? SalaryStr(player.salary) : "";
        row.Add(salLabel);

        return row;
    }

    static string SalaryStr(long salary)
    {
        return salary.ToString("N0").Replace(',', '.') + " $";
    }

    // ── ScrollBar ──
    class ScrollBarData : CustomScrollbar.Handle { }

    ScrollBarData BindScrollBar(VisualElement viewport, VisualElement content,
                                VisualElement track, VisualElement thumb)
        => CustomScrollbar.Bind<ScrollBarData>(viewport, content, track, thumb);






}
