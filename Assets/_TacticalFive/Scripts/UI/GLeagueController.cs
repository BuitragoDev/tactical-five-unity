using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class GLeagueController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.GLeague;

    // Resumen
    private Label _summaryAssigned;
    private Label _summaryTwoWay;

    // Tablas
    private VisualElement _assignedHeader;
    private VisualElement _assignedBody;
    private VisualElement _eligibleHeader;
    private VisualElement _eligibleBody;

    private List<PlayerData> _players = new();

    protected override void CacheReferences()
    {
        _summaryAssigned = _root.Q<Label>("SummaryAssigned");
        _summaryTwoWay = _root.Q<Label>("SummaryTwoWay");
        _assignedHeader = _root.Q<VisualElement>("AssignedHeader");
        _assignedBody = _root.Q<VisualElement>("AssignedBody");
        _eligibleHeader = _root.Q<VisualElement>("EligibleHeader");
        _eligibleBody = _root.Q<VisualElement>("EligibleBody");
    }

    protected override void LoadData()
    {
        base.LoadData();
        _players = _myTeam != null
            ? DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            : new List<PlayerData>();
    }

    protected override void Refresh()
    {
        try { RefreshHeader(); } catch (System.Exception ex) { Debug.LogWarning($"[GLeague] RefreshHeader error: {ex.Message}"); }
        RefreshSummary();
        BuildAssigned();
        BuildEligible();
    }

    void RefreshSummary()
    {
        if (_summaryAssigned != null)
            _summaryAssigned.text = _players.Count(p => p.g_league_assigned == 1).ToString();
        if (_summaryTwoWay != null && _myTeam != null)
            _summaryTwoWay.text = $"{DatabaseManager.Instance.GetTwoWayCount(_myTeam.id)}/{TradeHelper.MAX_TWO_WAY}";
    }

    // ── ASIGNADOS ────────────────────────────────────────

    void BuildAssigned()
    {
        if (_assignedHeader != null)
        {
            _assignedHeader.Clear();
            _assignedHeader.Add(MakeHeaderCell("JUGADOR", "gleague-player"));
            _assignedHeader.Add(MakeHeaderCell("POS", "col-pos"));
            _assignedHeader.Add(MakeHeaderCell("EDAD", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("PJ", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("PTS", "col-stat", true));
            _assignedHeader.Add(MakeHeaderCell("REB", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("AST", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("ROB", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("TAP", "col-stat"));
            _assignedHeader.Add(MakeHeaderCell("", "gleague-action"));
        }

        if (_assignedBody == null) return;
        _assignedBody.Clear();

        var assigned = _players
            .Where(p => p.g_league_assigned == 1)
            .OrderByDescending(p => p.potential)
            .ToList();

        if (assigned.Count == 0)
        {
            _assignedBody.Add(MakeEmpty("NO HAY JUGADORES ASIGNADOS A LA G-LEAGUE"));
            return;
        }

        foreach (var p in assigned)
        {
            var row = new VisualElement();
            row.AddToClassList("stats-row");
            row.AddToClassList("stats-row--my-team");

            row.Add(MakePlayerCell(p));

            row.Add(MakeStatCell(PositionCodes.GetShort(p.position), false, "col-pos"));
            row.Add(MakeStatCell(p.age.ToString(), false, "col-stat"));

            var s = _season != null ? DatabaseManager.Instance.GetGLeagueStats(p.id, _season.id) : null;
            int games = s?.games ?? 0;
            row.Add(MakeStatCell(games.ToString(), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(s?.points, games), true, "col-stat"));
            row.Add(MakeStatCell(PerGame(s?.rebounds, games), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(s?.assists, games), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(s?.steals, games), false, "col-stat"));
            row.Add(MakeStatCell(PerGame(s?.blocks, games), false, "col-stat"));

            row.Add(MakeActionCell("RECUPERAR", true, () =>
            {
                DatabaseManager.Instance.SetGLeagueAssignment(p, false);
                Refresh();
            }));

            _assignedBody.Add(row);
        }
    }

    // ── ELEGIBLES ────────────────────────────────────────

    void BuildEligible()
    {
        if (_eligibleHeader != null)
        {
            _eligibleHeader.Clear();
            _eligibleHeader.Add(MakeHeaderCell("JUGADOR", "gleague-player"));
            _eligibleHeader.Add(MakeHeaderCell("POS", "col-pos"));
            _eligibleHeader.Add(MakeHeaderCell("EDAD", "col-stat"));
            _eligibleHeader.Add(MakeHeaderCell("MEDIA", "col-stat", true));
            _eligibleHeader.Add(MakeHeaderCell("POT", "col-stat"));
            _eligibleHeader.Add(MakeHeaderCell("", "gleague-action"));
        }

        if (_eligibleBody == null) return;
        _eligibleBody.Clear();

        bool enough = GLeagueHelper.HasEnoughActive(_players);
        var eligible = _players
            .Where(p => GLeagueHelper.CanAssign(p))
            .OrderByDescending(p => p.potential)
            .ToList();

        if (!enough || eligible.Count == 0)
        {
            _eligibleBody.Add(MakeEmpty(enough
                ? "NO HAY JUGADORES ELEGIBLES PARA ASIGNAR"
                : "NECESITAS AL MENOS 12 JUGADORES ACTIVOS EN LA NBA PARA ASIGNAR A LA G-LEAGUE"));
            return;
        }

        foreach (var p in eligible)
        {
            var row = new VisualElement();
            row.AddToClassList("stats-row");
            row.AddToClassList("stats-row--my-team");

            row.Add(MakePlayerCell(p));

            row.Add(MakeStatCell(PositionCodes.GetShort(p.position), false, "col-pos"));
            row.Add(MakeStatCell(p.age.ToString(), false, "col-stat"));
            row.Add(MakeStatCell(p.GetCalculatedAverage().ToString(), true, "col-stat"));
            row.Add(MakeStatCell(p.potential.ToString(), false, "col-stat"));

            row.Add(MakeActionCell("ASIGNAR", false, () =>
            {
                DatabaseManager.Instance.SetGLeagueAssignment(p, true);
                Refresh();
            }));

            _eligibleBody.Add(row);
        }
    }

    // ── HELPERS DE CELDA ─────────────────────────────────

    Label MakeHeaderCell(string text, string baseClass, bool bold = false)
    {
        var lbl = new Label();
        lbl.AddToClassList(baseClass);
        lbl.AddToClassList("col-stat--header");
        if (bold) lbl.AddToClassList("col-stat--bold");
        lbl.text = text;
        return lbl;
    }

    Label MakeStatCell(string value, bool bold, string baseClass)
    {
        var lbl = new Label();
        lbl.AddToClassList(baseClass);
        if (bold) lbl.AddToClassList("col-stat--bold");
        lbl.text = value;
        return lbl;
    }

    VisualElement MakePlayerCell(PlayerData p)
    {
        var cell = new VisualElement();
        cell.AddToClassList("gleague-player");

        var avatar = new VisualElement();
        avatar.AddToClassList("gleague-avatar");
        Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
        if (tex != null)
            avatar.style.backgroundImage = new StyleBackground(tex);
        cell.Add(avatar);

        var nameLbl = new Label();
        nameLbl.AddToClassList("gleague-player-name");
        nameLbl.text = $"{p.first_name} {p.last_name}";
        cell.Add(nameLbl);

        return cell;
    }

    VisualElement MakeActionCell(string text, bool isOn, System.Action onClick)
    {
        var cell = new VisualElement();
        cell.AddToClassList("gleague-action");

        var btn = new Button();
        btn.AddToClassList("gleague-action-btn");
        if (isOn) btn.AddToClassList("gleague-action-btn--on");
        btn.text = text;
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            onClick();
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(btn);
        cell.Add(btn);

        return cell;
    }

    VisualElement MakeEmpty(string message)
    {
        var empty = new VisualElement();
        empty.AddToClassList("stats-empty");
        var lbl = new Label();
        lbl.AddToClassList("stats-empty-label");
        lbl.text = message;
        empty.Add(lbl);
        return empty;
    }

    static string PerGame(int? total, int games)
    {
        if (games <= 0 || total == null) return "—";
        return (total.Value / (float)games).ToString("F1");
    }
}
