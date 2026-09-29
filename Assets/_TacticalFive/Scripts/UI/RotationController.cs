using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Linq;

public class RotationController : UIScreenController
{
    protected override GameScreen ScreenId => GameScreen.Rotation;

    // Minutos de un partido de regular: 5 posiciones × 48 minutos.
    const int MAX_MINUTES = 48;
    const int TEAM_MINUTES = 240;
    static readonly string[] PosOrder = { "PG", "SG", "SF", "PF", "C" };

    private VisualElement _startersList;
    private VisualElement _benchList;
    private VisualElement _posSummary;
    private Label _remainingValue;
    private Label _remainingHint;
    private Label _subtitle;

    private List<PlayerData> _players;
    private List<LineupData> _lineup;
    private readonly Dictionary<int, int> _suggested = new();

    // Estado "todo automático": ningún convocado tiene minutos fijados (todos -1). La
    // pantalla muestra entonces el reparto automático de 240 (como el botón AUTO) para no
    // pintar un exceso irreal: las sugerencias por jugador no suman 240.
    private bool _allAuto;
    private readonly Dictionary<int, int> _autoSplit = new();

    // Sliders vivos de la lista, para recalcular el tope de minutos (240 en total).
    private readonly List<(LineupData ls, PlayerData p, bool playable, CustomSlider slider)> _sliders = new();

    protected override void CacheReferences()
    {
        _startersList = _root.Q<VisualElement>("StartersList");
        _benchList = _root.Q<VisualElement>("BenchList");
        _posSummary = _root.Q<VisualElement>("PosSummary");
        _remainingValue = _root.Q<Label>("RemainingValue");
        _remainingHint = _root.Q<Label>("RemainingHint");
        _subtitle = _root.Q<Label>("ScreenSubtitle");
    }

    protected override void RegisterCallbacks()
    {
        base.RegisterCallbacks();

        // Se vuelve a cablear en cada visita: el árbol UI Toolkit se recrea al
        // reactivar la pantalla. Unregister previo para no duplicar callbacks.
        WireButton("AutoMinutesBtn", OnAutoClicked);
        WireButton("ResetMinutesBtn", OnResetClicked);
    }

    void WireButton(string name, EventCallback<ClickEvent> handler)
    {
        var btn = _root.Q<Button>(name);
        if (btn == null)
        {
            Debug.LogWarning($"[Rotation] Botón '{name}' no encontrado en el UXML.");
            return;
        }
        btn.UnregisterCallback<ClickEvent>(handler);
        btn.RegisterCallback<ClickEvent>(handler);
    }

    void OnAutoClicked(ClickEvent evt) { PlayClick(); ApplyAuto(); }
    void OnResetClicked(ClickEvent evt) { PlayClick(); ApplyReset(); }

    protected override void LoadData()
    {
        base.LoadData();
        if (_myTeam == null) return;

        var screenIcon = _root.Q<VisualElement>("ScreenIcon");
        if (screenIcon != null)
        {
            var iconTex = Resources.Load<Texture2D>("Icons/quinteto_icon");
            if (iconTex != null)
                screenIcon.style.backgroundImage = iconTex;
        }

        _players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
    }

    protected override void Refresh()
    {
        base.Refresh();
        _root.Q<Button>("SubmenuRotacion")?.AddToClassList("nav-submenu-item--active");

        if (_myTeam == null || _players == null) return;

        _lineup = DatabaseManager.Instance.EnsureLineupSeeded(_myTeam.id, _players);
        BuildSuggested();
        ComputeAutoSplit();

        if (_subtitle != null)
            _subtitle.text = $"REPARTE LOS {TEAM_MINUTES} MINUTOS DEL PARTIDO ENTRE TUS CONVOCADOS";

        BuildRows();
        UpdateSummary();
    }

    // ── Datos ───────────────────────────────────────────────

    PlayerData FindPlayer(int id) => _players?.FirstOrDefault(p => p.id == id);

    bool IsPlayable(PlayerData p) => p.injury_days == 0 && p.g_league_assigned != 1;

    static string RoleName(PlayerRole role) => role switch
    {
        PlayerRole.Estrella => "Estrella",
        PlayerRole.Titular => "Titular",
        PlayerRole.Banquillo => "Banquillo",
        _ => "Último recurso"
    };

    void BuildSuggested()
    {
        _suggested.Clear();
        if (_lineup == null || _players == null) return;

        foreach (var ls in _lineup)
        {
            if (ls.slot > 1) continue;
            var p = FindPlayer(ls.player_id);
            if (p == null) continue;
            _suggested[p.id] = GameSimulator.GetSuggestedMinutes(p, ls.slot == 0);
        }
    }

