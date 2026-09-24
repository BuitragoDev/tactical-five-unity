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
    }

    protected override void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        _root.Q<Button>("SubmenuHistorial")?.AddToClassList("nav-submenu-item--active");
        if (_btnAction != null) _btnAction.text = "MENÚ PRINCIPAL";

        if (_myTeam != null)
        {
            if (_headerTeamName != null)
                _headerTeamName.text = _myTeam.name.ToUpper();
            if (_headerManagerName != null)
                _headerManagerName.text = $"Manager: {_manager?.name ?? ""}";
            if (_headerTeamLogo != null)
            {
                var logos = Resources.LoadAll<Sprite>("Teams/Logos/64x64/");
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
            if (headerSeason != null) headerSeason.text = $"Temporada {_season.year_start}-{_season.year_end}";
            var headerDate = _root.Q<Label>("HeaderDate");
            if (headerDate != null) headerDate.text = DatabaseManager.Instance.GetCurrentDateString(_manager.id);
        }

        _sortedTrades = _trades
            .OrderByDescending(t => t.game_date)   // "yyyy-MM-dd" -> orden lexicográfico = cronológico
            .ThenByDescending(t => t.id)           // a igual fecha, el más reciente insertado arriba
            .ToList();
        RenderPage();
    }

    void RenderPage()
    {
        _historialBody.Clear();

        if (_sortedTrades == null || _sortedTrades.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("historial-empty");
            var lbl = new Label("No hay traspasos esta temporada.");
            lbl.AddToClassList("historial-empty-text");
            empty.Add(lbl);
            _historialBody.Add(empty);
        }
        else
        {
            foreach (var trade in _sortedTrades)
                _historialBody.Add(BuildTradeRow(trade));
        }

        if (_historialBar != null) { _historialBar.ScrollY = 0f; ApplyScroll(_historialBar); }
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
            var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
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
            var logos = Resources.LoadAll<Sprite>("Teams/Logos/32x32/");
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
        buyerName.text = toTeam != null ? toTeam.name : $"ID {trade.team_id_to}";
        row.Add(buyerName);

        var posLabel = new Label();
        posLabel.AddToClassList("historial-col");
        posLabel.AddToClassList("historial-col-center");
        posLabel.AddToClassList("historial-col-bold");
        posLabel.text = PositionCodes.GetShort(player?.position);
        row.Add(posLabel);

        var ovrLabel = new Label();
        ovrLabel.AddToClassList("historial-col");
        ovrLabel.AddToClassList("historial-col-center");
        ovrLabel.AddToClassList("historial-col-bold");
        ovrLabel.AddToClassList("historial-col-ovr");
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
    class ScrollBarData
    {
        public VisualElement Viewport;
        public VisualElement Content;
        public VisualElement Track;
        public VisualElement Thumb;
        public float ScrollY;
        public bool Dragging;
        public float StartPointerY;
        public float StartScrollY;
    }

    ScrollBarData BindScrollBar(VisualElement viewport, VisualElement content,
                                VisualElement track, VisualElement thumb)
    {
        if (viewport == null || content == null || track == null || thumb == null) return null;

        viewport.style.overflow = Overflow.Hidden;
        viewport.style.flexDirection = FlexDirection.Column;

        var bar = new ScrollBarData
        {
            Viewport = viewport,
            Content = content,
            Track = track,
            Thumb = thumb
        };

        viewport.RegisterCallback<WheelEvent>(evt =>
        {
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            bar.ScrollY = Mathf.Clamp(bar.ScrollY + evt.delta.y * 30f, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerDownEvent>(evt =>
        {
            bar.Dragging = true;
            bar.StartPointerY = evt.position.y;
            bar.StartScrollY = bar.ScrollY;
            thumb.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (!bar.Dragging) return;
            float max = GetScrollMax(bar);
            float travel = track.layout.height - thumb.layout.height;
            if (max <= 0f || travel <= 0f) return;
            float delta = evt.position.y - bar.StartPointerY;
            bar.ScrollY = Mathf.Clamp(bar.StartScrollY + delta / travel * max, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (bar.Dragging)
            {
                bar.Dragging = false;
                thumb.ReleasePointer(evt.pointerId);
            }
            evt.StopPropagation();
        });

        thumb.RegisterCallback<PointerCaptureOutEvent>(_ => bar.Dragging = false);

        track.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (bar.Dragging) return;
            float max = GetScrollMax(bar);
            if (max <= 0f) return;
            float page = viewport.layout.height;
            float y = evt.localPosition.y;
            float thumbTop = thumb.layout.y;
            float thumbBottom = thumbTop + thumb.layout.height;
            float target = y < thumbTop ? bar.ScrollY - page
                         : (y > thumbBottom ? bar.ScrollY + page : bar.ScrollY);
            bar.ScrollY = Mathf.Clamp(target, 0f, max);
            ApplyScroll(bar);
            evt.StopPropagation();
        });

        CursorManager.Instance?.RegisterHandCursor(thumb);
        CursorManager.Instance?.RegisterHandCursor(track);

        content.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));
        viewport.RegisterCallback<GeometryChangedEvent>(_ => ClampAndApply(bar));

        ApplyScroll(bar);

        return bar;
    }

    static float GetScrollMax(ScrollBarData bar)
    {
        if (bar == null || bar.Content == null || bar.Viewport == null) return 0f;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        return Mathf.Max(0f, contentHeight - viewportHeight);
    }

    void ClampAndApply(ScrollBarData bar)
    {
        if (bar == null) return;
        bar.ScrollY = Mathf.Clamp(bar.ScrollY, 0f, GetScrollMax(bar));
        ApplyScroll(bar);
    }

    void ApplyScroll(ScrollBarData bar)
    {
        if (bar == null || bar.Content == null) return;

        bar.Content.style.top = -bar.ScrollY;

        float trackHeight = bar.Track.layout.height;
        float contentHeight = bar.Content.layout.height;
        float viewportHeight = bar.Viewport.layout.height;
        if (trackHeight <= 0f || contentHeight <= 0f) return;

        float max = Mathf.Max(0f, contentHeight - viewportHeight);
        float ratio = max > 0f ? bar.ScrollY / max : 0f;
        float thumbMax = trackHeight - bar.Thumb.layout.height;
        bar.Thumb.style.top = ratio * thumbMax;
    }
}
