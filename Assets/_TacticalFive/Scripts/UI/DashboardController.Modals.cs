using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Modales de rotación, quinteto, IR, deadline y cese.
public partial class DashboardController
{
    /// <summary>
    /// Estado del reparto de minutos de la rotación del equipo del usuario (solo convocados
    /// jugables). `IsComplete` = sin jugables, todo en automático, o 240 exactos y válidos.
    /// </summary>
    RotationStatus GetRotationStatus()
    {
        if (_myTeam == null) return new RotationStatus();

        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new RotationStatus();

        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var byId = players.ToDictionary(p => p.id);

        var slots = new List<RotationSlot>();
        foreach (var l in lineup)
        {
            if (l.slot != 0 && l.slot != 1) continue;
            if (!byId.TryGetValue(l.player_id, out var p) || p == null) continue;
            slots.Add(new RotationSlot
            {
                playerId = p.id,
                minutes = l.rotation_minutes,
                playable = p.injury_days == 0 && p.g_league_assigned != 1,
                suggestion = GameSimulator.GetSuggestedMinutes(p, l.slot == 0)
            });
        }

        return RotationPlanner.Evaluate(slots);
    }

    /// <summary>Aviso de rotación incompleta: minutos sin asignar, excedidos o inválidos.</summary>
    void ShowRotationModal(RotationStatus rot)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        var gold = new Color32(212, 160, 23, 255);
        box.style.borderTopColor = new StyleColor(gold);
        box.style.borderBottomColor = new StyleColor(gold);
        box.style.borderLeftColor = new StyleColor(gold);
        box.style.borderRightColor = new StyleColor(gold);
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.rotacion_incompleta"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        string detail = rot.hasInvalid
            ? "Hay minutos de rotación con valores no válidos."
            : rot.Missing > 0
                ? "Faltan minutos por asignar en la rotación."
                : $"Tienes {rot.total} minutos asignados: te pasas por {-rot.Missing}.";
        var text = new Label(detail + "\nAsigna todos los minutos antes de simular o usa el reparto automático.");
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var goBtn = new Button();
        goBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_a_rotacion");
        goBtn.AddToClassList("btn-modal-confirm");
        goBtn.AddToClassList("btn-modal-confirm--gold");
        goBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _rotationModalGoToRotation = true;
            _rotationModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(goBtn);