    int SuggestedFor(PlayerData p, bool starter)
    {
        if (p != null && _suggested.TryGetValue(p.id, out var s)) return s;
        return GameSimulator.GetSuggestedMinutes(p, starter);
    }

    /// <summary>
    /// Si NINGÚN convocado tiene minutos fijados, calcula el reparto automático de 240
    /// (reparto proporcional del botón AUTO) para mostrarlo tal cual. Así la pantalla
    /// nunca abre con "minutos excedidos" cuando en realidad no hay nada configurado.
    /// </summary>
    void ComputeAutoSplit()
    {
        _autoSplit.Clear();
        if (_lineup == null) { _allAuto = false; return; }

        var entries = ActiveEntries()
            .Where(ls => { var p = FindPlayer(ls.player_id); return p != null && IsPlayable(p); })
            .ToList();

        // Misma regla que el simulador (y que el aviso del Dashboard): sin ningún plan
        // positivo (todos -1 o 0) la rotación es automática.
        var slots = entries.Select(ls =>
        {
            var p = FindPlayer(ls.player_id);
            return new RotationSlot
            {
                playerId = ls.player_id,
                minutes = ls.rotation_minutes,
                playable = true,
                suggestion = SuggestedFor(p, ls.slot == 0)
            };
        }).ToList();

        _allAuto = RotationPlanner.Evaluate(slots).allAuto;
        if (!_allAuto) return;

        var sug = entries.Select(ls => SuggestedFor(FindPlayer(ls.player_id), ls.slot == 0)).ToList();
        var mins = RotationPlanner.AutoAssign(sug);
        for (int i = 0; i < entries.Count && i < mins.Length; i++)
            _autoSplit[entries[i].player_id] = mins[i];
    }

    // Minutos efectivos: los fijados por el usuario o, si no (slot -1), el reparto
    // automático de 240 (o la sugerencia del simulador si no se pudo repartir).
    int Effective(LineupData ls, PlayerData p)
    {
        if (!_allAuto && ls.rotation_minutes >= 0)
            return Mathf.Min(ls.rotation_minutes, MAX_MINUTES);
        if (p != null && _autoSplit.TryGetValue(p.id, out var auto)) return auto;
        return SuggestedFor(p, ls.slot == 0);
    }

    /// <summary>
    /// Fija (persiste) el reparto automático mostrado. Se llama en cuanto el usuario toca
    /// un slider en estado automático: a partir de ahí la BD coincide con la pantalla.
    /// </summary>
    void MaterializeAutoSplit()
    {
        if (!_allAuto || _lineup == null) return;

        foreach (var ls in ActiveEntries())
        {
            var p = FindPlayer(ls.player_id);
            if (p == null) continue;
            int mins = _autoSplit.TryGetValue(p.id, out var split) ? split : 0;
            ls.rotation_minutes = mins;
            DatabaseManager.Instance.SetLineupMinutes(p.id, mins);
        }

        _allAuto = false;
        _autoSplit.Clear();
    }

    List<LineupData> ActiveEntries()
    {
        if (_lineup == null) return new List<LineupData>();
        return _lineup
            .Where(l => l.slot == 0 || l.slot == 1)
            .OrderBy(l => l.slot)
            .ThenBy(l => l.slot_index >= 0 ? l.slot_index : 999)
            .ToList();
    }

    // ── Filas ───────────────────────────────────────────────

    void BuildRows()
    {
        if (_startersList == null || _benchList == null || _lineup == null) return;

        _startersList.Clear();
        _benchList.Clear();
        _sliders.Clear();

        foreach (var ls in ActiveEntries())
        {
            var p = FindPlayer(ls.player_id);
            if (p == null) continue;
            var row = CreateRow(p, ls, ls.slot == 0);
            if (ls.slot == 0) _startersList.Add(row);
            else _benchList.Add(row);
        }

        // Unity no soporta :last-child; marcamos la última fila de cada lista.
        int lastStarter = _startersList.childCount - 1;
        if (lastStarter >= 0) _startersList.ElementAt(lastStarter).AddToClassList("rot-row--last");
        int lastBench = _benchList.childCount - 1;
        if (lastBench >= 0) _benchList.ElementAt(lastBench).AddToClassList("rot-row--last");
    }

