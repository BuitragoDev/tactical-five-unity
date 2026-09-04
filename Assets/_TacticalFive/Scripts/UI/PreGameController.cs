using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class PreGameController : UIScreenController
{
    private Label _mpvFavorite;
    private VisualElement _mpvHomeLogo, _mpvAwayLogo;
    private Label _mpvHomeName, _mpvHomeRecord, _mpvHomeConf;
    private Label _mpvAwayName, _mpvAwayRecord, _mpvAwayConf;
    private VisualElement _mpvHomeLast10, _mpvAwayLast10;
    private Label _mpvDate, _mpvArena;
    private Label _mpvHomeOff, _mpvHomeOffRank, _mpvHomeDef, _mpvHomeDefRank;
    private Label _mpvAwayOff, _mpvAwayOffRank, _mpvAwayDef, _mpvAwayDefRank;
    private VisualElement _mpvHomeBajas, _mpvAwayBajas;
    private Label _mpvHomeBajasTitle, _mpvAwayBajasTitle;
    private VisualElement _mpvHomeStarters, _mpvAwayStarters;
    private VisualElement _mpvHomeBench, _mpvAwayBench;
    private VisualElement _mpvKeyRow1, _mpvKeyRow2, _mpvKeyRow3, _mpvKeyRow4;

    private Button _btnBack, _btnGoMatch;

    private List<TeamData> _allTeams;
    private Dictionary<string, Sprite> _logoSprites = new();

    protected override void CacheReferences()
    {
        _mpvFavorite = _root.Q<Label>("MPVFavorite");
        _mpvHomeLogo = _root.Q<VisualElement>("MPVHomeLogo");
        _mpvAwayLogo = _root.Q<VisualElement>("MPVAwayLogo");
        _mpvHomeName = _root.Q<Label>("MPVHomeName");
        _mpvHomeRecord = _root.Q<Label>("MPVHomeRecord");
        _mpvHomeConf = _root.Q<Label>("MPVHomeConf");
        _mpvAwayName = _root.Q<Label>("MPVAwayName");
        _mpvAwayRecord = _root.Q<Label>("MPVAwayRecord");
        _mpvAwayConf = _root.Q<Label>("MPVAwayConf");
        _mpvHomeLast10 = _root.Q<VisualElement>("MPVHomeLast10");
        _mpvAwayLast10 = _root.Q<VisualElement>("MPVAwayLast10");
        _mpvDate = _root.Q<Label>("MPVDate");
        _mpvArena = _root.Q<Label>("MPVArena");
        _mpvHomeOff = _root.Q<Label>("MPVHomeOff"); _mpvHomeOffRank = _root.Q<Label>("MPVHomeOffRank");
        _mpvHomeDef = _root.Q<Label>("MPVHomeDef"); _mpvHomeDefRank = _root.Q<Label>("MPVHomeDefRank");
        _mpvAwayOff = _root.Q<Label>("MPVAwayOff"); _mpvAwayOffRank = _root.Q<Label>("MPVAwayOffRank");
        _mpvAwayDef = _root.Q<Label>("MPVAwayDef"); _mpvAwayDefRank = _root.Q<Label>("MPVAwayDefRank");
        _mpvHomeBajas = _root.Q<VisualElement>("MPVHomeBajas");
        _mpvAwayBajas = _root.Q<VisualElement>("MPVAwayBajas");
        _mpvHomeBajasTitle = _root.Q<Label>("MPVHomeBajasTitle");
        _mpvAwayBajasTitle = _root.Q<Label>("MPVAwayBajasTitle");
        _mpvHomeStarters = _root.Q<VisualElement>("MPVHomeStarters");
        _mpvAwayStarters = _root.Q<VisualElement>("MPVAwayStarters");
        _mpvHomeBench = _root.Q<VisualElement>("MPVHomeBench");
        _mpvAwayBench = _root.Q<VisualElement>("MPVAwayBench");
        _mpvKeyRow1 = _root.Q<VisualElement>("MPVKeyRow1");
        _mpvKeyRow2 = _root.Q<VisualElement>("MPVKeyRow2");
        _mpvKeyRow3 = _root.Q<VisualElement>("MPVKeyRow3");
        _mpvKeyRow4 = _root.Q<VisualElement>("MPVKeyRow4");

        _btnBack = _root.Q<Button>("BtnBack");
        _btnGoMatch = _root.Q<Button>("BtnGoMatch");
    }

    protected override void LoadData()
    {
        base.LoadData();
        var logos = Resources.LoadAll<Sprite>("Teams/Logos");
        foreach (var s in logos) _logoSprites[s.name] = s;
        _allTeams = DatabaseManager.Instance.GetAllTeams();
    }

    protected override void RegisterCallbacks()
    {
        _btnBack?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.Dashboard);
        });

        _btnGoMatch?.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            DashboardController.RequestSimulateAndGoToMatchDay();
        });
    }

    protected override void Refresh()
    {
        LoadMatchPreview();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            PlayClick();
            DashboardController.RequestSimulateAndGoToMatchDay();
        }
    }

    void LoadMatchPreview()
    {
        if (_season == null || _myTeam == null) return;

        // Usar current_date para calcular el gameDay (mismo criterio que FindNextGameDay)
        int gameDay = DateToGameDay(System.DateTime.Parse(_season.current_date));
        var gamesToday = DatabaseManager.Instance.GetAllGamesByGameDay(_manager.id, gameDay);
        var myGame = gamesToday.FirstOrDefault(g =>
            !DashboardController.IsGLeagueGame(g) &&
            (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id));
        if (myGame == null) return;

        var home = _allTeams.Find(t => t.id == myGame.home_team_id);
        var away = _allTeams.Find(t => t.id == myGame.away_team_id);
        if (home == null || away == null) return;

        var preview = MatchupPreview.Compute(myGame.home_team_id, myGame.away_team_id,
            myGame.home_team_id == _myTeam.id, _manager.id, _season.id, myGame.game_date);

        if (_mpvFavorite != null)
        {
            _mpvFavorite.text = string.IsNullOrEmpty(preview.favoriteName)
                ? ""
                : $"FAVORITO {preview.favoriteName.ToUpper()}";
        }

        var h = preview.home;
        var a = preview.away;

        SetLogo(_mpvHomeLogo, home.logo);
        SetLogo(_mpvAwayLogo, away.logo);
        if (_mpvHomeName != null) _mpvHomeName.text = h.teamName;
        if (_mpvHomeRecord != null) _mpvHomeRecord.text = $"{h.wins}-{h.losses}";
        if (_mpvHomeConf != null) _mpvHomeConf.text = $"{h.conferenceRank}º EN CONFERENCIA {h.conferenceName}";
        if (_mpvAwayName != null) _mpvAwayName.text = a.teamName;
        if (_mpvAwayRecord != null) _mpvAwayRecord.text = $"{a.wins}-{a.losses}";
        if (_mpvAwayConf != null) _mpvAwayConf.text = $"{a.conferenceRank}º EN CONFERENCIA {a.conferenceName}";

        BuildLast10(_mpvHomeLast10, h.last10);
        BuildLast10(_mpvAwayLast10, a.last10);

        if (_mpvDate != null) _mpvDate.text = preview.gameDate;
        if (_mpvArena != null) _mpvArena.text = preview.arenaName?.ToUpper() ?? "";

        SetStatLabel(_mpvHomeOff, h.offRating.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        SetStatLabel(_mpvHomeDef, h.defRating.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        SetStatLabel(_mpvHomeOffRank, $"{h.offRank}º EN LA COMPETICIÓN");
        SetStatLabel(_mpvHomeDefRank, $"{h.defRank}º EN LA COMPETICIÓN");

        SetStatLabel(_mpvAwayOff, a.offRating.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        SetStatLabel(_mpvAwayDef, a.defRating.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        SetStatLabel(_mpvAwayOffRank, $"{a.offRank}º EN LA COMPETICIÓN");
        SetStatLabel(_mpvAwayDefRank, $"{a.defRank}º EN LA COMPETICIÓN");

        if (_mpvHomeBajasTitle != null) _mpvHomeBajasTitle.text = home.name.ToUpper();
        if (_mpvAwayBajasTitle != null) _mpvAwayBajasTitle.text = away.name.ToUpper();
        BuildBajas(_mpvHomeBajas, h.injured);
        BuildBajas(_mpvAwayBajas, a.injured);

        BuildPreviewRoster(_mpvHomeStarters, _mpvHomeBench, h.starters, h.bench);
        BuildPreviewRoster(_mpvAwayStarters, _mpvAwayBench, a.starters, a.bench);

        BuildKeyPlayerRow(_mpvKeyRow1, "PUNTOS", h.keyPts, h.keyPtsVal, a.keyPts, a.keyPtsVal);
        BuildKeyPlayerRow(_mpvKeyRow2, "REBOTES", h.keyReb, h.keyRebVal, a.keyReb, a.keyRebVal);
        BuildKeyPlayerRow(_mpvKeyRow3, "ASIST.", h.keyAst, h.keyAstVal, a.keyAst, a.keyAstVal);
        BuildKeyPlayerRow(_mpvKeyRow4, "TAPONES", h.keyBlk, h.keyBlkVal, a.keyBlk, a.keyBlkVal);
    }

    // ── Helpers (moved from MatchDayController) ──

    int DateToGameDay(System.DateTime date)
    {
        var seasonStart = new System.DateTime(_season.year_start, 10, 22);
        if (date >= seasonStart)
            return (int)(date - seasonStart).TotalDays + 1;
        else
            return -(int)(seasonStart - date).TotalDays;
    }

    void SetStatLabel(Label lbl, string text)
    {
        if (lbl != null) lbl.text = text;
    }

    void SetLogo(VisualElement elem, string logoName)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;
        if (_logoSprites.TryGetValue(logoName, out var sprite))
            elem.style.backgroundImage = new StyleBackground(sprite);
    }

    void BuildLast10(VisualElement container, List<char> results)
    {
        if (container == null) return;
        container.Clear();
        foreach (var r in results)
        {
            var dot = new VisualElement();
            dot.AddToClassList("mpv-streak-dot");
            dot.AddToClassList(r == 'G' ? "mpv-streak-dot--win" : "mpv-streak-dot--loss");
            var lbl = new Label(r.ToString());
            lbl.style.fontSize = 18;
            lbl.style.color = new Color(1, 1, 1, 0.9f);
            lbl.style.unityTextAlign = TextAnchor.MiddleCenter;
            dot.Add(lbl);
            container.Add(dot);
        }
    }

    void BuildBajas(VisualElement container, List<PlayerData> injured)
    {
        if (container == null) return;
        container.Clear();
        foreach (var p in injured)
        {
            var item = new VisualElement();
            item.AddToClassList("mpv-baja-item");

            var photo = new VisualElement();
            photo.AddToClassList("mpv-baja-photo");
            var tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null) photo.style.backgroundImage = new StyleBackground(tex);
            item.Add(photo);

            var nameLbl = new Label($"{p.first_name[0]}. {p.last_name.ToUpper()}");
            nameLbl.AddToClassList("mpv-baja-name");
            item.Add(nameLbl);

            container.Add(item);
        }
    }

    void BuildPreviewRoster(VisualElement starterRow, VisualElement benchList,
        List<PlayerData> starters, List<PlayerData> bench)
    {
        if (starterRow == null || benchList == null) return;
        starterRow.Clear();
        benchList.Clear();

        foreach (var p in starters) starterRow.Add(BuildStarterCard(p));
        foreach (var p in bench) benchList.Add(BuildBenchRow(p));
    }

    VisualElement BuildStarterCard(PlayerData p)
    {
        var card = new VisualElement();
        card.AddToClassList("mpv-starter-card");

        var photo = new VisualElement();
        photo.AddToClassList("mpv-starter-photo");
        var tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null) photo.style.backgroundImage = new StyleBackground(tex);
        card.Add(photo);

        var posLbl = new Label(PositionCodes.GetName(p.position));
        posLbl.AddToClassList("mpv-starter-pos");
        card.Add(posLbl);

        var nameLbl = new Label($"{FormatPlayerName(p)}");
        nameLbl.AddToClassList("mpv-starter-name");
        card.Add(nameLbl);

        var ovrLbl = new Label($"{p.overall}");
        ovrLbl.AddToClassList("mpv-starter-ovr");
        card.Add(ovrLbl);

        return card;
    }

    VisualElement BuildBenchRow(PlayerData p)
    {
        var row = new VisualElement();
        row.AddToClassList("mpv-bench-card");

        var nameLbl = new Label($"{FormatPlayerName(p)}");
        nameLbl.AddToClassList("mpv-bench-name");
        row.Add(nameLbl);

        var posLbl = new Label(PositionCodes.GetName(p.position));
        posLbl.AddToClassList("mpv-bench-pos");
        row.Add(posLbl);

        var ovrLbl = new Label($"{p.overall}");
        ovrLbl.AddToClassList("mpv-bench-ovr");
        row.Add(ovrLbl);

        return row;
    }

    string FormatPlayerName(PlayerData p)
    {
        var first = p.first_name?.Trim();
        var last = p.last_name?.Trim().ToUpper();
        if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(last))
            return $"{first} {last}";
        return $"{first[0]}. {last}";
    }

    void BuildKeyPlayerRow(VisualElement row, string statLabel,
        PlayerData homePlayer, float homeVal,
        PlayerData awayPlayer, float awayVal)
    {
        if (row == null) return;
        row.Clear();

        var homeSide = new VisualElement();
        homeSide.AddToClassList("mpv-key-player");
        if (homePlayer != null)
        {
            var photo = new VisualElement();
            photo.AddToClassList("mpv-key-photo");
            var tex = PlayerPhotoHelper.Load(homePlayer.id, homePlayer.photo);
            if (tex != null) photo.style.backgroundImage = new StyleBackground(tex);
            homeSide.Add(photo);
            var name = new Label($"{homePlayer.first_name[0]}. {homePlayer.last_name.ToUpper()}");
            name.AddToClassList("mpv-key-name");
            homeSide.Add(name);
        }
        row.Add(homeSide);

        var homeStat = new Label(homeVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        homeStat.AddToClassList("mpv-key-stat");
        row.Add(homeStat);

        var center = new Label(statLabel);
        center.AddToClassList("mpv-key-label");
        row.Add(center);

        var awayStat = new Label(awayVal.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        awayStat.AddToClassList("mpv-key-stat");
        row.Add(awayStat);

        var awaySide = new VisualElement();
        awaySide.AddToClassList("mpv-key-player");
        awaySide.AddToClassList("mpv-key-player--right");
        if (awayPlayer != null)
        {
            var name = new Label($"{awayPlayer.first_name[0]}. {awayPlayer.last_name.ToUpper()}");
            name.AddToClassList("mpv-key-name");
            awaySide.Add(name);
            var photo = new VisualElement();
            photo.AddToClassList("mpv-key-photo");
            var tex = PlayerPhotoHelper.Load(awayPlayer.id, awayPlayer.photo);
            if (tex != null) photo.style.backgroundImage = new StyleBackground(tex);
            awaySide.Add(photo);
        }
        row.Add(awaySide);
    }
}
