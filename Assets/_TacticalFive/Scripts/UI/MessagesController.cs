using UnityEngine;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;

public class MessagesController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Messages;

    private const int ITEMS_PER_PAGE = 5;

    private VisualElement _messagesBody;
    private VisualElement _paginator;
    private TextField _searchField;
    private DropdownField _sortDropdown;
    private Button _btnPrev;
    private Button _btnNext;
    private Label _pageLabel;

    private List<MessageData> _allMessages;
    private List<MessageData> _filteredMessages;
    private string _activeCategory = "TODAS";
    private string _searchQuery = "";
    private bool _sortAsc = false;
    private int _currentPage = 0;

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
        _sortDropdown = _root.Q<DropdownField>("SortDropdown");

        _filterTabs[0] = _root.Q<Button>("FilterTodas");
        _filterTabs[1] = _root.Q<Button>("FilterClub");
        _filterTabs[2] = _root.Q<Button>("FilterLiga");
        _filterTabs[3] = _root.Q<Button>("FilterTraspasos");
        _filterTabs[4] = _root.Q<Button>("FilterLesiones");
        _filterTabs[5] = _root.Q<Button>("FilterPrensa");
    }

    protected override void LoadData()
    {
        base.LoadData();

        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;

        if (_noticiasSprites.Count == 0)
        {
            var nombres = new[] {
                "contratado", "despedido", "rechazado", "entrenamiento", "lesionados",
                "obras-finalizadas", "pago-nominas", "prestamo-bancario", "renovacion",
                "traspaso", "bienvenida", "recuperado", "quejas"
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

        if (_sortDropdown != null)
        {
            _sortDropdown.choices = new List<string> { "Más recientes", "Más antiguos" };
            _sortDropdown.value = "Más recientes";
            _sortDropdown.RegisterValueChangedCallback(evt =>
            {
                _sortAsc = evt.newValue == "Más antiguos";
                _currentPage = 0;
                ApplyFilters();
            });
        }

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

        var query = _filteredMessages = _allMessages.AsEnumerable();

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

        query = _sortAsc
            ? query.OrderBy(m => m.id)
            : query.OrderByDescending(m => m.id);

        _filteredMessages = query.ToList();
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
            return;
        }

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
    }

    VisualElement CreateNewsCard(MessageData msg)
    {
        var card = new VisualElement();
        card.AddToClassList("news-card");
        if (msg.is_read == 0)
            card.AddToClassList("news-card--unread");

        var photo = new VisualElement();
        photo.AddToClassList("news-card-photo");
        SetNewsImage(photo, msg);
        card.Add(photo);

        var textBlock = new VisualElement();
        textBlock.AddToClassList("news-card-text");

        var badgeRow = new VisualElement();
        badgeRow.AddToClassList("news-card-badge-row");

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

        var meta = new VisualElement();
        meta.AddToClassList("news-card-meta");

        var catLabel = new Label(GetCategoryLabel(category).ToUpper());
        catLabel.AddToClassList("news-card-category");
        meta.Add(catLabel);

        var timeLabel = new Label(GetRelativeTime(msg.date_sent ?? msg.game_date));
        timeLabel.AddToClassList("news-card-time");
        meta.Add(timeLabel);

        textBlock.Add(meta);
        card.Add(textBlock);

        var arrow = new Label("\u203A");
        arrow.AddToClassList("news-card-arrow");
        card.Add(arrow);

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

    // ── CATEGORY ──

    string GetCategory(MessageData msg)
    {
        string t = msg.title ?? "";
        string b = msg.body ?? "";
        string combined = t + " " + b;

        if (msg.sender_type == 2)
        {
            if (t.Contains("Premios del Mes") || t.Contains("All-Star") || t.Contains("Playoffs")
                || t.Contains("Play-In") || t.Contains("Draft") || t.Contains("Racha")
                || t.Contains("Campanada") || t.Contains("TRIPLE-DOBLE") || t.Contains("EXPLOSIÓN")
                || t.Contains("Resultado"))
                return "LIGA";
            return "CLUB";
        }

        if (t.Contains("Lesión") || t.Contains("RESERVA") || t.Contains("Recuperado")
            || t.Contains("recuperado") || t.Contains("TRATAR"))
            return "LESIONES";

        if (t.Contains("Traspaso") || t.Contains("Sign & Trade") || t.Contains("Intercambio")
            || t.Contains("OFERTAS") || t.Contains("Rescisión") || t.Contains("Hard cap")
            || t.Contains("buyout") || t.Contains("Última semana") || t.Contains("TRANSF")
            || t.Contains("transferible"))
            return "TRASPASOS";

        if (t.Contains("Resultado:") || t.Contains("Racha") || t.Contains("Campanada")
            || t.Contains("TRIPLE-DOBLE") || t.Contains("EXPLOSIÓN") || t.Contains("Premios del Mes")
            || t.Contains("All-Star") || t.Contains("Playoffs") || t.Contains("Play-In")
            || t.Contains("Draft") || t.Contains("Jornada"))
            return "LIGA";

        if (t.Contains("Queja") || t.Contains("Preocupación"))
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
