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

    const int PAGE_SIZE = 15;
    private int _currentPage;
    private Button _prevBtn, _nextBtn;
    private Label _pageInfo;

    protected override void CacheReferences()
    {
        _historialBody = _root.Q<VisualElement>("HistorialBody");
        _prevBtn = _root.Q<Button>("HistorialPrev");
        _nextBtn = _root.Q<Button>("HistorialNext");
        _pageInfo = _root.Q<Label>("HistorialPageInfo");

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
        _prevBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _currentPage--; RenderPage(); });
        _nextBtn?.RegisterCallback<ClickEvent>(_ => { PlayClick(); _currentPage++; RenderPage(); });
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

        _sortedTrades = _trades.OrderByDescending(t => t.game_day).ToList();
        _currentPage = 0;
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

            if (_pageInfo != null) _pageInfo.text = "0 / 0";
            if (_prevBtn != null) _prevBtn.SetEnabled(false);
            if (_nextBtn != null) _nextBtn.SetEnabled(false);
            return;
        }

        int totalPages = Mathf.Max(1, Mathf.CeilToInt((float)_sortedTrades.Count / PAGE_SIZE));
        _currentPage = Mathf.Clamp(_currentPage, 0, totalPages - 1);

        int start = _currentPage * PAGE_SIZE;
        int end = Mathf.Min(start + PAGE_SIZE, _sortedTrades.Count);
        for (int i = start; i < end; i++)
        {
            _historialBody.Add(BuildTradeRow(_sortedTrades[i]));
        }

        if (_pageInfo != null) _pageInfo.text = $"{_currentPage + 1} / {totalPages}";
        if (_prevBtn != null) _prevBtn.SetEnabled(_currentPage > 0);
        if (_nextBtn != null) _nextBtn.SetEnabled(_currentPage < totalPages - 1);
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
            itemLabel.text = $"R{pick.round} {origTeam.abbreviation}";
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

        return row;
    }
}