    VisualElement CreateRow(PlayerData p, LineupData ls, bool starter)
    {
        int suggested = SuggestedFor(p, starter);
        int effective = Effective(ls, p);
        bool playable = IsPlayable(p);

        var row = new VisualElement();
        row.AddToClassList("rot-row");
        if (starter) row.AddToClassList("rot-row--starter");
        if (!playable) row.AddToClassList("rot-row--disabled");

        // ── Nombre + badge ──
        var nameCell = new VisualElement();
        nameCell.AddToClassList("rot-col");
        nameCell.AddToClassList("rot-col--name");
        nameCell.AddToClassList("rot-name-cell");
        var nameLbl = new Label();
        nameLbl.AddToClassList("rot-name");
        nameLbl.text = $"{p.first_name} {p.last_name}{(p.is_rookie == 1 ? " (R)" : "")}";
        nameCell.Add(nameLbl);

        if (p.injury_days > 0)
            nameCell.Add(CreateBadge($"LESIONADO · {p.injury_days} DÍAS", false));
        else if (p.g_league_assigned == 1)
            nameCell.Add(CreateBadge("G-LEAGUE", true));
        row.Add(nameCell);

        // ── Posición ──
        var posLbl = new Label();
        posLbl.AddToClassList("rot-col");
        posLbl.AddToClassList("rot-col--pos");
        posLbl.AddToClassList("rot-pos");
        string posStr = PositionCodes.GetShort(p.position);
        if (!string.IsNullOrEmpty(p.secondary_position))
            posStr += $"/{PositionCodes.GetShort(p.secondary_position)}";
        posLbl.text = posStr;
        row.Add(posLbl);

        // ── Rol ──
        var roleLbl = new Label();
        roleLbl.AddToClassList("rot-col");
        roleLbl.AddToClassList("rot-col--role");
        roleLbl.AddToClassList("rot-role");
        roleLbl.text = RoleName(p.role);
        roleLbl.tooltip = RoleName(p.role);
        row.Add(roleLbl);

        // ── OVR ──
        int ovr = p.GetCalculatedAverage();
        var ovrLbl = new Label();
        ovrLbl.AddToClassList("rot-col");
        ovrLbl.AddToClassList("rot-col--ovr");
        ovrLbl.AddToClassList("rot-ovr");
        ovrLbl.text = ovr.ToString();
        ovrLbl.AddToClassList(ovr > 84 ? "rot-ovr--high" : ovr >= 70 ? "rot-ovr--mid" : "rot-ovr--low");
        row.Add(ovrLbl);

        // ── Slider de minutos ──
        var minCell = new VisualElement();
        minCell.AddToClassList("rot-col");
        minCell.AddToClassList("rot-col--min");
        var slider = new VisualElement();
        slider.AddToClassList("custom-slider");
        var track = new VisualElement(); track.AddToClassList("custom-slider-track");
        var fill = new VisualElement(); fill.AddToClassList("custom-slider-fill");
        var dragger = new VisualElement(); dragger.AddToClassList("custom-slider-dragger");
        slider.Add(track);
        slider.Add(fill);
        slider.Add(dragger);
        minCell.Add(slider);
        row.Add(minCell);

        var valLbl = new Label();
        valLbl.AddToClassList("rot-col");
        valLbl.AddToClassList("rot-col--val");
        valLbl.AddToClassList("rot-min-val");
        row.Add(valLbl);

        var sugCell = new VisualElement();
        sugCell.AddToClassList("rot-col");
        sugCell.AddToClassList("rot-col--sug");
        sugCell.AddToClassList("rot-sug");
        var sugBase = new Label();
        sugBase.AddToClassList("rot-sug-base");
        var sugDelta = new Label();
        sugDelta.AddToClassList("rot-sug-delta");
        sugCell.Add(sugBase);
        sugCell.Add(sugDelta);
        row.Add(sugCell);

        bool isAuto = _allAuto;
        ApplyRowValues(valLbl, sugBase, sugDelta, effective, suggested, isAuto);

        var cs = new CustomSlider(slider, fill, dragger);
        _sliders.Add((ls, p, playable, cs));
        cs.SetValueWithoutNotify(Mathf.Clamp01(effective / (float)MAX_MINUTES));

        int playerId = p.id;
        int entrySlot = ls.slot;
        cs.OnValueChanged = v =>
        {
            int minutes = Mathf.Clamp(Mathf.RoundToInt(v * MAX_MINUTES), 0, MAX_MINUTES);

            // Primera interacción en estado automático: se fija el reparto mostrado (para
            // que la BD y la pantalla digan lo mismo) y en el mismo gesto se aplica el
            // cambio del usuario. No se reconstruyen las filas para no cortar el arrastre:
            // el resto conserva el aspecto "automático" hasta la próxima visita.
            if (_allAuto) MaterializeAutoSplit();

            ls.rotation_minutes = minutes;
            DatabaseManager.Instance.SetLineupMinutes(playerId, minutes);

            int sug = SuggestedFor(FindPlayer(playerId), entrySlot == 0);
            ApplyRowValues(valLbl, sugBase, sugDelta, minutes, sug);
            UpdateSummary();
        };

        return row;
    }

