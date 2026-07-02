# Plan de Mejoras — Sistema de Traspasos y Fichajes NBA

## Diagnóstico principal

**Bug crítico**: Equipos muy por encima del salary cap pueden ofertar salarios enormes a agentes libres externos porque `GetMaxOfferBreakdown` usa la excepción "SIN BIRD" (120% del salario previo) para cualquier FA, cuando en realidad esa excepción solo aplica para renovar jugadores propios.

**Ejemplo**: OKC con $230M payroll (-$75M margen) puede ofertar $54.25M a LeBron James como FA. En la NBA real un equipo sobre el cap solo puede fichar FAs externos usando las excepciones MLE, BAE o mínimo.

---

## Fase 1: Arreglar ofertas a agentes libres externos

### 1.1 — `GetMaxOfferBreakdown`: eliminar Bird Rights para FA externos

**Archivo**: `RosterController.cs` (líneas 947-968)

Cambiar la lógica para `isFromSameTeam=false`:
- `birdMax = 0` (sin Bird Rights para FA externos)
- Si `totalPayroll <= salary_cap`: usar `capSpaceMax` (espacio salarial disponible)
- Si `totalPayroll > salary_cap`: usar excepciones según apron:
  - `≤ FIRST_APRON`: NT-MLE (~$14.1M)
  - `≤ SECOND_APRON`: T-MLE (~$5.7M)  
  - `> SECOND_APRON`: salario mínimo (~$2M)

### 1.2 — `UpdateFAMaxInfo`: mostrar explicación de la excepción usada

**Archivo**: `MarketController.cs`

Mostrar qué tipo de excepción permite la oferta y por qué (cap space, NT-MLE, T-MLE, mínimo).

### 1.3 — `SendFAOffer`: validar antes de enviar

**Archivo**: `MarketController.cs`

Rechazar la oferta inmediatamente si excede el máximo legal, en lugar de dejar que el usuario espere 7 días para recibir un rechazo automático.

### 1.4 — Simplificar `ProcessMaturedOffers`

**Archivo**: `DashboardController.cs`

Quitar el check de cap space redundante (líneas 1020-1046), ya que la validación se hará en el envío.

---

## Fase 2: Hard cap + validación bilateral de traspasos

### 2.1 — Hard cap tracking

**Archivo**: `TeamData.cs`
- Campo `first_apron_hard_capped` (int, 0/1)

**Archivo**: `DashboardController.cs`
- Al usar NT-MLE para fichar un FA, marcar `first_apron_hard_capped = 1`
- Bloquear cualquier transacción que lleve el payroll por encima de `FIRST_APRON`

### 2.2 — `ValidateTradeSide`: validación para un equipo

**Archivo**: `TradeHelper.cs`
- Nuevo método estático que aplica reglas de salary matching a UN solo equipo
- Parámetro `hardCappedToFirstApron` para el nuevo hard cap
- Reglas según payroll post-trade:
  - `> SECOND_APRON` o hard-capped + `> FIRST_APRON`: no agregación, salario entrante ≤ saliente
  - `> FIRST_APRON`: máximo 110% del salario saliente
  - resto: reglas estándar (2×+$250K, +$7.5M, 125%+$250K)

### 2.3 — `ValidateTrade`: validar ambos lados

**Archivo**: `TradeHelper.cs`
- Llamar `ValidateTradeSide()` para el equipo A Y el equipo B
- Pasar flags de hard cap de cada equipo

### 2.4 — Actualizar callers

**Archivos**: `MarketController.cs`, `DashboardController.cs`
- Pasar nombres y payrolls de ambos equipos
- Pasar flags de hard cap

---

## Fase 3: Draft picks + Luxury Tax

### 3.1 — Modelo de draft picks

**Archivo nuevo**: `DraftPickData.cs`
- Tabla SQLite `draft_picks`
- Campos: id, season_id, round, pick_number, original_team_id, current_team_id

**Archivo**: `DatabaseManager.cs`
- `CreateTable<DraftPickData>()` en `CreateTables`
- `SeedDraftPicks(seasonId)`: 2 rondas × 30 equipos, ordenados por overall

### 3.2 — Luxury tax

**Archivo**: `TradeHelper.cs`
- `CalculateLuxuryTax(payroll)`: tramos progresivos
  - $0-$5M sobre tax: ×1.50
  - $5M-$10M: ×1.75
  - $10M-$15M: ×2.50
  - $15M-$20M: ×3.25
  - $20M+: ×3.75

**Archivo**: `FinanceRecord.cs`
- `TYPE_TAX = 10`

**Archivo**: `EndSeasonController.cs`
- `CollectLuxuryTax()`: calcular y registrar para los 30 equipos

**Archivo**: `FinancesController.cs`
- Mostrar luxury tax en tabla de gastos

---

## Fase 4: Sign-and-Trade + Buyout

### 4.1 — Sign-and-Trade en traspasos

**Archivo**: `MarketController.cs`
- Detectar jugadores entrantes con `contract_years <= 1`
- Mostrar toggle "Sign & Trade" en el panel de confirmación
- Al confirmar: extender contrato (`CalcSATYears`/`CalcSATSalary`), hard cap al equipo receptor
- `trade_type = "sign_and_trade"` en TradeData

### 4.2 — Buyout con stretch provision

**Archivos**: `Roster.uxml`, `RosterController.cs`
- Botón "RESCINDIR CONTRATO" en panel de detalle
- Modal con opción de rescisión progresiva
- Stretch: salario restante ÷ (2 × años pendientes), pagos anuales

**Archivo**: `FinanceRecord.cs`
- `TYPE_BUYOUT = 11`

**Archivo**: `Roster.uss`
- Estilo `.btn-buyout`

**Archivo**: `FinancesController.cs`
- Mostrar buyout en tabla de gastos

---

## Constantes NBA 2025-26 (fuente única: `TradeHelper.cs`)

| Constante | Valor |
|-----------|-------|
| `SALARY_CAP` | $154,647,000 |
| `LUXURY_TAX` | $200,428,000 |
| `FIRST_APRON` | $209,015,000 |
| `SECOND_APRON` | $221,686,000 |
| `NT_MLE` | $14,100,000 |
| `T_MLE` | $5,700,000 |
| `MIN_SALARY` | $2,000,000 |
| `MAX_ROSTER` | 18 |

---

## Decisiones clave

1. **Sin Bird Rights para FA externos**: las excepciones Bird/Non-Bird solo aplican al renovar jugadores propios
2. **NT-MLE activa hard cap**: el equipo no puede superar FIRST_APRON en ninguna transacción posterior
3. **Validación en envío, no en maduración**: la oferta se rechaza al enviarla si es ilegal, no 7 días después
4. **Ambos lados del traspaso validados**: las reglas de apron aplican tanto al equipo IA como al usuario
5. **Constantes unificadas en TradeHelper**: eliminar duplicados de `MarketController` y `FinancesController`

---

## Archivos modificados

| Archivo | Fases |
|---------|-------|
| `TradeHelper.cs` | 2, 3 |
| `RosterController.cs` | 1 |
| `MarketController.cs` | 1, 2, 4 |
| `DashboardController.cs` | 1, 2 |
| `TeamData.cs` | 2 |
| `FinanceRecord.cs` | 3, 4 |
| `FinancesController.cs` | 3, 4 |
| `EndSeasonController.cs` | 3 |
| `Roster.uxml` | 4 |
| `Roster.uss` | 4 |
| `DraftPickData.cs` (nuevo) | 3 |
| `DatabaseManager.cs` | 3 |
