# Dashboard Modals — Update Tracking

> Objetivo: modernizar todos los modales del Dashboard al formato header+body estándar (imagen de referencia: cabecera con fondo oscuro translúcido, borde inferior, título 32px BarlowCondensed-Bold blanco, body padding 20px 24px, botones outline modernos).
> Última actualización: 2026-09-19

---

## Referencia visual: formato header+body

```
┌──────────────────────────────────────┐
│  TÍTULO DEL MODAL  (32px bold,      │  ← header: bg rgb() oscuro, border-bottom
│  color blanco, padding 12px 24px)    │
├──────────────────────────────────────┤
│                                      │
│  Contenido del cuerpo                │  ← body: padding 20px 24px
│  (texto, listas, badges...)          │
│                                      │
├──────────────────────────────────────┤
│  [CANCELAR]        [ACEPTAR]        │  ← buttons: outline modernos
└──────────────────────────────────────┘
```

- Botón cancelar: `btn-modal-cancel` (gris outline)
- Botón confirmar: `btn-modal-confirm` (verde outline) / `btn-modal-confirm--gold` (dorado) / `btn-modal-confirm--danger` (rojo)
- Ancho modal básico: **650px** (`fired-modal-box--modern`)

---

## UXML-Defined Modals (3)

Gestionados por `UIScreenController` base. Estos ya siguen el patrón estándar (modal-title + modal-body + modal-actions).

| # | Modal | Elementos | Estado |
|---|---|---|---|
| 1 | ConfigModalOverlay | Sliders audio, calidad gráficos, sim mode, volver/salir | ✅ Hecho (btn-modal-cancel CERRAR) |
| 2 | ConfigMainMenuConfirmOverlay | Confirmación volver menú | ✅ Hecho (btn-modal-cancel + btn-modal-confirm--danger) |
| 3 | ConfigExitConfirmOverlay | Confirmación salir juego | ✅ Hecho (btn-modal-cancel + btn-modal-confirm--danger) |

---

## Procedural C# Modals (14)

Todos construidos en `DashboardController.cs` usando `_firedOverlay`. Cada uno necesita reestructurarse con header+body.

| # | Modal | Borde | Trigger | Método | Estado |
|---|---|---|---|---|---|
| 4 | Budget Warning | Gold | Presupuesto rojo | `ShowBudgetWarningModal` | ✅ Hecho |
| 5 | Budget Fired | Red | Avisos >= umbral | `ShowBudgetFiredModal` | ✅ Hecho |
| 6 | Injured Lineup | Red | Lesionados en quinteto | `ShowInjuredLineupModal` | ✅ Hecho |
| 7 | Load Management | Red | Jugadores cansados | `ShowLoadManagementModal` | ✅ Hecho |
| 8 | Empty Lineup | Red | Slots vacíos | `ShowEmptyLineupModal` | ✅ Hecho |
| 9 | Offer Result | Green/Gold/Red | Ofertas maduradas | `ShowOfferResultModal` | ✅ Hecho |
| 10 | Trade Offer | Dark Gold | Traspaso IA | `ShowTradeOfferModal` | ✅ Hecho |
| 11 | Trade Deadline Week | Gold | 1 de febrero | `ShowTradeDeadlineModal` | ✅ Hecho |
| 12 | Deadline Day | Gold | 7 de febrero | `ShowDeadlineDayModal` | ✅ Hecho |
| 13 | Recovered Player(s) | Green | Recuperación lesión | `ShowRecoveredModal` | ✅ Hecho |
| 14 | IR Release | Green | Roster > MAX | `ShowPendingIRReleaseModal` | ✅ Hecho |
| 15 | Fired (Trust) | Red | Confianza < 10 | `ShowFiredModal` | ✅ Hecho |
| 16 | Fired (Objective) | Red | ProManager objetivo | `ShowObjectiveFiredModal` | ✅ Hecho |
| 17 | Fast Sim Summary | Blue | Sim rápida OK | `ShowFastSimSummary` | ✅ Hecho |

---

## Nota arquitectónica

- Los 14 modales procedurales comparten un único `_firedOverlay` VisualElement
- Cada método llama `_firedOverlay.Clear()` antes de construir su UI
- Solo un modal puede mostrarse a la vez
- `IsAnyModalOpen()` solo verifica `_firedOverlay.style.display`