    void ApplyRowValues(Label valLbl, Label sugBase, Label sugDelta, int minutes, int suggested,
                        bool isAuto = false)
    {
        valLbl.text = minutes.ToString();
        valLbl.EnableInClassList("rot-min-val--auto", isAuto);
        valLbl.EnableInClassList("rot-min-val--custom", !isAuto && minutes != suggested);

        // La sugerencia del simulador se muestra siempre (es la referencia de la fila),
        // también en el estado automático.
        sugBase.text = suggested.ToString();
        sugBase.tooltip = $"Sugerido por el simulador: {suggested} min";

        // El delta se calcula siempre (también en el reparto automático): compara los
        // minutos asignados con la sugerencia del simulador.
        int delta = minutes - suggested;
        if (delta == 0)
        {
            sugDelta.text = string.Empty;
            sugDelta.style.display = DisplayStyle.None;
            sugDelta.tooltip = null;
            sugDelta.EnableInClassList("rot-sug-delta--pos", false);
            sugDelta.EnableInClassList("rot-sug-delta--neg", false);
            return;
        }

        sugDelta.text = delta.ToString("+0;-0");
        sugDelta.style.display = DisplayStyle.Flex;
        sugDelta.EnableInClassList("rot-sug-delta--pos", delta > 0);
        sugDelta.EnableInClassList("rot-sug-delta--neg", delta < 0);
        sugDelta.tooltip = delta > 0
            ? (isAuto ? $"El reparto automático da {delta} min más que la sugerencia ({suggested} min)"
                      : $"Vas {delta} min por encima de la sugerencia ({suggested} min)")
            : (isAuto ? $"El reparto automático da {-delta} min menos que la sugerencia ({suggested} min)"
                      : $"Vas {-delta} min por debajo de la sugerencia ({suggested} min)");
    }

    Label CreateBadge(string text, bool isGL)
    {
        var b = new Label();
        b.AddToClassList("rot-badge");
        if (isGL) b.AddToClassList("rot-badge--gleague");
        b.text = text;
        return b;
    }

    // ── Resumen ─────────────────────────────────────────────

    void UpdateSummary()
    {
        if (_lineup == null) return;

        var rows = new List<(PlayerData p, LineupData ls)>();
        int total = 0;

        foreach (var ls in ActiveEntries())
        {
            var p = FindPlayer(ls.player_id);
            if (p == null) continue;
            rows.Add((p, ls));
            if (IsPlayable(p)) total += Effective(ls, p);
        }

        int remaining = TEAM_MINUTES - total;

        if (_remainingValue != null)
        {
            _remainingValue.text = remaining.ToString();
            _remainingValue.EnableInClassList("remaining-value--ok", remaining == 0);
            _remainingValue.EnableInClassList("remaining-value--over", remaining < 0);
        }

        if (_remainingHint != null)
        {
            _remainingHint.text = remaining > 0 ? "MINUTOS POR REPARTIR"
                                : remaining < 0 ? "MINUTOS EXCEDIDOS"
                                : _allAuto ? "REPARTO AUTOMÁTICO (240)"
                                : "REPARTO EXACTO (240)";
        }

        BuildPosSummary(rows);
        RefreshSliderLimits(total);
    }

    // Tope por slider: no se pueden repartir más de 240 minutos. Si el reparto ya
    // está completo (remaining == 0) o hay exceso (remaining < 0), cada fila queda
    // clavada en su valor actual y solo puede bajar; el margen de subida es el
    // sobrante actual (remaining), repartible entre todos.
    void RefreshSliderLimits(int total)
    {
        if (_sliders.Count == 0) return;

        int remaining = TEAM_MINUTES - total;

        foreach (var s in _sliders)
        {
            // Los no jugables no cuentan en el total (igual que el panel), así que no
            // se les impone tope.
            if (!s.playable)
            {
                s.slider.MaxValue = 1f;
                continue;
            }

            int mine = Effective(s.ls, s.p);
            int maxAllowed = Mathf.Clamp(mine + Mathf.Max(0, remaining), 0, MAX_MINUTES);
            s.slider.MaxValue = maxAllowed / (float)MAX_MINUTES;
        }
    }

