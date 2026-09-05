using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;
    public class SponsorsController : UIScreenController
{
    private VisualElement _currentSponsorBanner;
    private Label _currentSponsorName;
    private VisualElement _cardsContainer;
    private Label _infoMessage;
    private SponsorData _currentSponsor;
    private List<SponsorData> _availableSponsors;
    protected override GameScreen ScreenId => GameScreen.Sponsors;
    protected override void CacheReferences()
    {
        _btnAction = _root.Q<Button>("BtnAction");
        _currentSponsorBanner = _root.Q<VisualElement>("CurrentSponsorBanner");
        _currentSponsorName = _root.Q<Label>("CurrentSponsorName");
        _cardsContainer = _root.Q<VisualElement>("SponsorsCardsContainer");
        _infoMessage = _root.Q<Label>("SponsorsInfoMessage");
    }

    protected override void LoadData()
    {
        base.LoadData();
        if (_manager == null) return;

        _currentSponsor = DatabaseManager.Instance.GetActiveSponsor(_myTeam.id);
        _availableSponsors = DatabaseManager.Instance.GetAvailableSponsors(_myTeam.id);
    }

    protected override void Refresh()
    {
        if (CursorManager.Instance != null)
            CursorManager.Instance.SetDefaultCursor();
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[Sponsors] RefreshHeader error: {ex.Message}"); }
        BuildCurrentSponsorBanner();
        BuildCards();
    }

    // Sponsors can only be signed in September (preseason) or October (days 1-10)
    bool IsOctober()
    {
        if (_season == null) return false;
        int day = _season.current_game_day;
        return day <= 10;
    }

    void BuildCurrentSponsorBanner()
    {
        if (_currentSponsor != null)
        {
            _currentSponsorBanner.style.display = DisplayStyle.Flex;
            _currentSponsorName.text = _currentSponsor.name;
        }
        else
        {
            _currentSponsorBanner.style.display = DisplayStyle.None;
        }
    }

    void BuildCards()
    {
        _cardsContainer.Clear();

        if (_availableSponsors == null || _availableSponsors.Count == 0)
        {
            var emptyLbl = new Label("No hay patrocinadores disponibles.");
            emptyLbl.AddToClassList("sponsors-info-message");
            _cardsContainer.Add(emptyLbl);
            return;
        }

        bool hasCurrent = _currentSponsor != null;

        foreach (var sponsor in _availableSponsors)
        {
            var card = CreateCard(sponsor, hasCurrent);
            _cardsContainer.Add(card);
        }
    }

    VisualElement CreateCard(SponsorData sponsor, bool hasCurrent)
    {
        var card = new VisualElement();
        card.AddToClassList("sponsor-card");

        // Logo
        var logo = new VisualElement();
        logo.AddToClassList("sponsor-card-logo");
        // Load sponsor logo from Resources (strip .png extension for Resources.Load)
        var logoPath = sponsor.logo?.Replace(".png", "");
        var sponsorLogo = Resources.Load<Sprite>(logoPath);
        if (sponsorLogo != null)
            logo.style.backgroundImage = new StyleBackground(sponsorLogo);

        // If we have a current sponsor and this is not it, show in grayscale
        if (hasCurrent && _currentSponsor != null && sponsor.id != _currentSponsor.id)
            logo.AddToClassList("sponsor-card-logo--grayscale");

        card.Add(logo);

        // Name
        var nameLbl = new Label(sponsor.name.ToUpper());
        nameLbl.AddToClassList("sponsor-card-name");
        card.Add(nameLbl);

        // Ingreso Inicial
        card.Add(CreateCardRow("Ingreso Inicial", $"${sponsor.initial_income:N0}"));

        // Por Partido en Casa
        card.Add(CreateCardRow("Por Partido en Casa", $"${sponsor.home_game_income:N0}"));

        // Duración
        card.Add(CreateCardRow("Duración", $"{sponsor.contract_years} año{(sponsor.contract_years > 1 ? "s" : "")}"));

        // Button
        var btn = new Button();
        btn.AddToClassList("sponsor-card-btn");
        bool isContracted = hasCurrent && _currentSponsor != null && _currentSponsor.id == sponsor.id;

        if (isContracted)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (hasCurrent)
        {
            btn.text = "CONTRATADO";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else if (!IsOctober())
        {
            btn.text = "SOLO OCTUBRE";
            btn.AddToClassList("sponsor-card-btn--disabled");
            btn.SetEnabled(false);
        }
        else
        {
            btn.text = "CONTRATAR";
            var sponsorCopy = sponsor;
            btn.clicked += () => { PlayClick(); SignSponsor(sponsorCopy); };
        }
        card.Add(btn);
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);

        return card;
    }

    VisualElement CreateCardRow(string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("sponsor-card-row");

        var lbl = new Label(label);
        lbl.AddToClassList("sponsor-card-label");

        var val = new Label(value);
        val.AddToClassList("sponsor-card-value");

        row.Add(lbl);
        row.Add(val);

        return row;
    }

    void SignSponsor(SponsorData sponsor)
    {
        if (_currentSponsor != null) return; // Can't sign if already have one
        if (!IsOctober()) return; // Sponsors can only be signed in October

        DatabaseManager.Instance.SignSponsor(sponsor.id, _season.id, _myTeam.id, _season.current_game_day);

        // Send message
        var msg = new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = $"Patrocinador firmado: {sponsor.name.ToUpper()}",
            body = $"Se ha firmado un nuevo patrocinio con {sponsor.name}.\nIngreso inicial: ${sponsor.initial_income:N0}",
            created_at = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            is_read = 0
        };
        DatabaseManager.Instance.AddMessage(msg);

        LoadData();
        Refresh();
    }
}
