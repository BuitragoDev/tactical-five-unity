using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class MessagesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Messages;

    private const int ITEMS_PER_PAGE = 4;

    private VisualElement _messagesBody;
    private VisualElement _paginator;
    private TextField _searchField;
    private Button _btnPrev;
    private Button _btnNext;
    private Label _pageLabel;

    private VisualElement _newsDetailPanel;
    private VisualElement _newsDetailContent;
    private VisualElement _newsDetailPhoto;
    private Label _newsDetailDate;
    private Label _newsDetailTitle;
    private Label _newsDetailBody;
    private Label _newsDetailEmpty;
    private VisualElement _newsDetailFooter;
    private Button _btnDeleteNews;

    private List<MessageData> _allMessages;
    private List<MessageData> _filteredMessages;
    private string _activeCategory = "TODAS";
    private string _searchQuery = "";
    private int _currentPage = 0;
    private int _selectedMessageId = 0;

    private readonly Dictionary<string, Sprite> _logoSprites = new();
    private readonly Dictionary<string, Texture2D> _noticiasSprites = new();
    private readonly Dictionary<string, PlayerData> _playerByName = new();
    private List<TeamData> _allTeams;

    private readonly Button[] _filterTabs = new Button[6];

    protected override void CacheReferences()
    {
        _messagesBody = _root.Q<VisualElement>("MessagesBody");
        _paginator = _root.Q<VisualElement>("Paginator");
        _searchField = _root.Q<TextField>("SearchField");

        _filterTabs[0] = _root.Q<Button>("FilterTodas");
        _filterTabs[1] = _root.Q<Button>("FilterClub");
        _filterTabs[2] = _root.Q<Button>("FilterLiga");
        _filterTabs[3] = _root.Q<Button>("FilterTraspasos");
        _filterTabs[4] = _root.Q<Button>("FilterLesiones");
        _filterTabs[5] = _root.Q<Button>("FilterPrensa");

        _newsDetailPanel = _root.Q<VisualElement>("NewsDetailPanel");
        _newsDetailContent = _root.Q<VisualElement>("NewsDetailContent");
        _newsDetailPhoto = _root.Q<VisualElement>("NewsDetailPhoto");
        _newsDetailTitle = _root.Q<Label>("NewsDetailTitle");
        _newsDetailBody = _root.Q<Label>("NewsDetailBody");
        _newsDetailDate = _root.Q<Label>("NewsDetailDate");
        _newsDetailEmpty = _root.Q<Label>("NewsDetailEmpty");
        _newsDetailFooter = _root.Q<VisualElement>("NewsDetailFooter");
        _btnDeleteNews = _root.Q<Button>("BtnDeleteNews");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var screenIcon = _root.Q<VisualElement>("MessagesScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/noticias_icon");
            if (iconTex != null) screenIcon.style.backgroundImage = new StyleBackground(iconTex);
        }

        var logos = Resources.LoadAll<Sprite>("Teams/Logos/100x100");
        foreach (var s in logos) _logoSprites[s.name] = s;

        if (_noticiasSprites.Count == 0)
        {
            var nombres = new[] {
                "contratado", "despedido", "rechazado", "entrenamiento", "lesionados",
                "obras-finalizadas", "pago-nominas", "prestamo-bancario", "renovacion",
                "traspaso", "bienvenida", "recuperado", "quejas", "premios",
                "eliminacion", "remodelacion"
            };
            foreach (var n in nombres)
            {
                var tex = Resources.Load<Texture2D>("Noticias/" + n);
                if (tex != null) _noticiasSprites[n] = tex;
            }
        }

        _allTeams = DatabaseManager.Instance.GetAllTeams();

        _playerByName.Clear();
        foreach (var p in DatabaseManager.Instance.GetAllPlayers())
            _playerByName[$"{p.first_name} {p.last_name}"] = p;

        _allMessages = DatabaseManager.Instance.GetMessages(_manager.id);
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        _searchField?.RegisterValueChangedCallback(evt =>
        {
            _searchQuery = evt.newValue ?? "";
            _currentPage = 0;
            ApplyFilters();
        });

        var filterNames = new[] { "TODAS", "CLUB", "LIGA", "TRASPASOS", "LESIONES", "PRENSA" };
        for (int i = 0; i < _filterTabs.Length; i++)
        {
            if (_filterTabs[i] == null) continue;
            string cat = filterNames[i];
            _filterTabs[i].RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                SetActiveFilter(cat);
            });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(_filterTabs[i]);
        }

        if (_btnDeleteNews != null)
        {
            _btnDeleteNews.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                DeleteSelectedMessage();
            });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(_btnDeleteNews);
        }
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (Exception ex) { Debug.LogWarning($"[Messages] RefreshHeader error: {ex.Message}"); }
        ApplyFilters();
    }

    void SetActiveFilter(string category)
    {
        _activeCategory = category;
        _currentPage = 0;

        var filterNames = new[] { "TODAS", "CLUB", "LIGA", "TRASPASOS", "LESIONES", "PRENSA" };
        for (int i = 0; i < _filterTabs.Length; i++)
        {
            if (_filterTabs[i] == null) continue;
            _filterTabs[i].RemoveFromClassList("messages-filter-tab--active");
            if (filterNames[i] == category)
                _filterTabs[i].AddToClassList("messages-filter-tab--active");
        }

        ApplyFilters();
    }

    void ApplyFilters()
    {
        if (_allMessages == null)
        {
            _filteredMessages = new List<MessageData>();
            BuildNewsCards();
            return;
        }

        IEnumerable<MessageData> query = _allMessages;

        if (_activeCategory != "TODAS")
        {
            query = query.Where(m => GetCategory(m) == _activeCategory);
        }

        if (!string.IsNullOrEmpty(_searchQuery))
        {
            string q = _searchQuery.ToLowerInvariant();
            query = query.Where(m =>
                (m.title != null && m.title.ToLowerInvariant().Contains(q)) ||
                (m.body != null && m.body.ToLowerInvariant().Contains(q))
            );
        }

        _filteredMessages = query.OrderByDescending(m => m.id).ToList();
        BuildNewsCards();
    }

    void BuildNewsCards()
    {
        _messagesBody.Clear();

        if (_filteredMessages == null || _filteredMessages.Count == 0)
        {
            var empty = new VisualElement();
            empty.AddToClassList("messages-empty");
            var icon = new Label("\U0001F4ED");
            icon.AddToClassList("messages-empty-icon");
            empty.Add(icon);
            var lbl = new Label("No hay noticias disponibles.");
            lbl.AddToClassList("messages-empty-text");
            empty.Add(lbl);
            _messagesBody.Add(empty);
            _paginator.style.display = DisplayStyle.None;
            ShowNewsDetail(null);
            return;
        }

        // Keep the current selection if it is still present; otherwise select the first one
        if (_filteredMessages.All(m => m.id != _selectedMessageId))
            _selectedMessageId = _filteredMessages[0].id;

        int totalPages = Mathf.CeilToInt((float)_filteredMessages.Count / ITEMS_PER_PAGE);
        if (_currentPage >= totalPages) _currentPage = totalPages - 1;
        if (_currentPage < 0) _currentPage = 0;

        int start = _currentPage * ITEMS_PER_PAGE;
        int end = Mathf.Min(start + ITEMS_PER_PAGE, _filteredMessages.Count);
        var pageMessages = _filteredMessages.GetRange(start, end - start);

        foreach (var msg in pageMessages)
        {
            var card = CreateNewsCard(msg);
            _messagesBody.Add(card);

            if (msg.is_read == 0)
                DatabaseManager.Instance.MarkMessageRead(msg.id);
        }

        BuildPaginator(totalPages);

        var selected = _filteredMessages.FirstOrDefault(m => m.id == _selectedMessageId);
        ShowNewsDetail(selected);
    }

    VisualElement CreateNewsCard(MessageData msg)
    {
        var card = new VisualElement();
        card.userData = msg.id;
        card.AddToClassList("news-card");
        if (msg.is_read == 0)
            card.AddToClassList("news-card--unread");
        if (msg.id == _selectedMessageId)
            card.AddToClassList("news-card--selected");

        var photo = new VisualElement();
        photo.AddToClassList("news-card-photo");
        SetNewsImage(photo, msg);
        card.Add(photo);

        var textBlock = new VisualElement();
        textBlock.AddToClassList("news-card-text");

        var badgeRow = new VisualElement();
        badgeRow.AddToClassList("news-card-badge-row");

        var dateBadge = new Label(GetNewsDate(msg));
        dateBadge.AddToClassList("news-card-date");
        badgeRow.Add(dateBadge);

        string category = GetCategory(msg);
        var badge = new Label(GetCategoryLabel(category));
        badge.AddToClassList("news-card-badge");
        badge.AddToClassList(GetCategoryBadgeClass(category));
        badgeRow.Add(badge);
        textBlock.Add(badgeRow);

        var title = new Label(msg.title ?? "Sin asunto");
        title.AddToClassList("news-card-title");
        textBlock.Add(title);

        var body = new Label(msg.body ?? "");
        body.AddToClassList("news-card-body");
        textBlock.Add(body);

        card.Add(textBlock);

        var arrow = new Label("\u203A");
        arrow.AddToClassList("news-card-arrow");
        card.Add(arrow);

        var msgRef = msg;
        card.RegisterCallback<ClickEvent>(_ => { PlayClick(); SelectNews(msgRef); });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(card);

        return card;
    }

    void BuildPaginator(int totalPages)
    {
        _paginator.Clear();

        if (totalPages <= 1)
        {
            _paginator.style.display = DisplayStyle.None;
            return;
        }

        _paginator.style.display = DisplayStyle.Flex;

        var btnPrev = new Button();
        btnPrev.AddToClassList("messages-paginator-btn");
        btnPrev.text = "\u25C0";
        btnPrev.SetEnabled(_currentPage > 0);
        if (_currentPage <= 0) btnPrev.AddToClassList("messages-paginator-btn--disabled");
        int prevPage = _currentPage - 1;
        btnPrev.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(prevPage); });
        if (CursorManager.Instance != null)
        {
            btnPrev.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            btnPrev.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        _paginator.Add(btnPrev);

        var pageLbl = new Label();
        pageLbl.AddToClassList("messages-paginator-label");
        pageLbl.text = $"{_currentPage + 1} / {totalPages}";
        _paginator.Add(pageLbl);

        var btnNext = new Button();
        btnNext.AddToClassList("messages-paginator-btn");
        btnNext.text = "\u25B6";
        btnNext.SetEnabled(_currentPage < totalPages - 1);
        if (_currentPage >= totalPages - 1) btnNext.AddToClassList("messages-paginator-btn--disabled");
        int nextPage = _currentPage + 1;
        btnNext.RegisterCallback<ClickEvent>(_ => { PlayClick(); GoToPage(nextPage); });
        if (CursorManager.Instance != null)
        {
            btnNext.RegisterCallback<MouseEnterEvent>(_ => CursorManager.Instance.SetHandCursor());
            btnNext.RegisterCallback<MouseLeaveEvent>(_ => CursorManager.Instance.SetDefaultCursor());
        }
        _paginator.Add(btnNext);
    }

    void GoToPage(int page)
    {
        _currentPage = page;
        BuildNewsCards();
    }

    // ── NEWS DETAIL ──

    void SelectNews(MessageData msg)
    {
        if (msg == null) return;

        _selectedMessageId = msg.id;

        UpdateSelectedHighlight();
        ShowNewsDetail(msg);
    }

    void UpdateSelectedHighlight()
    {
        if (_messagesBody == null) return;
        foreach (var child in _messagesBody.Children())
        {
            if (child.userData is int id)
            {
                child.EnableInClassList("news-card--selected", id == _selectedMessageId);
            }
        }
    }

    void ShowNewsDetail(MessageData msg)
    {
        if (_newsDetailPanel == null) return;

        if (msg == null)
        {
            if (_newsDetailContent != null) _newsDetailContent.style.display = DisplayStyle.None;
            if (_newsDetailFooter != null) _newsDetailFooter.style.display = DisplayStyle.None;
            if (_newsDetailEmpty != null) _newsDetailEmpty.style.display = DisplayStyle.Flex;
            return;
        }

        if (_newsDetailContent != null) _newsDetailContent.style.display = DisplayStyle.Flex;
        if (_newsDetailFooter != null) _newsDetailFooter.style.display = DisplayStyle.Flex;
        if (_newsDetailEmpty != null) _newsDetailEmpty.style.display = DisplayStyle.None;

        if (_newsDetailTitle != null) _newsDetailTitle.text = msg.title ?? "Sin asunto";
        if (_newsDetailBody != null) _newsDetailBody.text = msg.body ?? "";

        if (_newsDetailDate != null)
            _newsDetailDate.text = GetNewsDate(msg);

        if (_newsDetailPhoto != null)
        {
            _newsDetailPhoto.style.backgroundImage = null;
            SetNewsImage(_newsDetailPhoto, msg);
        }
    }

    // ── DELETE ──

    void DeleteSelectedMessage()
    {
        if (_selectedMessageId <= 0) return;

        DatabaseManager.Instance.DeleteMessage(_selectedMessageId);
        _selectedMessageId = 0;
        _allMessages = DatabaseManager.Instance.GetMessages(_manager.id);
        ApplyFilters();
        ShowNewsDetail(null);
    }

    // ── CATEGORY ──

    string GetCategory(MessageData msg)
    {
        if (!string.IsNullOrEmpty(msg.category))
            return msg.category;

        string t = msg.title ?? "";
        string b = msg.body ?? "";

        if (msg.sender_type == 2)
        {
            if (t.Contains("Hito", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Premios del Mes", StringComparison.OrdinalIgnoreCase)
                || t.Contains("All-Star", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Playoffs", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Play-In", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Draft", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Racha", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Campanada", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Triple-Doble", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Explosión", StringComparison.OrdinalIgnoreCase)
                || t.Contains("Resultado", StringComparison.OrdinalIgnoreCase))
                return "LIGA";
            return "CLUB";
        }

        if (t.Contains("Lesión", StringComparison.OrdinalIgnoreCase)
            || t.Contains("RESERVA", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Recuperado", StringComparison.OrdinalIgnoreCase)
            || t.Contains("TRATAR", StringComparison.OrdinalIgnoreCase))
            return "LESIONES";

        if (t.Contains("Traspaso", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Sign & Trade", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Intercambio", StringComparison.OrdinalIgnoreCase)
            || t.Contains("OFERTAS", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Rescisión", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Hard cap", StringComparison.OrdinalIgnoreCase)
            || t.Contains("buyout", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Última semana", StringComparison.OrdinalIgnoreCase)
            || t.Contains("transferible", StringComparison.OrdinalIgnoreCase))
            return "TRASPASOS";

        if (t.Contains("Resultado:", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Racha", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Campanada", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Triple-Doble", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Explosión", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Premios del Mes", StringComparison.OrdinalIgnoreCase)
            || t.Contains("All-Star", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Playoffs", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Play-In", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Draft", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Jornada", StringComparison.OrdinalIgnoreCase))
            return "LIGA";

        if (t.Contains("Queja", StringComparison.OrdinalIgnoreCase)
            || t.Contains("Preocupación", StringComparison.OrdinalIgnoreCase))
            return "PRENSA";

        return "CLUB";
    }

    string GetCategoryLabel(string category)
    {
        return category switch
        {
            "CLUB" => "CLUB",
            "LIGA" => "LIGA",
            "TRASPASOS" => "MERCADO",
            "LESIONES" => "LESIONES",
            "PRENSA" => "PRENSA",
            _ => "CLUB"
        };
    }

    string GetNewsDate(MessageData msg)
    {
        string raw = msg.game_date ?? msg.date_sent;
        if (string.IsNullOrEmpty(raw)) return "";
        if (DateTime.TryParse(raw, out var date))
            return date.ToString("dd/MM/yyyy");
        return raw;
    }

    string GetCategoryBadgeClass(string category)
    {
        return category switch
        {
            "CLUB" => "news-card-badge--club",
            "LIGA" => "news-card-badge--liga",
            "TRASPASOS" => "news-card-badge--traspasos",
            "LESIONES" => "news-card-badge--lesiones",
            "PRENSA" => "news-card-badge--prensa",
            _ => "news-card-badge--club"
        };
    }

    // ── NEWS IMAGE ──

    void SetNewsImage(VisualElement icon, MessageData msg)
    {
        // Prioridad: news_type (nuevo) → lógica por título (retrocompatibilidad)
        if (!string.IsNullOrEmpty(msg.news_type))
        {
            if (SetNewsImageByType(icon, msg)) return;
        }

        SetNewsImageByTitle(icon, msg);
    }

    bool SetNewsImageByType(VisualElement icon, MessageData msg)
    {
        string nt = msg.news_type;

        if (nt == NewsType.HITO || nt == NewsType.RACHA_VICTORIAS || nt == NewsType.MALA_RACHA
            || nt == NewsType.CAMPANADA || nt == NewsType.RESULTADO || nt == NewsType.GLEAGUE_CHAMPION)
        {
            string teamName = ExtractFirstTeamFromBody(msg.body);
            if (teamName != null)
            {
                var team = _allTeams?.FirstOrDefault(t => t.name == teamName);
                if (team != null && _logoSprites.TryGetValue(team.logo, out var sprite))
                {
                    icon.style.backgroundImage = new StyleBackground(sprite);
                    return true;
                }
            }
            if (nt == NewsType.CAMPANADA)
            {
                var tex = _noticiasSprites.GetValueOrDefault("rechazado");
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
            }
            return false;
        }

        if (nt == NewsType.TRIPLE_DOBLE || nt == NewsType.EXPLOSION
            || nt == NewsType.FICHAJE || nt == NewsType.HARD_CAP)
        {
            string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
            if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
            {
                var tex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
            }
        }

        if (nt == NewsType.FICHAJE_RECHAZADO || nt == NewsType.FICHAJE_CANCELADO
            || nt == NewsType.OFERTA_RECHAZADA || nt == NewsType.REFIRMA_CANCELADA
            || nt == NewsType.DESPEDIDO || nt == NewsType.EMPLEADO_DESPEDIDO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("despedido");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.RENOVACION || nt == NewsType.REFIRMA_ENVIADA
            || nt == NewsType.AGENTE_LIBRE)
        {
            var tex = _noticiasSprites.GetValueOrDefault("renovacion");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.ENTRENAMIENTO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("entrenamiento");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.RECUPERACION)
        {
            var tex = _noticiasSprites.GetValueOrDefault("recuperado");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.LESION || nt == NewsType.RESERVA_IR)
        {
            var tex = _noticiasSprites.GetValueOrDefault("lesionados");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.INTERCAMBIO || nt == NewsType.TRASPASO || nt == NewsType.SIGN_AND_TRADE
            || nt == NewsType.DEADLINE_SEMANA || nt == NewsType.BUYOUT)
        {
            var tex = _noticiasSprites.GetValueOrDefault("traspaso");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.EMPLEADO_CONTRATADO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("contratado");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.NOMINA || nt == NewsType.ABONOS)
        {
            var tex = _noticiasSprites.GetValueOrDefault("pago-nominas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.PATROCINIO)
        {
            string sponsorName = ExtractFirstFromBody(msg.body,
                DatabaseManager.Instance.Db.Table<SponsorData>()
                    .Where(s => s.team_id == _myTeam.id && s.is_active == 1)
                    .Select(s => s.name).ToList());
            if (sponsorName != null)
            {
                var sponsor = DatabaseManager.Instance.Db.Table<SponsorData>()
                    .FirstOrDefault(s => s.team_id == _myTeam.id && s.is_active == 1
                                      && s.name == sponsorName);
                if (sponsor != null)
                {
                    var sponsorTex = Resources.Load<Texture2D>(sponsor.logo?.Replace(".png", ""));
                    if (sponsorTex != null) { icon.style.backgroundImage = new StyleBackground(sponsorTex); return true; }
                }
            }
            var texS = _noticiasSprites.GetValueOrDefault("pago-nominas");
            if (texS != null) { icon.style.backgroundImage = new StyleBackground(texS); return true; }
        }

        if (nt == NewsType.TV)
        {
            string channelName = ExtractFirstFromBody(msg.body,
                DatabaseManager.Instance.Db.Table<TvChannelData>()
                    .Where(c => c.team_id == _myTeam.id && c.is_active == 1)
                    .Select(c => c.name).ToList());
            if (channelName != null)
            {
                var channel = DatabaseManager.Instance.Db.Table<TvChannelData>()
                    .FirstOrDefault(c => c.team_id == _myTeam.id && c.is_active == 1
                                       && c.name == channelName);
                if (channel != null)
                {
                    var channelTex = Resources.Load<Texture2D>(channel.logo?.Replace(".png", ""));
                    if (channelTex != null) { icon.style.backgroundImage = new StyleBackground(channelTex); return true; }
                }
            }
            var texT = _noticiasSprites.GetValueOrDefault("pago-nominas");
            if (texT != null) { icon.style.backgroundImage = new StyleBackground(texT); return true; }
        }

        if (nt == NewsType.PRESTAMO_CONCEDIDO || nt == NewsType.PRESTAMO_SALDADO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("prestamo-bancario");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.REMODELACION_INICIADA || nt == NewsType.REMODELACION_COMPLETADA)
        {
            var tex = _noticiasSprites.GetValueOrDefault("obras-finalizadas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.BIENVENIDA)
        {
            var tex = _noticiasSprites.GetValueOrDefault("bienvenida");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.ELIMINACION_PLAYIN || nt == NewsType.ELIMINACION_PLAYOFFS)
        {
            var tex = _noticiasSprites.GetValueOrDefault("eliminacion");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.PREMIOS_MES)
        {
            var tex = _noticiasSprites.GetValueOrDefault("premios");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.QUEJA || nt == NewsType.PREOCUPACION)
        {
            var tex = _noticiasSprites.GetValueOrDefault("quejas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        return false;
    }

    void SetNewsImageByTitle(VisualElement icon, MessageData msg)
    {
        if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Resultado:"))
        {
            string rivalName = msg.title.Substring("Resultado: ".Length);
            int parenIdx = rivalName.IndexOf(" (");
            if (parenIdx > 0) rivalName = rivalName.Substring(0, parenIdx);
            var rival = _allTeams?.FirstOrDefault(t => t.name == rivalName);
            if (rival != null && _logoSprites.TryGetValue(rival.logo, out var sprite))
            {
                icon.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Patrocinador firmado:"))
        {
            string sponsorName = msg.title.Substring("Patrocinador firmado: ".Length).ToLower();
            var sponsor = DatabaseManager.Instance.Db.Table<SponsorData>()
                .FirstOrDefault(s => s.team_id == _myTeam.id && s.is_active == 1
                                  && s.name.ToLower() == sponsorName);
            if (sponsor != null)
            {
                var sponsorTex = Resources.Load<Texture2D>(sponsor.logo?.Replace(".png", ""));
                if (sponsorTex != null) { icon.style.backgroundImage = new StyleBackground(sponsorTex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Derechos televisivos firmados:"))
        {
            string channelName = msg.title.Substring("Derechos televisivos firmados: ".Length).ToLower();
            var channel = DatabaseManager.Instance.Db.Table<TvChannelData>()
                .FirstOrDefault(c => c.team_id == _myTeam.id && c.is_active == 1
                                   && c.name.ToLower() == channelName);
            if (channel != null)
            {
                var channelTex = Resources.Load<Texture2D>(channel.logo?.Replace(".png", ""));
                if (channelTex != null) { icon.style.backgroundImage = new StyleBackground(channelTex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Fichaje:"))
        {
            string playerName = msg.title.Substring("Fichaje: ".Length).Trim();
            if (_playerByName.TryGetValue(playerName, out var p))
            {
                var tex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Hard cap activado"))
        {
            string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
            if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
            {
                var tex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return; }
            }
        }
        else if (msg.sender_type == 2)
        {
            if (string.Equals(msg.title, "TRIPLE-DOBLE", StringComparison.OrdinalIgnoreCase)
             || string.Equals(msg.title, "EXPLOSIÓN", StringComparison.OrdinalIgnoreCase))
            {
                string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
                if (playerName != null && _playerByName.TryGetValue(playerName, out var player))
                {
                    var tex = PlayerPhotoHelper.Load(player.id, player.photo);
                    if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return; }
                }
            }
            else
            {
                string teamName = ExtractFirstTeamFromBody(msg.body);
                if (teamName != null)
                {
                    var team = _allTeams?.FirstOrDefault(t => t.name == teamName);
                    if (team != null && _logoSprites.TryGetValue(team.logo, out var sprite))
                    {
                        icon.style.backgroundImage = new StyleBackground(sprite);
                        return;
                    }
                }
            }
        }
        else
        {
            string key = GetNoticiasKey(msg.title);
            if (key != null)
            {
                var tex = _noticiasSprites.GetValueOrDefault(key);
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return; }
            }
        }
    }

    string ExtractFirstFromBody(string body, IEnumerable<string> knownNames)
    {
        if (string.IsNullOrEmpty(body)) return null;
        int earliest = int.MaxValue;
        string found = null;
        foreach (var name in knownNames)
        {
            int idx = body.IndexOf(name, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliest)
            {
                earliest = idx;
                found = name;
            }
        }
        return found;
    }

    string ExtractFirstTeamFromBody(string body)
    {
        if (string.IsNullOrEmpty(body) || _allTeams == null) return null;
        int earliest = int.MaxValue;
        string found = null;
        foreach (var team in _allTeams)
        {
            int idx = body.IndexOf(team.name, StringComparison.Ordinal);
            if (idx >= 0 && idx < earliest)
            {
                earliest = idx;
                found = team.name;
            }
        }
        return found;
    }

    string GetNoticiasKey(string title)
    {
        if (string.IsNullOrEmpty(title)) return null;
        if (title.StartsWith("Bienvenido")) return "bienvenida";
        if (title.StartsWith("Recuperado")) return "recuperado";
        if (title.StartsWith("Queja") || title.StartsWith("Preocupación")) return "quejas";
        if (title.StartsWith("Fichaje:")) return "contratado";
        if (title.Contains("Nuevo empleado")) return "contratado";
        if (title.StartsWith("Fichaje rechazado")) return "rechazado";
        if (title.Contains("cancelado") || title.Contains("rechazado") || title.Contains("despedido")) return "despedido";
        if (title.Contains("Entrenamiento")) return "entrenamiento";
        if (title.Contains("Lesión") || title.Contains("RESERVA")) return "lesionados";
        if (title.Contains("Remodelación")) return "obras-finalizadas";
        if (title.Contains("nóminas") || title.Contains("abonos")) return "pago-nominas";
        if (title.Contains("Préstamo")) return "prestamo-bancario";
        if (title.Contains("renovado") || title.Contains("Re-firma") || title.Contains("Oferta rechazada")) return "renovacion";
        if (title.Contains("Traspaso") || title.Contains("Sign & Trade") || title.Contains("Intercambio")
            || title.Contains("Última semana") || title.Contains("OFERTAS") || title.Contains("Rescisión")
            || title.Contains("Hard cap") || title.Contains("buyout")) return "traspaso";
        return null;
    }

    // ── RELATIVE TIME ──

    string GetRelativeTime(string dateStr)
    {
        if (string.IsNullOrEmpty(dateStr)) return "";

        DateTime date;
        if (!DateTime.TryParse(dateStr, out date))
            return dateStr;

        var diff = DateTime.Now - date;
        if (diff.TotalMinutes < 1) return "Ahora mismo";
        if (diff.TotalMinutes < 60) return $"Hace {(int)diff.TotalMinutes} min";
        if (diff.TotalHours < 24) return $"Hace {(int)diff.TotalHours} h";
        if (diff.TotalDays < 7) return $"Hace {(int)diff.TotalDays} día{((int)diff.TotalDays != 1 ? "s" : "")}";
        if (diff.TotalDays < 30) return $"Hace {(int)(diff.TotalDays / 7)} semana{((int)(diff.TotalDays / 7) != 1 ? "s" : "")}";
        return $"Hace {(int)(diff.TotalDays / 30)} mes{((int)(diff.TotalDays / 30) != 1 ? "es" : "")}";
    }
}