    void BuildPosSummary(List<(PlayerData p, LineupData ls)> rows)
    {
        if (_posSummary == null) return;
        _posSummary.Clear();

        var primary = new int[PosOrder.Length];
        var secondary = new int[PosOrder.Length];

        foreach (var (p, ls) in rows)
        {
            if (!IsPlayable(p)) continue;
            int mins = Effective(ls, p);

            int i = System.Array.IndexOf(PosOrder, p.position);
            if (i >= 0) primary[i] += mins;

            if (!string.IsNullOrEmpty(p.secondary_position))
            {
                int j = System.Array.IndexOf(PosOrder, p.secondary_position);
                if (j >= 0) secondary[j] += mins;
            }
        }

        // Escala relativa: al menos lo que juega una posición (48).
        int scale = MAX_MINUTES;
        for (int i = 0; i < PosOrder.Length; i++)
            scale = Mathf.Max(scale, primary[i] + secondary[i]);

        for (int i = 0; i < PosOrder.Length; i++)
        {
            int total = primary[i] + secondary[i];

            var row = new VisualElement();
            row.AddToClassList("pos-row");
            var lbl = new Label();
            lbl.AddToClassList("pos-label");
            lbl.text = PositionCodes.GetShort(PosOrder[i]);
            row.Add(lbl);

            var track = new VisualElement();
            track.AddToClassList("pos-bar-track");
            var fill = new VisualElement();
            fill.AddToClassList("pos-bar-fill");
            fill.style.width = new StyleLength(new Length(total / (float)scale * 100f, LengthUnit.Percent));

            var prim = new VisualElement();
            prim.AddToClassList("pos-bar-primary");
            prim.style.width = new StyleLength(new Length(total > 0 ? primary[i] / (float)total * 100f : 0f, LengthUnit.Percent));

            var sec = new VisualElement();
            sec.AddToClassList("pos-bar-secondary");
            fill.Add(prim);
            fill.Add(sec);
            track.Add(fill);
            row.Add(track);

            var val = new Label();
            val.AddToClassList("pos-value");
            val.text = total.ToString();
            val.tooltip = $"Principal {primary[i]} · Secundaria {secondary[i]}";
            row.Add(val);

            _posSummary.Add(row);
        }

        // Leyenda
        var legend = new VisualElement();
        legend.AddToClassList("pos-legend");
        var sw1 = new VisualElement();
        sw1.AddToClassList("pos-legend-swatch");
        sw1.AddToClassList("pos-legend-swatch--primary");
        var l1 = new Label();
        l1.AddToClassList("pos-legend-label");
        l1.text = "Principal";

        var sw2 = new VisualElement();
        sw2.AddToClassList("pos-legend-swatch");
        sw2.AddToClassList("pos-legend-swatch--secondary");
        var l2 = new Label();
        l2.AddToClassList("pos-legend-label");
        l2.text = "Secundaria";

        legend.Add(sw1);
        legend.Add(l1);
        legend.Add(sw2);
        legend.Add(l2);
        _posSummary.Add(legend);
    }

    // ── Acciones ────────────────────────────────────────────

    void ApplyAuto()
    {
        if (_lineup == null) return;

        var entries = ActiveEntries()
            .Where(ls => { var p = FindPlayer(ls.player_id); return p != null && IsPlayable(p); })
            .ToList();
        if (entries.Count == 0) return;

        // Fuente única del reparto (misma que el aviso del Dashboard).
        DatabaseManager.Instance.AutoAssignRotationMinutes(_myTeam.id, _players);

        _allAuto = false;
        _autoSplit.Clear();
        _lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        BuildRows();
        UpdateSummary();
    }

    // RESET = todos a 0 minutos (reparto desde cero). 0 es un valor real, no el
    // centinela de automático (-1), que solo aparece en una convocatoria sin tocar.
    void ApplyReset()
    {
        if (_lineup == null) return;

        foreach (var ls in ActiveEntries())
        {
            if (ls.rotation_minutes == 0) continue;
            ls.rotation_minutes = 0;
            DatabaseManager.Instance.SetLineupMinutes(ls.player_id, 0);
        }

        // RESET deja todo a 0 = el simulador vuelve a repartir automáticamente; la pantalla
        // lo muestra con el reparto de 240 (no como "0 minutos" para todos).
        ComputeAutoSplit();
        BuildRows();
        UpdateSummary();
    }
}