        var autoBtn = new Button();
        autoBtn.text = LocalizationService.T("ui.code.dashboard_controller.asignar_automaticamente");
        autoBtn.AddToClassList("btn-modal-confirm");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            DatabaseManager.Instance.AutoAssignRotationMinutes(_myTeam.id, allPlayers);
            _rotationModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(autoBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(goBtn);
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        }
    }

    List<int> GetEmptyStarterSlots()
    {
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new List<int>();

        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);

        var filledSlots = new HashSet<int>();
        foreach (var l in lineup)
            if (l.slot == 0 && playerMap.ContainsKey(l.player_id))
                filledSlots.Add(l.slot_index);

        var emptySlots = new List<int>();
        for (int i = 0; i < 5; i++)
            if (!filledSlots.Contains(i))
                emptySlots.Add(i);
        return emptySlots;
    }

    List<(LineupData li, PlayerData p)> GetInjuredActiveLineupPlayers()
    {
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new List<(LineupData, PlayerData)>();

        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);

        return lineup
            .Where(l => l.slot <= 1)
            .Select(l => (li: l, p: playerMap.GetValueOrDefault(l.player_id)))
            .Where(x => x.p != null && x.p.injury_days > 0)
            .ToList();
    }

    bool IsMyTeamBackToBack(int gameDay)
    {
        if (_myTeam == null) return false;
        return DatabaseManager.Instance.Db.Table<GameData>()
            .Any(g => (g.home_team_id == _myTeam.id || g.away_team_id == _myTeam.id)
                   && g.is_played == 1 && g.game_day == gameDay - 1);
    }

    List<(LineupData li, PlayerData p)> GetTiredActivePlayers()
    {
        var lineup = DatabaseManager.Instance.GetTeamLineup(_myTeam.id);
        if (lineup.Count == 0) return new List<(LineupData, PlayerData)>();

        var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
        var playerMap = allPlayers.ToDictionary(p => p.id);

        return lineup
            .Where(l => l.slot <= 1)
            .Select(l => (li: l, p: playerMap.GetValueOrDefault(l.player_id)))
            .Where(x => x.p != null && x.p.injury_days == 0)
            .Where(x => (x.li.slot == 0 && x.p.fisico < 55) || (x.li.slot == 1 && x.p.fisico < 30))
            .OrderBy(x => x.li.slot)
            .ThenBy(x => x.p.fisico)
            .ToList();
    }

    void ShowEmptyLineupModal(List<int> emptySlots)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");

        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.quinteto_incompleto"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Empty position cards ──
        var playerList = new VisualElement();
        playerList.AddToClassList("injury-modal-player-list");

        var posNames = new[] { "Base", "Escolta", "Alero", "Ala-Pívot", "Pívot" };
        var posShort = new[] { "B", "E", "A", "AP", "P" };

        foreach (var si in emptySlots)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-modal-player-card");
            card.AddToClassList("empty-modal-card");

            // Slot icon placeholder
            var slotIcon = new VisualElement();
            slotIcon.AddToClassList("empty-modal-slot-icon");

            var iconText = new Label(posShort[si]);
            iconText.AddToClassList("empty-modal-slot-icon-text");
            slotIcon.Add(iconText);
            card.Add(slotIcon);

            // Info
            var info = new VisualElement();
            info.AddToClassList("trade-modal-player-info");

            var nameLbl = new Label(posNames[si]);
            nameLbl.AddToClassList("trade-modal-player-name");
            nameLbl.AddToClassList("injury-modal-text-lg");
            info.Add(nameLbl);

            var metaLbl = new Label(LocalizationService.T("ui.code.dashboard_controller.quinteto_titular_slot_vacio"));
            metaLbl.AddToClassList("trade-modal-player-meta");
            metaLbl.AddToClassList("injury-modal-text-lg");
            info.Add(metaLbl);

            // Badge
            var badge = new VisualElement();
            badge.AddToClassList("empty-modal-badge");

            var badgeText = new Label(LocalizationService.T("ui.code.dashboard_controller.sin_asignar"));
            badgeText.AddToClassList("empty-modal-badge-text");
            badge.Add(badgeText);

            info.Add(badge);

            card.Add(info);

            playerList.Add(card);
        }
        body.Add(playerList);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var manualBtn = new Button();
        manualBtn.text = LocalizationService.T("ui.code.dashboard_controller.revisar_manualmente");
        manualBtn.AddToClassList("btn-modal-confirm");
        manualBtn.AddToClassList("btn-modal-confirm--gold");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _emptyLineupGoToQuinteto = true;
            _emptyLineupModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(manualBtn);

        var autoBtn = new Button();
        autoBtn.text = LocalizationService.T("ui.code.dashboard_controller.auto_rellenar");
        autoBtn.AddToClassList("btn-modal-cancel");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var injuredIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0 || p.g_league_assigned == 1)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, injuredIds);
            _emptyLineupModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(autoBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(manualBtn);
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        }
    }

    void ShowInjuredLineupModal(List<(LineupData li, PlayerData p)> injured)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 800;
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");

        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.jugadores_lesionados"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Injured players ──
        var playerList = new VisualElement();
        playerList.AddToClassList("injury-modal-player-list");

        // Iconos de la acción "tratar lesión" (misma imagen que el editor de contenido).
        var injectionIcon = Resources.Load<Texture2D>("Icons/inyeccion");
        var treatedIcon = Resources.Load<Texture2D>("Icons/boton-v-64px");

        foreach (var (li, p) in injured)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-modal-player-card");
            card.AddToClassList("injury-modal-card");

            // Photo
            var photo = new VisualElement();
            photo.AddToClassList("trade-modal-player-photo");
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                photo.style.backgroundImage = new StyleBackground(tex);
            card.Add(photo);

            // Info
            var info = new VisualElement();
            info.AddToClassList("trade-modal-player-info");

            var nameLbl = new Label($"{p.first_name} {p.last_name}");
            nameLbl.AddToClassList("trade-modal-player-name");
            nameLbl.AddToClassList("injury-modal-text-lg");
            info.Add(nameLbl);

            string stars = p.overall >= 85 ? " ★★★" : p.overall >= 75 ? " ★★" : p.overall >= 65 ? " ★" : "";
            var metaLbl = new Label($"{PositionCodes.GetShort(p.position)} | {p.age} años{stars}");
            metaLbl.AddToClassList("trade-modal-player-meta");
            metaLbl.AddToClassList("injury-modal-text-lg");
            info.Add(metaLbl);

            // Injury badge
            var injuryBadge = new VisualElement();
            injuryBadge.AddToClassList("injury-modal-badge");

            var injuryTypeLbl = new Label(p.injury_type);
            injuryTypeLbl.AddToClassList("injury-modal-badge-text");
            injuryBadge.Add(injuryTypeLbl);

            var injuryDaysLbl = new Label($"{p.injury_days} días");
            injuryDaysLbl.AddToClassList("injury-modal-badge-days");
            injuryBadge.Add(injuryDaysLbl);

            info.Add(injuryBadge);

            // Slot label
            string slotLabel = li.slot == 0 ? "TITULAR" : "BANQUILLO";
            var slotLbl = new Label(slotLabel);
            slotLbl.AddToClassList("injury-modal-slot");
            if (li.slot == 0)
                slotLbl.AddToClassList("injury-modal-slot--starter");
            info.Add(slotLbl);

            card.Add(info);

            // OVR column
            var ovrCol = new VisualElement();
            ovrCol.AddToClassList("trade-modal-player-ovr-col");

            var ovrLbl = new Label(p.overall.ToString());
            ovrLbl.AddToClassList("trade-modal-player-ovr");
            if (p.overall >= 80)
                ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
            else if (p.overall >= 60)
                ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
            else
                ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
            ovrCol.Add(ovrLbl);

            var ovrSub = new Label(LocalizationService.T("ui.code.dashboard_controller.med"));
            ovrSub.AddToClassList("trade-modal-player-ovr-label");
            ovrCol.Add(ovrSub);

            card.Add(ovrCol);

            // Botón de tratamiento: icono de inyección (misma acción; al tratar cambia al
            // icono de "hecho" y se actualiza el valor de días EN ESTE MISMO modal).
            var treatBtn = new Button();
            bool alreadyTreated = p.treated == 1;
            var medico = DatabaseManager.Instance.GetEmployeesByTeam(_myTeam.id)
                .FirstOrDefault(e => e.position == "MEDICO");
            bool hasMedico = medico != null;

            // La caja del botón conserva su tamaño; dentro va la imagen a 40x40.
            var treatIcon = new VisualElement();
            treatIcon.AddToClassList("injury-modal-treat-icon");
            treatBtn.Add(treatIcon);

            void PaintTreatIcon(Texture2D tex, bool done, bool enabled)
            {
                treatBtn.text = "";
                if (tex != null) treatIcon.style.backgroundImage = new StyleBackground(tex);
                treatBtn.EnableInClassList("injury-modal-treat-btn--done", done);
                treatBtn.SetEnabled(enabled);
                treatBtn.tooltip = done ? "Lesión ya tratada"
                               : enabled ? "Tratar lesión (reduce los días de baja)"
                               : "Sin médico en el equipo: no se puede tratar";
            }

            treatBtn.AddToClassList("injury-modal-treat-btn");
            treatBtn.AddToClassList("injury-modal-treat-btn--icon");
            CursorManager.Instance?.RegisterHandCursor(treatBtn);
            PaintTreatIcon(alreadyTreated ? treatedIcon : injectionIcon,
                           done: alreadyTreated,
                           enabled: !alreadyTreated && hasMedico);

            if (!alreadyTreated && hasMedico)
            {
                treatBtn.RegisterCallback<ClickEvent>(_ =>
                {
                    PlayClick();
                    float pct = medico.reputation switch
                    {
                        5 => UnityEngine.Random.Range(0.25f, 0.40f),
                        4 => UnityEngine.Random.Range(0.20f, 0.32f),
                        3 => UnityEngine.Random.Range(0.15f, 0.25f),
                        2 => UnityEngine.Random.Range(0.10f, 0.18f),
                        _ => UnityEngine.Random.Range(0.05f, 0.12f),
                    };
                    int newDays = Mathf.CeilToInt(p.injury_days * (1f - pct));
                    p.injury_days = Mathf.Clamp(newDays, 1, p.injury_days);
                    p.treated = 1;
                    DatabaseManager.Instance.UpdatePlayer(p);

                    // Refrescar el valor mostrado en el propio modal.
                    injuryDaysLbl.text = $"{p.injury_days} días";
                    injuryBadge.tooltip = $"Tratada: quedan {p.injury_days} días de baja";
                    PaintTreatIcon(treatedIcon, done: true, enabled: false);
                });
            }
            card.Add(treatBtn);

            playerList.Add(card);
        }
        body.Add(playerList);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var manualBtn = new Button();
        manualBtn.text = LocalizationService.T("ui.code.dashboard_controller.cambiar_manualmente");
        manualBtn.AddToClassList("btn-modal-confirm");
        manualBtn.AddToClassList("btn-modal-confirm--gold");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _injuredModalGoToQuinteto = true;
            _injuredModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(manualBtn);

        var autoBtn = new Button();
        autoBtn.text = LocalizationService.T("ui.code.dashboard_controller.reemplazar_automaticamente");
        autoBtn.AddToClassList("btn-modal-cancel");
        autoBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var inactiveIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0 || p.g_league_assigned == 1)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, inactiveIds);
            _injuredModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(autoBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(manualBtn);
            CursorManager.Instance.RegisterHandCursor(autoBtn);
        }
    }

    void ShowLoadManagementModal(List<(LineupData li, PlayerData p)> tiredPlayers, bool isB2B)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.width = 800;
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.gestion_de_descanso"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        if (isB2B)
        {
            var b2bLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.back_to_back_tu_equipo_jugo_ayer_conside_3b473c2e"));
            b2bLabel.AddToClassList("fired-modal-text");
            b2bLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(b2bLabel);
        }

        if (tiredPlayers.Count == 0 && isB2B)
        {
            var infoLabel = new Label(LocalizationService.T("ui.code.dashboard_controller.ningun_jugador_esta_por_debajo_del_umbra_65b9be70"));
            infoLabel.AddToClassList("fired-modal-text");
            infoLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(infoLabel);
        }

        if (tiredPlayers.Count > 0)
        {
            var text = new Label(LocalizationService.T("ui.code.dashboard_controller.los_siguientes_jugadores_estan_cansados_4d304b95"));
            text.AddToClassList("fired-modal-text");
            text.style.unityTextAlign = TextAnchor.MiddleLeft;
            body.Add(text);

            var list = new VisualElement();
            list.AddToClassList("injured-modal-list");
            foreach (var (li, p) in tiredPlayers)
            {
                var slotLabel = li.slot == 0 ? "TITULAR" : "BANQUILLO";
                Color fisicoColor = p.fisico >= 60 ? new Color32(39, 174, 96, 255)
                    : p.fisico >= 30 ? new Color32(212, 160, 23, 255)
                    : new Color32(192, 57, 43, 255);
                var row = new Label($"{p.first_name} {p.last_name} — {PositionCodes.GetShort(p.position)} ({slotLabel}) — Físico: {p.fisico}");
                row.AddToClassList("injured-modal-player-row");
                row.style.color = new StyleColor(fisicoColor);
                list.Add(row);
            }
            body.Add(list);
        }

        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        if (tiredPlayers.Count > 0)
        {
            var restBtn = new Button();
            restBtn.text = LocalizationService.T("ui.code.dashboard_controller.descansar_cansados");
            restBtn.AddToClassList("btn-modal-confirm");
            restBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
                var tiredIds = new HashSet<int>(tiredPlayers.Select(x => x.p.id));
                var injuredIds = new HashSet<int>(allPlayers
                    .Where(p => p.injury_days > 0)
                    .Select(p => p.id));
                tiredIds.UnionWith(injuredIds);
                DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, tiredIds);
                _loadMgmtModalResolved = true;
                _firedOverlay.style.display = DisplayStyle.None;
            });
            actions.Add(restBtn);
        }

        var manualBtn = new Button();
        manualBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_al_quinteto");
        manualBtn.AddToClassList("btn-modal-confirm");
        manualBtn.AddToClassList("btn-modal-confirm--gold");
        manualBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _loadMgmtModalGoToQuinteto = true;
            _loadMgmtModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(manualBtn);

        var continueBtn = new Button();
        continueBtn.text = LocalizationService.T("ui.code.dashboard_controller.continuar_sin_cambios");
        continueBtn.AddToClassList("btn-modal-cancel");
        continueBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _loadMgmtModalResolved = true;
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(continueBtn);

        box.Add(actions);
    }

    void ShowPendingRecoveryModal()
    {
        if (_pendingRecoveredIds.Count == 0) return;

        var players = new List<PlayerData>();
        foreach (int id in _pendingRecoveredIds)
        {
            var p = DatabaseManager.Instance.GetPlayerById(id);
            if (p != null) players.Add(p);
        }
        if (players.Count == 0)
        {
            _pendingRecoveredIds.Clear();
            return;
        }

        _pendingRecoveredIds.Clear();
        ShowRecoveredModal(players);
    }

    void ShowRecoveredModal(List<PlayerData> recovered)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderRightColor = new StyleColor(new Color32(39, 174, 96, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--green");

        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.jugador_es_recuperado_s"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(39, 174, 96, 255));
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // ── Player cards ──
        var playerList = new VisualElement();
        playerList.AddToClassList("injury-modal-player-list");

        foreach (var p in recovered)
        {
            var card = new VisualElement();
            card.AddToClassList("trade-modal-player-card");
            card.AddToClassList("recovery-modal-card");

            // Photo
            var photo = new VisualElement();
            photo.AddToClassList("trade-modal-player-photo");
            Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
                photo.style.backgroundImage = new StyleBackground(tex);
            card.Add(photo);

            // Info
            var info = new VisualElement();
            info.AddToClassList("trade-modal-player-info");

            var nameLbl = new Label($"{p.first_name} {p.last_name}");
            nameLbl.AddToClassList("trade-modal-player-name");
            nameLbl.AddToClassList("injury-modal-text-lg");
            info.Add(nameLbl);

            string stars = p.overall >= 85 ? " ★★★" : p.overall >= 75 ? " ★★" : p.overall >= 65 ? " ★" : "";
            var metaLbl = new Label($"{PositionCodes.GetShort(p.position)} | {p.age} años{stars}");
            metaLbl.AddToClassList("trade-modal-player-meta");
            metaLbl.AddToClassList("injury-modal-text-lg");
            info.Add(metaLbl);

            // Recovery badge
            var badge = new VisualElement();
            badge.AddToClassList("recovery-modal-badge");

            var badgeText = new Label(LocalizationService.T("ui.code.dashboard_controller.recuperado_disponible"));
            badgeText.AddToClassList("recovery-modal-badge-text");
            badge.Add(badgeText);

            info.Add(badge);

            card.Add(info);

            // OVR column
            var ovrCol = new VisualElement();
            ovrCol.AddToClassList("trade-modal-player-ovr-col");

            var ovrLbl = new Label(p.overall.ToString());
            ovrLbl.AddToClassList("trade-modal-player-ovr");
            if (p.overall >= 80)
                ovrLbl.style.color = new StyleColor(new Color32(39, 174, 96, 255));
            else if (p.overall >= 60)
                ovrLbl.style.color = new StyleColor(new Color32(212, 160, 23, 255));
            else
                ovrLbl.style.color = new StyleColor(new Color32(192, 57, 43, 255));
            ovrCol.Add(ovrLbl);

            var ovrSub = new Label(LocalizationService.T("ui.code.dashboard_controller.med"));
            ovrSub.AddToClassList("trade-modal-player-ovr-label");
            ovrCol.Add(ovrSub);

            card.Add(ovrCol);

            playerList.Add(card);
        }
        body.Add(playerList);
        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var goBtn = new Button();
        goBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_a_quinteto");
        goBtn.AddToClassList("btn-modal-confirm");
        goBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimRunning)
            {
                _recoveryModalGoToQuinteto = true;
                _recoveryModalResolved = true;
            }
            else
            {
                ScreenManager.Instance.GoTo(GameScreen.Quinteto);
            }
        });
        actions.Add(goBtn);

        var closeBtn = new Button();
        closeBtn.text = LocalizationService.T("ui.code.dashboard_controller.rehacer_convocatoria");
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            var allPlayers = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id);
            var inactiveIds = new HashSet<int>(allPlayers
                .Where(p => p.injury_days > 0 || p.g_league_assigned == 1)
                .Select(p => p.id));
            DatabaseManager.Instance.AutoSeedLineup(_myTeam.id, allPlayers, inactiveIds);
            _firedOverlay.style.display = DisplayStyle.None;
            if (_fastSimRunning)
            {
                _recoveryModalGoToQuinteto = false;
                _recoveryModalResolved = true;
            }
        });
        actions.Add(closeBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(goBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void ShowPendingIRReleaseModal()
    {
        if (_pendingIRReleaseCount <= 0)
        {
            _irReleaseModalResolved = true;
            return;
        }
        _pendingIRReleaseIds.Clear();
        if (DatabaseManager.Instance.GetRosterCount(_myTeam.id) <= TradeHelper.MAX_STANDARD_ROSTER)
        {
            _pendingIRReleaseCount = 0;
            _irReleaseModalResolved = true;
            return;
        }

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(39, 174, 96, 255));
        box.style.borderRightColor = new StyleColor(new Color32(39, 174, 96, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--green");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.plantilla_completa_tras_recuperacion"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(39, 174, 96, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        var text = new Label(LocalizationService.T("ui.dashboard.ir_release_warning", TradeHelper.MAX_STANDARD_ROSTER));
        text.AddToClassList("fired-modal-text");
        body.Add(text);

        var list = new VisualElement();
        list.AddToClassList("injured-modal-list");
        var players = DatabaseManager.Instance.GetPlayersByTeam(_myTeam.id)
            .Where(p => p.is_on_ir == 0 && p.is_two_way == 0)
            .OrderByDescending(p => p.GetCalculatedAverage())
            .ToList();
        foreach (var p in players)
        {
            var row = new VisualElement();
            row.AddToClassList("injured-modal-player-row");

            var nameLbl = new Label($"{p.first_name} {p.last_name} — {PositionCodes.GetShort(p.position)} · OVR {p.GetCalculatedAverage()} · ${p.salary:N0}");
            nameLbl.style.flexGrow = 1;
            row.Add(nameLbl);

            var releaseBtn = new Button();
            releaseBtn.text = LocalizationService.T("ui.code.dashboard_controller.liberar");
            releaseBtn.AddToClassList("injured-modal-btn");
            releaseBtn.style.backgroundColor = new StyleColor(new Color32(192, 57, 43, 255));
            releaseBtn.RegisterCallback<ClickEvent>(_ =>
            {
                PlayClick();
                ReleasePlayerForIR(p);
            });
            if (CursorManager.Instance != null)
                CursorManager.Instance.RegisterHandCursor(releaseBtn);
            row.Add(releaseBtn);

            list.Add(row);
        }
        body.Add(list);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var cancelBtn = new Button();
        cancelBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_a_plantilla");
        cancelBtn.AddToClassList("btn-modal-confirm");
        cancelBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            _irReleaseModalResolved = true;
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        if (CursorManager.Instance != null)
            CursorManager.Instance.RegisterHandCursor(cancelBtn);
        actions.Add(cancelBtn);
        box.Add(actions);
    }

    void ReleasePlayerForIR(PlayerData player)
    {
        long remainingSalary = player.salary * player.contract_years;
        int stretchYears = Mathf.Max(1, player.contract_years * 2);
        long annualPayment = remainingSalary / stretchYears;
        int currentDay = _season?.current_game_day ?? 0;
        string playerName = $"{player.first_name} {player.last_name}";
        string now = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        player.team_id = 0;
        player.last_team_id = 0;
        player.seasons_with_team = 0;
        player.is_on_ir = 0;
        player.g_league_assigned = 0;
        DatabaseManager.Instance.UpdatePlayer(player);

        long remainder = remainingSalary;
        for (int y = 0; y < stretchYears; y++)
        {
            long payment = (y == stretchYears - 1) ? remainder : annualPayment;
            remainder -= payment;
            DatabaseManager.Instance.AddFinanceRecord(new FinanceRecord
            {
                team_id = _myTeam.id,
                season_id = _season?.id ?? 0,
                record_type = FinanceRecord.TYPE_BUYOUT,
                game_day = currentDay,
                amount = payment,
                created_at = now
            });
        }

        DatabaseManager.Instance.AddMessage(new MessageData
        {
            manager_id = _manager.id,
            sender_type = 1,
            sender_id = 0,
            title = "Rescisión de contrato (buyout)",
            body = NewsTexts.Buyout(playerName, _myTeam.name) +
                   $"\n\nSalario restante: ${remainingSalary:N0}\nPago progresivo: ${annualPayment:N0} durante {stretchYears} años.",
            game_day = currentDay,
            game_date = now,
            created_at = now,
            date_sent = now,
            is_read = 0,
            category = NewsCategory.TRASPASOS,
            news_type = NewsType.BUYOUT
        });

        _pendingIRReleaseCount--;
        if (DatabaseManager.Instance.GetRosterCount(_myTeam.id) <= TradeHelper.MAX_STANDARD_ROSTER || _pendingIRReleaseCount <= 0)
        {
            _firedOverlay.style.display = DisplayStyle.None;
            _irReleaseModalResolved = true;
            return;
        }
        ShowPendingIRReleaseModal();
    }

    void CheckTradeDeadlineModal()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return;
        if (date.Month == 2 && date.Day == 1 && date.Year == _season.year_end)
        {
            // Check if the message was already sent this season to avoid repeating the modal
            var existing = DatabaseManager.Instance.Db.Table<MessageData>()
                .FirstOrDefault(m => m.manager_id == _manager.id
                                  && m.title == "Última semana de traspasos"
                                  && m.game_date == _season.current_date);
            if (existing != null) return;

            ShowTradeDeadlineModal();
        }
    }

    void ShowTradeDeadlineModal(bool fastSim = false)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.cierre_de_mercado"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(LocalizationService.T("ui.code.dashboard_controller.el_periodo_de_traspasos_finaliza_el_8_de_8b2850d7"));
        text.AddToClassList("fired-modal-text");
        text.style.marginTop = 15;
        text.style.marginBottom = 15;
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var marketBtn = new Button();
        marketBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_al_mercado");
        marketBtn.AddToClassList("btn-modal-confirm");
        marketBtn.AddToClassList("btn-modal-confirm--gold");
        marketBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        actions.Add(marketBtn);

        var closeBtn = new Button();
        closeBtn.text = fastSim ? "SEGUIR SIMULANDO" : "CERRAR";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _firedOverlay.style.display = DisplayStyle.None;
        });
        actions.Add(closeBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(marketBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void ShowDeadlineDayModal(bool fastSim = false)
    {
        _deadlineDayActive = true;
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        // ── Header ──
        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.cierre_de_mercado"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        // ── Body ──
        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        // Operations list
        var pendingTrades = DatabaseManager.Instance.GetPendingTradeOffers(_manager.id) ?? new List<TradeOfferData>();
        var pendingSATIds = GetPendingSATIds();
        var maturedOffers = DatabaseManager.Instance.GetMaturedUnprocessedOffers(_manager.id, _season.current_game_day) ?? new List<OfferData>();

        var opList = new VisualElement();
        opList.AddToClassList("deadline-modal-op-list");

        if (pendingTrades.Count == 0 && pendingSATIds.Count == 0 && maturedOffers.Count == 0)
        {
            var emptyLbl = new Label(LocalizationService.T("ui.code.dashboard_controller.no_tienes_operaciones_pendientes_ultima_b8e2ca87"));
            emptyLbl.AddToClassList("fired-modal-text");
            emptyLbl.style.marginTop = 15;
            emptyLbl.style.marginBottom = 15;
            body.Add(emptyLbl);
            opList = null;
        }
        else
        {
            var allTeams = DatabaseManager.Instance.GetAllTeams();
            var teamDict = allTeams.ToDictionary(t => t.id);

            // ── Trade offers ──
            foreach (var offer in pendingTrades)
            {
                var card = new VisualElement();
                card.AddToClassList("trade-modal-player-card");
                card.AddToClassList("deadline-modal-card");

                var photo = new VisualElement();
                photo.AddToClassList("trade-modal-player-photo");
                photo.AddToClassList("deadline-modal-icon");
                var iconLbl = new Label("\U0001F504");
                iconLbl.AddToClassList("deadline-modal-icon-text");
                photo.Add(iconLbl);
                card.Add(photo);

                var info = new VisualElement();
                info.AddToClassList("trade-modal-player-info");

                string teamName = teamDict.TryGetValue(offer.team_id_from, out var t) ? t.name : "Desconocido";
                var nameLbl = new Label(teamName.ToUpper());
                nameLbl.AddToClassList("trade-modal-player-name");
                info.Add(nameLbl);

                var wantedIds = offer.GetWantedPlayerIds();
                var offeredIds = offer.GetOfferedPlayerIds();
                var wantedPicks = offer.GetWantedPickIds();
                var offeredPicks = offer.GetOfferedPickIds();

                var wantedNames = wantedIds.Select(id => DatabaseManager.Instance.GetPlayerById(id))
                    .Where(p => p != null).Select(p => $"{p.first_name} {p.last_name}").ToList();
                var offeredNames = offeredIds.Select(id => DatabaseManager.Instance.GetPlayerById(id))
                    .Where(p => p != null).Select(p => $"{p.first_name} {p.last_name}").ToList();

                string detail = "";
                if (wantedNames.Count > 0) detail += $"Te pide: {string.Join(", ", wantedNames)}";
                if (wantedPicks.Count > 0) detail += (detail.Length > 0 ? "\n" : "") + $"Te pide: {wantedPicks.Count} pick(s)";
                if (offeredNames.Count > 0) detail += (detail.Length > 0 ? "\n" : "") + $"Te ofrece: {string.Join(", ", offeredNames)}";
                if (offeredPicks.Count > 0) detail += (detail.Length > 0 ? "\n" : "") + $"Te ofrece: {offeredPicks.Count} pick(s)";

                var metaLbl = new Label(detail);
                metaLbl.AddToClassList("trade-modal-player-meta");
                metaLbl.style.whiteSpace = WhiteSpace.Normal;
                info.Add(metaLbl);
                card.Add(info);

                var badge = new VisualElement();
                badge.AddToClassList("deadline-modal-badge");
                badge.AddToClassList("deadline-modal-badge--trade");
                var badgeText = new Label(LocalizationService.T("ui.code.dashboard_controller.traspaso"));
                badgeText.AddToClassList("deadline-modal-badge-text");
                badge.Add(badgeText);
                card.Add(badge);

                opList.Add(card);
            }

            // ── Matured FA offers ──
            foreach (var offer in maturedOffers)
            {
                bool isSAT = pendingSATIds.Contains(offer.player_id);

                var card = new VisualElement();
                card.AddToClassList("trade-modal-player-card");
                card.AddToClassList("deadline-modal-card");

                var photo = new VisualElement();
                photo.AddToClassList("trade-modal-player-photo");
                photo.AddToClassList("deadline-modal-icon");
                var iconLbl = new Label(isSAT ? "\U0001F91D" : "\U0001F4CB");
                iconLbl.AddToClassList("deadline-modal-icon-text");
                photo.Add(iconLbl);
                card.Add(photo);

                var info = new VisualElement();
                info.AddToClassList("trade-modal-player-info");

                var player = DatabaseManager.Instance.GetPlayerById(offer.player_id);
                string playerName = player != null ? $"{player.first_name} {player.last_name}" : $"Jugador #{offer.player_id}";
                var nameLbl = new Label(playerName.ToUpper());
                nameLbl.AddToClassList("trade-modal-player-name");
                info.Add(nameLbl);

                string salaryStr = offer.offer_salary >= 1_000_000
                    ? $"${offer.offer_salary / 1_000_000f:F1}M"
                    : $"${offer.offer_salary / 1_000f:N0}K";
                string optionStr = "";
                if (offer.has_team_option == 1) optionStr = " (TO último año)";
                else if (offer.has_player_option == 1) optionStr = " (PO último año)";
                string twoWayStr = offer.is_two_way == 1 ? " · TWO-WAY" : "";
                var metaLbl = new Label($"{salaryStr} × {offer.offer_years} años{optionStr}{twoWayStr}");
                metaLbl.AddToClassList("trade-modal-player-meta");
                info.Add(metaLbl);
                card.Add(info);

                var badge = new VisualElement();
                badge.AddToClassList("deadline-modal-badge");
                badge.AddToClassList(isSAT ? "deadline-modal-badge--sat" : "deadline-modal-badge--fa");
                var badgeText = new Label(isSAT ? "SIGN & TRADE" : "OFERTA FA");
                badgeText.AddToClassList("deadline-modal-badge-text");
                badge.Add(badgeText);
                card.Add(badge);

                opList.Add(card);
            }
        }
        if (opList != null)
        {
            body.Add(opList);

            var hint = new Label(LocalizationService.T("ui.code.dashboard_controller.ultima_oportunidad_para_realizar_traspas_73671f1d"));
            hint.AddToClassList("deadline-modal-hint");
            body.Add(hint);
        }

        box.Add(body);

        // ── Buttons ──
        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var marketBtn = new Button();
        marketBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_al_mercado");
        marketBtn.AddToClassList("btn-modal-confirm");
        marketBtn.AddToClassList("btn-modal-confirm--gold");
        marketBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _deadlineDayActive = false;
            _firedOverlay.style.display = DisplayStyle.None;
            ScreenManager.Instance.GoTo(GameScreen.Market);
        });
        actions.Add(marketBtn);

        var closeBtn = new Button();
        closeBtn.text = fastSim ? "SEGUIR SIMULANDO" : "CERRAR";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _deadlineDayActive = false;
            _firedOverlay.style.display = DisplayStyle.None;
            RefreshActionButton();
        });
        actions.Add(closeBtn);
        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(marketBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    void ShowRenewalDeadlineModal(bool fastSim = false)
    {
        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(212, 160, 23, 255));
        box.style.borderRightColor = new StyleColor(new Color32(212, 160, 23, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        header.AddToClassList("fired-modal-header--warning");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.fecha_limite_de_renovaciones"));
        title.AddToClassList("fired-modal-title");
        title.style.color = new StyleColor(new Color32(212, 160, 23, 255));
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");

        var text = new Label(LocalizationService.T("ui.code.dashboard_controller.hoy_12_de_febrero_es_el_ultimo_dia_para_75d7b3e8"));
        text.AddToClassList("fired-modal-text");
        body.Add(text);

        var hint = new Label(LocalizationService.T("ui.code.dashboard_controller.si_no_renuevas_a_tiempo_tus_jugadores_pa_36e0d825"));
        hint.AddToClassList("fired-modal-text");
        hint.style.marginTop = 10;
        body.Add(hint);

        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");

        var rosterBtn = new Button();
        rosterBtn.text = LocalizationService.T("ui.code.dashboard_controller.ir_a_jugadores");
        rosterBtn.AddToClassList("btn-modal-confirm");
        rosterBtn.AddToClassList("btn-modal-confirm--gold");
        rosterBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _renewalDeadlineModalShown = true;
            _firedOverlay.style.display = DisplayStyle.None;
            _renewalDeadlineResolved = true;
            ScreenManager.Instance.GoTo(GameScreen.Roster);
        });
        actions.Add(rosterBtn);

        var closeBtn = new Button();
        closeBtn.text = fastSim ? "SEGUIR SIMULANDO" : "CERRAR";
        closeBtn.AddToClassList("btn-modal-cancel");
        closeBtn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            _renewalDeadlineModalShown = true;
            _firedOverlay.style.display = DisplayStyle.None;
            _renewalDeadlineResolved = true;
        });
        actions.Add(closeBtn);

        box.Add(actions);

        if (CursorManager.Instance != null)
        {
            CursorManager.Instance.RegisterHandCursor(rosterBtn);
            CursorManager.Instance.RegisterHandCursor(closeBtn);
        }
    }

    bool IsFeb7OfYearEnd()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date.Month == 2 && date.Day == 7 && date.Year == _season.year_end;
    }

    bool IsFeb12OfYearEnd()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date.Month == 2 && date.Day == 12 && date.Year == _season.year_end;
    }

    bool IsRenewalDeadlinePassed()
    {
        if (_season == null || string.IsNullOrEmpty(_season.current_date)) return false;
        if (!System.DateTime.TryParse(_season.current_date, out var date)) return false;
        return date > new DateTime(_season.year_end, 2, 12);
    }

    // ── Despido anticipado ──

    bool ShouldFireManager()
    {
        if (_manager == null) return false;
        bool isPro = _manager.game_mode == "promanager";
        if (isPro && !IsObjectiveMetThisSeason()) return true;
        if (_manager.trust < 10) return true;
        return false;
    }

    bool IsTeamChampion()
    {
        if (_season == null || _myTeam == null) return false;
        string label = $"{_season.year_start}-{_season.year_end.ToString().Substring(2)}";
        var fr = DatabaseManager.Instance.Db.Table<FinalsRecord>()
            .FirstOrDefault(f => f.season == label);
        return fr != null && fr.champ_name == _myTeam.name;
    }

    bool ShowFiredModalIfNeeded()
    {
        if (IsTeamChampion()) return false;
        if (!ShouldFireManager()) return false;
        bool isPro = _manager.game_mode == "promanager";
        if (isPro && !IsObjectiveMetThisSeason())
            ShowObjectiveFiredModal();
        else
            ShowFiredModal();
        return true;
    }

    void MarkManagerFired()
    {
        if (_manager == null) return;

        try
        {
            _manager.is_fired = 1;
            DatabaseManager.Instance.SaveManager(_manager);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Dashboard] No se pudo marcar is_fired: {ex.Message}");
        }

        int slot = DatabaseManager.Instance.ActiveSaveSlot;
        var info = GameSaveManager.GetSlot(slot);
        if (info != null)
        {
            info.managerStatus = "fired";
            GameSaveManager.SaveSlotInfo(info);
        }
    }

    void ShowFiredModal()
    {
        MarkManagerFired();

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.despido"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios.\n\n" +
            "Los resultados de la temporada no han cumplido las expectativas mínimas."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = LocalizationService.T("ui.code.dashboard_controller.ir_al_menu_principal");
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--danger");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        actions.Add(btn);
        box.Add(actions);
    }

    void ShowObjectiveFiredModal()
    {
        MarkManagerFired();

        _firedOverlay.Clear();
        _firedOverlay.style.display = DisplayStyle.Flex;

        var box = new VisualElement();
        box.AddToClassList("fired-modal-box--modern");
        box.style.borderTopColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderBottomColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderLeftColor = new StyleColor(new Color32(192, 57, 43, 255));
        box.style.borderRightColor = new StyleColor(new Color32(192, 57, 43, 255));
        _firedOverlay.Add(box);

        var header = new VisualElement();
        header.AddToClassList("fired-modal-header");
        var title = new Label(LocalizationService.T("ui.code.dashboard_controller.despido"));
        title.AddToClassList("fired-modal-title");
        header.Add(title);
        box.Add(header);

        var body = new VisualElement();
        body.AddToClassList("fired-modal-body");
        string obj = _myTeam?.objective ?? "el objetivo de temporada";
        var text = new Label(
            "La directiva ha decidido prescindir de tus servicios.\n\n" +
            $"No has cumplido el objetivo de la temporada: {obj}.\n" +
            "En el modo ProManager, el objetivo es innegociable."
        );
        text.AddToClassList("fired-modal-text");
        body.Add(text);
        box.Add(body);

        var actions = new VisualElement();
        actions.AddToClassList("fired-modal-actions");
        var btn = new Button();
        btn.text = LocalizationService.T("ui.code.dashboard_controller.ir_al_menu_principal");
        btn.AddToClassList("btn-modal-confirm");
        btn.AddToClassList("btn-modal-confirm--danger");
        btn.RegisterCallback<ClickEvent>(_ =>
        {
            PlayClick();
            ScreenManager.Instance.GoTo(GameScreen.MainMenu);
        });
        actions.Add(btn);
        box.Add(actions);
    }

}
