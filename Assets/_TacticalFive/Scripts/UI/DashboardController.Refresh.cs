using UnityEngine;
using UnityEngine.UIElements;
using SQLite;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;

// B47: Refresco del dashboard: estadísticas, tablas, noticias e iconos.
public partial class DashboardController
{
    // ── ESTADISTICAS ───────────────────────────────────────
    void RefreshPlayerStats()
    {
        if (_season == null || _myTeam == null) return;

        var teamStats = DatabaseManager.Instance.GetTeamPlayerSeasonStats(_season.id, _myTeam.id, _manager.id);
        if (teamStats.Count == 0) return;

        var myPlayerIds = _players.Select(p => p.id).ToHashSet();
        teamStats = teamStats.Where(s => myPlayerIds.Contains(s.player_id)).ToList();
        if (teamStats.Count == 0) return;

        var scorer = teamStats.OrderByDescending(s => (float)s.total_points / s.games).First();
        SetStatCard("StatScorer", "StatScorerName", "StatScorerGames",
            (scorer.total_points / (float)scorer.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{scorer.first_name} {scorer.last_name}",
            $"{scorer.games} {(scorer.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            scorer.player_id);

        var rebounder = teamStats.OrderByDescending(s => (float)s.total_rebounds / s.games).First();
        SetStatCard("StatRebounder", "StatRebounderName", "StatRebounderGames",
            (rebounder.total_rebounds / (float)rebounder.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{rebounder.first_name} {rebounder.last_name}",
            $"{rebounder.games} {(rebounder.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            rebounder.player_id);

        var assister = teamStats.OrderByDescending(s => (float)s.total_assists / s.games).First();
        SetStatCard("StatAssister", "StatAssisterName", "StatAssisterGames",
            (assister.total_assists / (float)assister.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{assister.first_name} {assister.last_name}",
            $"{assister.games} {(assister.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            assister.player_id);

        var stealer = teamStats.OrderByDescending(s => (float)s.total_steals / s.games).First();
        SetStatCard("StatStealer", "StatStealerName", "StatStealerGames",
            (stealer.total_steals / (float)stealer.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{stealer.first_name} {stealer.last_name}",
            $"{stealer.games} {(stealer.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            stealer.player_id);

        var blocker = teamStats.OrderByDescending(s => (float)s.total_blocks / s.games).First();
        SetStatCard("StatBlocker", "StatBlockerName", "StatBlockerGames",
            (blocker.total_blocks / (float)blocker.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{blocker.first_name} {blocker.last_name}",
            $"{blocker.games} {(blocker.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            blocker.player_id);

        var rated = teamStats.OrderByDescending(s => (float)s.total_rating / s.games).First();
        SetStatCard("StatRated", "StatRatedName", "StatRatedGames",
            (rated.total_rating / (float)rated.games).ToString("F1", System.Globalization.CultureInfo.InvariantCulture),
            $"{rated.first_name} {rated.last_name}",
            $"{rated.games} {(rated.games == 1 ? "PARTIDO JUGADO" : "PARTIDOS JUGADOS")}",
            rated.player_id);
    }

    void SetStatCard(string valName, string playerName, string gamesName,
                     string val, string player, string games, int playerId = 0)
    {
        var valLbl = _root.Q<Label>(valName);
        var playerLbl = _root.Q<Label>(playerName);
        var gamesLbl = _root.Q<Label>(gamesName);
        var photoName = valName + "Photo";
        var photo = _root.Q<VisualElement>(photoName);

        if (valLbl != null) valLbl.text = val;
        if (playerLbl != null) playerLbl.text = player;
        if (gamesLbl != null) gamesLbl.text = games;

        if (photo != null && playerId > 0)
        {
            var p = _players.FirstOrDefault(pl => pl.id == playerId)
                     ?? DatabaseManager.Instance.GetPlayer(playerId);

            if (p != null)
            {
                Texture2D tex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (tex != null)
                    photo.style.backgroundImage = new StyleBackground(tex);
            }
        }
    }

    // ── ESTADÍSTICAS DEL EQUIPO ─────────────────────────

    int GetMyTeamConferenceRank()
    {
        if (_myTeam == null) return 0;
        return ObjectiveHelper.GetConferenceRank(_myTeam.id, _myTeam.conference, _allTeams, _allGames);
    }

    bool IsObjectiveMetThisSeason()
    {
        if (_myTeam == null) return false;
        return ObjectiveHelper.IsObjectiveMet(_myTeam.objective, GetMyTeamConferenceRank());
    }

    void RefreshTeamStats()
    {
        if (_myTeam == null) return;

        // Objetivo de la temporada
        if (_teamObjective != null)
            _teamObjective.text = _myTeam.objective ?? "--";

        // Estado del objetivo: calcular según posición en conferencia
        int rank = GetMyTeamConferenceRank();
        bool objectiveMet = ObjectiveHelper.IsObjectiveMet(_myTeam.objective, rank);

        if (_teamObjectiveStatus != null)
        {
            string iconName = objectiveMet ? "boton-v-64px" : "boton-x-64px";
            var tex = Resources.Load<Texture2D>($"Icons/{iconName}");
            if (tex != null)
                _teamObjectiveStatus.style.backgroundImage = new StyleBackground(tex);
        }

        // Logo del equipo (32x32 desde carpeta específica)
        if (_teamStatsLogo != null)
        {
            var logo32 = TeamImages.Logo(32, _myTeam.logo);
            if (logo32 != null)
                _teamStatsLogo.style.backgroundImage = new StyleBackground(logo32);
            else
                SetTeamLogo(_teamStatsLogo, _myTeam.logo);
        }

        // Media equipo (overall)
        int teamAvg = _players.Count > 0 ? Mathf.RoundToInt((float)_players.Average(p => p.GetCalculatedAverage())) : 0;
        if (_teamOverallLabel != null)
            _teamOverallLabel.text = $"Media: {teamAvg}";
        if (_teamOverallRingVal != null)
            _teamOverallRingVal.text = teamAvg.ToString();

        // Química equipo
        int chemistry = DatabaseManager.Instance.GetTeamChemistry(_myTeam.id);
        if (_teamChemistryLabel != null)
            _teamChemistryLabel.text = $"Química: {chemistry}";
        if (_teamChemistryIcon != null)
        {
            var tex = Resources.Load<Texture2D>("Icons/quimica");
            if (tex != null)
                _teamChemistryIcon.style.backgroundImage = new StyleBackground(tex);
        }
        if (_teamChemistryRingVal != null)
            _teamChemistryRingVal.text = chemistry.ToString();
        if (_teamChemistryRing != null)
        {
            Color ringColor;
            if (chemistry >= 70)
                ringColor = new Color32(39, 174, 96, 255);
            else if (chemistry >= 40)
                ringColor = new Color32(212, 160, 23, 255);
            else
                ringColor = new Color32(192, 57, 43, 255);
            _teamChemistryRing.style.borderTopColor = new StyleColor(ringColor);
            _teamChemistryRing.style.borderBottomColor = new StyleColor(ringColor);
            _teamChemistryRing.style.borderLeftColor = new StyleColor(ringColor);
            _teamChemistryRing.style.borderRightColor = new StyleColor(ringColor);
        }

        // Pabellón
        if (_teamArenaName != null)
            _teamArenaName.text = _myTeam.arena ?? "Pabellón";
        if (_teamArenaCapacity != null)
            _teamArenaCapacity.text = $"{_myTeam.capacity:N0} espectadores";

        // Estrellas de reputación
        if (_teamReputationStars != null)
        {
            _teamReputationStars.Clear();
            for (int i = 1; i <= 5; i++)
            {
                var star = new Label { text = i <= _myTeam.reputation ? "★" : "☆" };
                star.AddToClassList("team-stat-star");
                star.style.color = i <= _myTeam.reputation ? new Color(0.84f, 0.63f, 0.09f) : new Color(0.2f, 0.25f, 0.35f);
                _teamReputationStars.Add(star);
            }
        }
    }

    // ── RELACIONES ───────────────────────────────────────

    void RefreshBoard()
    {
        if (_manager == null) return;
        SetCircle(_barTrust, _valTrust, _manager.trust);
        SetCircle(_barMorale, _valMorale, _manager.morale);
        SetCircle(_barFanConfidence, _valFanConfidence, _manager.fan_confidence);
    }

    // ── MENSAJES ────────────────────────────────────────

    /// <summary>Badge con el hilo narrativo activo más relevante del equipo.</summary>
    void RefreshStorylineBadge()
    {
        if (_storylineBadge == null) return;

        var active = DatabaseManager.Instance.GetActiveStorylines(_manager.id, _myTeam.id);

        // Los hilos de jugador cuyo protagonista ya no está en la plantilla no se muestran
        // (el motor los cierra al avanzar el día; esto evita verlos antes).
        var rosterIds = new HashSet<int>(_players.Select(p => p.id));
        active = active.Where(s => s.subject_player_id <= 0 || rosterIds.Contains(s.subject_player_id)).ToList();

        if (active.Count == 0)
        {
            _storylineBadge.style.display = DisplayStyle.None;
            return;
        }

        var top = active.OrderByDescending(s => s.intensity).First();
        _storylineBadge.text = $"HILO: {top.title.ToUpper()}";
        _storylineBadge.tooltip = string.Join("\n", active.Select(s => $"{s.title} (escalón {s.intensity})"));
        _storylineBadge.style.display = DisplayStyle.Flex;
    }

    void RefreshMessages()
    {
        if (_messagesBody == null || _manager == null) return;
        _messagesBody.Clear();
        RefreshStorylineBadge();

        // Solo los últimos: antes se cargaba toda la bandeja en cada refresco.
        var all = DatabaseManager.Instance.GetRecentMessages(_manager.id, 20);
        if (all == null || all.Count == 0)
        {
            var lbl = new Label(LocalizationService.T("ui.code.dashboard_controller.no_hay_noticias"));
            lbl.AddToClassList("no-games-text");
            _messagesBody.Add(lbl);
            return;
        }

        var latest = all.OrderByDescending(m => m.id)
                        .Where(m => !m.title.StartsWith("Premios del Mes"))
                        .Take(5).ToList();

        foreach (var msg in latest)
        {
            var item = new VisualElement();
            item.AddToClassList("message-item");

            var icon = new VisualElement();
            icon.AddToClassList("message-item-icon");
            SetNewsImage(icon, msg);
            item.Add(icon);

            var textBlock = new VisualElement();
            textBlock.AddToClassList("message-item-text");

            var subj = new Label(msg.title ?? "Sin asunto");
            subj.AddToClassList("message-item-subject");
            textBlock.Add(subj);

            var body = new Label(GetBodyPreview(msg.body));
            body.AddToClassList("message-item-body");
            textBlock.Add(body);

            item.Add(textBlock);
            _messagesBody.Add(item);

            if (msg.is_read == 0)
                DatabaseManager.Instance.MarkMessageRead(msg.id);
        }
    }

    void SetNewsImage(VisualElement icon, MessageData msg)
    {
        // Ninguna noticia del panel debe quedar sin imagen.
        TrySetIcon(icon, "Icons/noticias_icon");

        if (!string.IsNullOrEmpty(msg.news_type))
        {
            if (SetNewsImageByType(icon, msg)) return;
        }
        SetNewsImageByTitle(icon, msg);
    }

    bool TrySetIcon(VisualElement icon, string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return false;
        var tex = Resources.Load<Texture2D>(resourcePath);
        if (tex == null) return false;
        icon.style.backgroundImage = new StyleBackground(tex);
        return true;
    }

    bool TrySetPlayerProtagonist(VisualElement icon, MessageData msg)
    {
        string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys)
            ?? ExtractFirstFromBody(msg.title, _playerByName.Keys);
        if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
        {
            var tex = PlayerPhotoHelper.Load(p.id, p.photo);
            if (tex != null)
            {
                icon.style.backgroundImage = new StyleBackground(tex);
                return true;
            }
        }
        return false;
    }

    static bool IsPlayerProtagonistType(string nt)
    {
        return nt == NewsType.TRIPLE_DOBLE || nt == NewsType.EXPLOSION
            || nt == NewsType.FICHAJE || nt == NewsType.HARD_CAP
            || nt == NewsType.SEQUIA || nt == NewsType.BAJA_ESTRELLA
            || nt == NewsType.RUMOR_TRASPASO || nt == NewsType.DESCONTENTO
            || nt == NewsType.LESION || nt == NewsType.RECUPERACION
            || nt == NewsType.RESERVA_IR || nt == NewsType.QUEJA
            || nt == NewsType.PREOCUPACION || nt == NewsType.HITO
            || nt == NewsType.LEGADO || nt == NewsType.FICHAJE_RECHAZADO
            || nt == NewsType.FICHAJE_CANCELADO || nt == NewsType.OFERTA_RECHAZADA
            || nt == NewsType.REFIRMA_CANCELADA || nt == NewsType.RENOVACION
            || nt == NewsType.REFIRMA_ENVIADA || nt == NewsType.AGENTE_LIBRE
            || nt == NewsType.DESPEDIDO || nt == NewsType.BUYOUT;
    }

    bool SetNewsImageByType(VisualElement icon, MessageData msg)
    {
        string nt = msg.news_type;

        if (IsPlayerProtagonistType(nt) && TrySetPlayerProtagonist(icon, msg))
            return true;

        // Hitos de legado (E1): icono de récord/trofeo.
        if (nt == NewsType.LEGADO)
        {
            var legacyTex = Resources.Load<Texture2D>(MilestoneCatalog.SectionIcon);
            if (legacyTex != null) { icon.style.backgroundImage = new StyleBackground(legacyTex); return true; }
            return false;
        }

        if (nt == NewsType.CUP_CHAMPION)
            return TrySetIcon(icon, "Icons/trofeoNBA_big");
        if (nt == NewsType.PROYECTO_CIUDAD || nt == NewsType.EVENTO_CIUDAD)
            return TrySetIcon(icon, "Icons/ciudad_icon");
        if (nt == NewsType.PREMIOS_MES)
            return TrySetIcon(icon, "Icons/premios_icon");

        if (nt == NewsType.HITO || nt == NewsType.RACHA_VICTORIAS || nt == NewsType.MALA_RACHA
            || nt == NewsType.CAMPANADA || nt == NewsType.RESULTADO || nt == NewsType.GLEAGUE_CHAMPION
            || nt == NewsType.REVANCHA || nt == NewsType.DINASTIA)
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
            if (nt == NewsType.GLEAGUE_CHAMPION)
                return TrySetIcon(icon, "Icons/trofeoGLeague_big");
            if (nt == NewsType.HITO || nt == NewsType.REVANCHA || nt == NewsType.DINASTIA)
                return TrySetIcon(icon, "Icons/record_icon");
            if (nt == NewsType.RESULTADO)
                return TrySetIcon(icon, "Icons/resultados128px");
            if (nt == NewsType.CAMPANADA)
            {
                var tex = _noticiasSprites.GetValueOrDefault("rechazado");
                if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
            }
            return false;
        }

        if (nt == NewsType.ALLSTAR)
        {
            if (_logoSprites.TryGetValue("all-star-game", out var allStarSprite))
            {
                icon.style.backgroundImage = new StyleBackground(allStarSprite);
                return true;
            }
            return TrySetIcon(icon, "Icons/estrella64px");
        }

        if (nt == NewsType.TRIPLE_DOBLE || nt == NewsType.EXPLOSION
            || nt == NewsType.FICHAJE || nt == NewsType.HARD_CAP
            // Hilos narrativos de jugador: foto del protagonista
            || nt == NewsType.SEQUIA || nt == NewsType.BAJA_ESTRELLA
            || nt == NewsType.RUMOR_TRASPASO || nt == NewsType.DESCONTENTO)
        {
            string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
            if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
            {
                var ptex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (ptex != null) { icon.style.backgroundImage = new StyleBackground(ptex); return true; }
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
                    var stex = Resources.Load<Texture2D>(sponsor.logo?.Replace(".png", ""));
                    if (stex != null) { icon.style.backgroundImage = new StyleBackground(stex); return true; }
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
                    var ctex = Resources.Load<Texture2D>(channel.logo?.Replace(".png", ""));
                    if (ctex != null) { icon.style.backgroundImage = new StyleBackground(ctex); return true; }
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
            return TrySetIcon(icon, "Icons/eliminatorias_icon");

        if (nt == NewsType.PREMIOS_MES)
            return TrySetIcon(icon, "Icons/premios_icon");

        if (nt == NewsType.QUEJA || nt == NewsType.PREOCUPACION || nt == NewsType.DESCONTENTO)
        {
            var tex = _noticiasSprites.GetValueOrDefault("quejas");
            if (tex != null) { icon.style.backgroundImage = new StyleBackground(tex); return true; }
        }

        if (nt == NewsType.FICHAJE || nt == NewsType.FICHAJE_RECHAZADO
            || nt == NewsType.FICHAJE_CANCELADO)
            return TrySetIcon(icon, "Noticias/contratado");
        if (nt == NewsType.HARD_CAP || nt == NewsType.RUMOR_TRASPASO)
            return TrySetIcon(icon, "Noticias/traspaso");
        if (nt == NewsType.SEQUIA || nt == NewsType.BAJA_ESTRELLA)
            return TrySetIcon(icon, "Noticias/lesionados");

        return false;
    }

    void SetNewsImageByTitle(VisualElement icon, MessageData msg)
    {
        if (msg.title != null && msg.title.IndexOf("NBA CUP", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (TrySetIcon(icon, "Icons/trofeoNBA_big")) return;
        }
        if (msg.title != null && msg.title.IndexOf("ciudad", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (TrySetIcon(icon, "Icons/ciudad_icon")) return;
        }
        if (msg.title != null && msg.title.StartsWith("Premios del Mes"))
        {
            if (TrySetIcon(icon, "Icons/premios_icon")) return;
        }

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
                var stex = Resources.Load<Texture2D>(sponsor.logo?.Replace(".png", ""));
                if (stex != null) { icon.style.backgroundImage = new StyleBackground(stex); return; }
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
                var ctex = Resources.Load<Texture2D>(channel.logo?.Replace(".png", ""));
                if (ctex != null) { icon.style.backgroundImage = new StyleBackground(ctex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Fichaje:"))
        {
            string playerName = msg.title.Substring("Fichaje: ".Length).Trim();
            if (_playerByName.TryGetValue(playerName, out var p))
            {
                var ptex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (ptex != null) { icon.style.backgroundImage = new StyleBackground(ptex); return; }
            }
        }
        else if (msg.sender_type == 1 && msg.title != null && msg.title.StartsWith("Hard cap activado"))
        {
            string playerName = ExtractFirstFromBody(msg.body, _playerByName.Keys);
            if (playerName != null && _playerByName.TryGetValue(playerName, out var p))
            {
                var ptex = PlayerPhotoHelper.Load(p.id, p.photo);
                if (ptex != null) { icon.style.backgroundImage = new StyleBackground(ptex); return; }
            }
        }
        else if (msg.sender_type == 2)
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

    static string GetBodyPreview(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        string normalized = body.Replace("\r\n", "\n").Trim();
        int idx = normalized.IndexOf("\n\n", StringComparison.Ordinal);
        string preview = idx < 0 ? normalized : normalized.Substring(0, idx).Trim();
        if (idx >= 0 && preview.Length > 0)
        {
            char lastChar = preview[preview.Length - 1];
            if (lastChar != '.' && lastChar != '!' && lastChar != '?' && lastChar != '\u2026')
                preview += "\u2026";
        }
        return preview;
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

    void SetCircle(VisualElement circle, Label val, int value)
    {
        if (circle == null || val == null) return;
        float pct = Mathf.Clamp01(value / 100f);

        Color bgColor, borderColor;
        if (value >= 70)
        {
            bgColor = new Color32(39, 174, 96, 40);
            borderColor = new Color32(39, 174, 96, 255);
        }
        else if (value >= 40)
        {
            bgColor = new Color32(212, 160, 23, 40);
            borderColor = new Color32(212, 160, 23, 255);
        }
        else
        {
            bgColor = new Color32(192, 57, 43, 40);
            borderColor = new Color32(192, 57, 43, 255);
        }

        circle.style.backgroundColor = new StyleColor(bgColor);
        circle.style.borderTopColor = new StyleColor(borderColor);
        circle.style.borderBottomColor = new StyleColor(borderColor);
        circle.style.borderLeftColor = new StyleColor(borderColor);
        circle.style.borderRightColor = new StyleColor(borderColor);

        val.text = $"{value}%";
    }

    // ── HELPERS ──────────────────────────────────────────

    void SetTeamLogo(VisualElement elem, string logoName, string sizeFolder = null)
    {
        if (elem == null || string.IsNullOrEmpty(logoName)) return;

        if (!string.IsNullOrEmpty(sizeFolder))
        {
            var sprite = TeamImages.Logo(sizeFolder, logoName);
            if (sprite != null)
            {
                elem.style.backgroundImage = new StyleBackground(sprite);
                return;
            }
        }

        if (_logoSprites.TryGetValue(logoName, out var fallback))
            elem.style.backgroundImage = new StyleBackground(fallback);
    }

    /// <summary>
    /// Convocatoria del partido de las estrellas: los **3 mejores de cada posición por
    /// valoración media de la temporada regular** (15 por conferencia), excluyendo a los
    /// lesionados. Si un jugador aún no tiene partidos (valoración 0) sirve el overall como
    /// desempate.
    /// </summary>
    /// <summary>
    /// Reparto de los 240 minutos del partido de exhibición entre los convocados (todos deben
    /// jugar). Marca `rotation_minutes` de forma vinculante para que el simulador lo respete.
    /// </summary>
    void AssignAllStarMinutes(List<PlayerData> players)
    {
        if (players == null || players.Count == 0) return;

        const int TEAM_MINUTES = 240;
        int n = players.Count;
        int baseMin = TEAM_MINUTES / n;          // 16 con 15 jugadores
        int remainder = TEAM_MINUTES - baseMin * n;

        for (int i = 0; i < n; i++)
            players[i].rotation_minutes = baseMin + (i < remainder ? 1 : 0);
    }

    List<PlayerData> BuildAllStarRoster(string conference)
    {
        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var conferenceTeamIds = allTeams
            .Where(t => t.conference == conference)
            .Select(t => t.id)
            .ToHashSet();

        var candidates = DatabaseManager.Instance.Db.Table<PlayerData>()
            .Where(p => p.team_id != 0 && p.injury_days == 0 && p.g_league_assigned == 0)
            .ToList()
            .Where(p => conferenceTeamIds.Contains(p.team_id))
            .ToList();

        // Valoración media (rating) de la temporada regular por jugador.
        var avgRating = new Dictionary<int, double>();
        var gamesPlayed = new Dictionary<int, int>();
        foreach (var s in DatabaseManager.Instance.GetSeasonPlayerStatsAggregates(_manager.id, _season.id))
        {
            avgRating[s.player_id] = s.gp > 0 ? (double)s.total_rating / s.gp : 0;
            gamesPlayed[s.player_id] = s.gp;
        }

        var roster = new List<PlayerData>();
        var usedIds = new HashSet<int>();

        // Por posición se toman los 3 mejores por valoración: el 1º de cada puesto es titular
        // y el 2º/3º suplentes. Los 5 titulares (mejor B/E/A/AP/P) van primero para que
        // `Take(5)` y `SaveAllStarRoster` (i < 5) los identifiquen correctamente.
        var byPosition = new Dictionary<string, List<PlayerData>>();
        foreach (var pos in PositionCodes.Order)
        {
            var selected = candidates
                .Where(p => p.position == pos)
                .OrderByDescending(p => avgRating.TryGetValue(p.id, out var r) ? r : 0)
                .ThenByDescending(p => gamesPlayed.TryGetValue(p.id, out var g) ? g : 0)
                .ThenByDescending(p => p.overall)
                .Take(3)
                .ToList();
            byPosition[pos] = selected;
            foreach (var p in selected) usedIds.Add(p.id);
        }

        // 5 titulares: el mejor de cada posición principal.
        foreach (var pos in PositionCodes.Order)
            if (byPosition[pos].Count > 0) roster.Add(byPosition[pos][0]);

        // Suplentes: el 2º/3º de cada posición.
        foreach (var pos in PositionCodes.Order)
            for (int i = 1; i < byPosition[pos].Count; i++)
                roster.Add(byPosition[pos][i]);

        // Relleno si alguna posición no llega a 3 (por valoración, luego overall).
        if (roster.Count < 15)
        {
            var remaining = candidates
                .Where(p => !usedIds.Contains(p.id))
                .OrderByDescending(p => avgRating.TryGetValue(p.id, out var r) ? r : 0)
                .ThenByDescending(p => p.overall)
                .Take(15 - roster.Count);
            roster.AddRange(remaining);
        }

        return roster;
    }

    // ── CLASE AUXILIAR ───────────────────────────────────

    // StandingRow vive en Scripts/Core/StandingsHelper.cs (compartida con
    // Standings, Play-In/Playoffs y ObjectiveHelper).

    // ═══════════════════════════════════════════════════════════
    //  CONFIG MODAL
    // ═══════════════════════════════════════════════════════════

    void SetupPlayerCoach()
    {
        if (_myTeam == null || _manager == null) return;
        DatabaseManager.Instance.AddPlayerCoachEntry(_myTeam.id, _manager.name);
        DatabaseManager.Instance.SetCoachInactive(_myTeam.id);
    }

    void AssignSeasonPoints()
    {
        if (_manager == null || _season == null) return;

        var allTeams = DatabaseManager.Instance.GetAllTeams();
        var allGames = DatabaseManager.Instance.GetStandingsGames(_manager.id);
        allGames.AddRange(DatabaseManager.Instance.GetPlayoffGames(_manager.id));
        allGames.AddRange(DatabaseManager.Instance.GetPlayInGames(_manager.id));
        var ranking = DatabaseManager.Instance.GetCoachRanking();

        var eastTeams = allTeams.Where(t => t.conference == "East").ToList();
        var westTeams = allTeams.Where(t => t.conference == "West").ToList();

        var teamRank = new Dictionary<int, int>();
        ComputeConfRanks(eastTeams, allGames, teamRank);
        ComputeConfRanks(westTeams, allGames, teamRank);

        int championId = 0, finalistId = 0;
        var finalsList = DatabaseManager.Instance.GetFinalsRecords();
        var seasonKey = $"{_season.year_start}-{_season.year_end}";
        var finals = finalsList.FirstOrDefault(f => f.season == seasonKey);
        if (finals != null)
        {
            championId = allTeams.FirstOrDefault(t => t.logo == finals.champ_keyword)?.id ?? 0;
            finalistId = allTeams.FirstOrDefault(t => t.logo == finals.finalist_keyword)?.id ?? 0;
        }

        foreach (var team in allTeams)
        {
            var coach = ranking.FirstOrDefault(c => c.team_id == team.id && (c.status == "active" || c.status == "player"));
            if (coach == null) continue;

            int points = CalcTeamPoints(team, allGames, teamRank, championId, finalistId);
            DatabaseManager.Instance.UpdateCoachScore(coach.id, points);
        }
    }

    void ComputeConfRanks(List<TeamData> confTeams, List<GameData> allGames, Dictionary<int, int> teamRank)
    {
        var standings = new List<(int teamId, int wins, int losses)>();
        foreach (var t in confTeams)
        {
            var tg = allGames.Where(g => g.is_played == 1 && g.game_type == "regular" && (g.home_team_id == t.id || g.away_team_id == t.id)).ToList();
            int w = tg.Count(g => (g.home_team_id == t.id && g.home_score > g.away_score) || (g.away_team_id == t.id && g.away_score > g.home_score));
            standings.Add((t.id, w, tg.Count - w));
        }
        standings.Sort((a, b) =>
        {
            float pctA = a.wins + a.losses > 0 ? (float)a.wins / (a.wins + a.losses) : 0;
            float pctB = b.wins + b.losses > 0 ? (float)b.wins / (b.wins + b.losses) : 0;
            if (pctB != pctA) return pctB.CompareTo(pctA);
            if (a.losses != b.losses) return a.losses.CompareTo(b.losses);
            return b.wins.CompareTo(a.wins);
        });
        for (int i = 0; i < standings.Count; i++)
            teamRank[standings[i].teamId] = i + 1;
    }

    int CalcTeamPoints(TeamData team, List<GameData> allGames, Dictionary<int, int> teamRank, int championId, int finalistId)
    {
        int pts = 0;

        var rg = allGames.Where(g => g.is_played == 1 && g.game_type == "regular" && (g.home_team_id == team.id || g.away_team_id == team.id)).ToList();
        int rWins = rg.Count(g => (g.home_team_id == team.id && g.home_score > g.away_score) || (g.away_team_id == team.id && g.away_score > g.home_score));
        pts += Mathf.RoundToInt(rWins * 0.5f);
        if (rWins >= 60) pts += 20;
        else if (rWins >= 50) pts += 10;

        var pg = allGames.Where(g => g.is_played == 1 && (g.game_type == "playoff" || g.game_type == "playin") && (g.home_team_id == team.id || g.away_team_id == team.id)).ToList();
        int pWins = pg.Count(g => (g.home_team_id == team.id && g.home_score > g.away_score) || (g.away_team_id == team.id && g.away_score > g.home_score));
        pts += pWins * 2;

        if (team.id == championId) pts += 100;
        else if (team.id == finalistId) pts += 50;
        else if (pWins >= 8) pts += 25;

        int rank = teamRank.TryGetValue(team.id, out var r) ? r : 0;
        string obj = team.objective ?? "";
        bool met = false;
        if (rank > 0)
        {
            if (obj == "Zona tranquila") met = rank <= 12;
            else if (obj == "Play-In") met = rank <= 10;
            else if (obj == "Playoffs") met = rank <= 6;
            else if (obj == "Campeonato") met = rank <= 2;
        }

        if (met)
        {
            if (obj == "Campeonato") pts += 40;
            else if (obj == "Playoffs") pts += 25;
            else if (obj == "Play-In") pts += 12;
            else if (obj == "Zona tranquila") pts += 6;
        }
        else if (rank > 0)
        {
            pts -= 15;
        }

        return pts;
    }
}
