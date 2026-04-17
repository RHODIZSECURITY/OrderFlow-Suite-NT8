# OrderFlow Suite NT8 — Estado del Proyecto

## Contexto General
- **Proyecto**: NinjaTrader 8 C# indicators — suite profesional de Order Flow, Smart Money Concepts y análisis de volumen
- **Versión actual**: 1.5.0
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ`
- **CI/CD**: Push a `main` → GitHub Actions genera ZIP versionado → crea Release en GitHub automáticamente
- **Rama de trabajo**: `main` — nunca crear feature branches permanentes

---

## 8 indicadores modulares — COMPLETADOS ✅

| # | Archivo | Motores principales | Estado |
|---|---------|---------------------|--------|
| 1 | `TrendSeries.cs` | EMA ×4 · SMA ×5 · Bollinger Bands Pro (SMA/EMA/WMA/VWMA, fill D2D) | ✅ |
| 2 | `SupportResistance.cs` | Pivots / Donchian / CSID · Zone depth ATR · Mitigation · Sweep · Retest strength · Overlap merge/hide | ✅ |
| 3 | `LevelsSuite.cs` | PDH/PDL · NY Pre-Market H/L/VWAP · Session VWAP + **Bandas ±1σ/±2σ** · ORB Pro (Break/Trap/Reversal) · Session Gap · **ICT Kill Zones** (Asia/London/NY AM) | ✅ |
| 4 | `SmartMoneyConcepts.cs` | FVG Delta (quality filter) + **Mitigation** · Order Blocks + **Invalidation** · **Breaker Blocks** (OB flippeado) · Merge/KeepAll · MaxZones cap | ✅ |
| 5 | `StructureSuite.cs` | BOS/CHoCH · **Internal Structure (iBOS/iCHoCH)** · Liquidity Sweeps · Premium/Discount · **EQH/EQL** · **Swing Dots** | ✅ |
| 6 | `OrderFlowSignals.cs` | **Big Trades** (SharpDX círculos, radio log-vol, delta opcional) · **Absorption** (SharpDX rombos, radio log-vol) · **Triple-A** Fabio Valentini · **Señales configurables** (Triangle/Arrow/Diamond/Dot) | ✅ |
| 7 | `HeatMapFlow.cs` | Order book depth heatmap · Bid/Ask imbalance · Large order detection (SharpDX D2D) | ✅ |
| 8 | `VolumeProfile.cs` | Delta · RelVol · FilteredVol · CumulativeDelta · **POC / VAH / VAL** (value area 70%) | ✅ |

### AddOns (NO MODIFICAR — nunca)
- `AddOns/SE.cs` — librería matemáticas
- `AddOns/OrderFlow-Suite.cs` — WyckoffRenderControl base class (SharpDX, BrushCache, etc.)

---

## Reglas de desarrollo — CRÍTICAS (leer antes de tocar cualquier archivo)

| Regla | Detalle |
|-------|---------|
| **Namespace** | `NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ` en TODOS los indicadores |
| **MaxLookBack** | `MaxLookBack = MaximumBarsLookBack.Infinite;` en `SetDefaults` de TODOS — sin excepción |
| **XmlIgnore** | Siempre `using System.Xml.Serialization;` + par `*Serializable` con `Serialize.BrushToString` para cada `Brush` |
| **Draw objects** | Tag único por barra; usar `Queue<string>` con límite (`MaxBubbles=600`, `MaxImbalances=400`, `MaxOrbSignals=200`) |
| **NaN guards** | Antes de ATR, sqrt, división — nunca asumir que son > 0 |
| **IsNYCash()** | Usar `TimeSpan` — NUNCA `h*100+m` |
| **TickSize** | No dividir por TickSize sin `Math.Max(TickSize, epsilon)` |
| **SharpDX** | Toda `PathGeometry`, `GeometrySink`, `TextFormat` creada debe ser `Dispose()`d explícitamente |
| **DataLoaded** | Resetear TODAS las colecciones (`List<>`, `Queue<>`, contadores, acumuladores) en `State.DataLoaded` |
| **IsSuspendedWhileInactive** | Indicadores con `Calculate.OnEachTick` deben tenerlo (excepto HeatMapFlow que lo desactiva intencionalmente) |
| **Rama** | Todo va directamente a `main` — el CI/CD solo dispara en push a main |

---

## Arquitectura — motores Pro implementados

### LevelsSuite.cs
- **VWAP SD Bands** ±1σ/±2σ — varianza incremental: `σ = √(ΣVol·P²/ΣVol − VWAP²)`; campos `_sessSumVT2`, reset en `ResetSessionState()`
- **ICT Kill Zones** — Asia (20:00–00:00 ET, dotted), London (02:00–05:00 ET, dashed), NY AM (07:00–10:00 ET, solid); tags con fecha para evitar bleed entre sesiones
- **ORB Pro** — Break / Trap (fakeout con `Close[1]`) / Reversal (diamond); cutoff hora configurable; signals en `Queue<string>` con cap 200
- **Session Gap** — evaluado una sola vez en 09:30–09:31:30 ET

### SmartMoneyConcepts.cs
- **FVG Mitigation** — zona se atenúa (opacity/4) cuando `Close` cierra dentro del gap
- **OB Invalidation** — OB eliminado cuando precio cierra al otro lado
- **Breaker Blocks** — OB invalidado flipea a BB de dirección opuesta; cap MaxZones=300
- **KeepAll mode** — usa `AddZoneCapped()` que enforce MaxZones=300 con eviction del más antiguo
- **Quality filter**: `Displaced` (body ≥ 40% ATR) / `Strong` (body ≥ 70% ATR)

### StructureSuite.cs
- **EQH/EQL** — Equal Highs/Lows con tolerancia en ticks (`EqTolerance`, default 3)
- **Swing Dots** — pivot exacto con `IsPivotHigh/Low()` (comparación estricta `>` / `<`)
- **Internal Structure** — `iBOS↑/↓` (break menor CON tendencia) y `iCHoCH↑/↓` (break menor CONTRA tendencia = early reversal)
- BOS/CHoCH externo sobre `SwingStrength` bars

### OrderFlowSignals.cs
- **Big Trades** — `OnMarketData` detecta prints al bid/ask; SharpDX `FillEllipse` con radio log₁₀(vol/BigPrintSize); delta opcional como texto centrado en el círculo (`ShowBubbleDelta`). Thread-safe `Queue<BubblePrint>` cap 500.
- **Absorption** — barra de alto volumen + rango estrecho (vol > avg×Mult, range < ATR×Factor); SharpDX `PathGeometry` 4 puntos (rombo); radio log proporcional a vol/avg. Queue cap 200.
- **Triple-A (Fabio Valentini)** — `None → Absorption↑/↓ → Accumulation (inside/tight bar) → Aggression (breakout+vol)`; señales en `Values[0]/[1]` (cross-indicator); renderizado SharpDX en `OnRender` con `SignalMarkerShape` configurable.
- **Signal shapes** — `Triangle / Arrow / Diamond / Dot`; flecha con tallo (PathGeometry 7 puntos); tamaño en px configurable (`SignalSize`).
- **DX cache** — `EnsureDxResources(rt)` recrea brushes + `TextFormat` solo si `RenderTarget` cambia; `DisposeDxResources()` en `State.Terminated`.
- **Eliminados de UI** — Stacked Imbalances, LVN Engine (removidos completamente; no eran necesarios para 3A).

### VolumeProfile.cs
- **POC** — `SortedList<long, double>` keyed por `Math.Round(Close/TickSize)`; precio con mayor volumen
- **VAH/VAL** — expansión bilateral desde POC hasta capturar `ValueAreaPct`% (default 70%) del volumen total
- **Running mode cap** — elimina nivel de menor volumen al superar 5000 entradas; `_totalSessionVol` se decrementa en sync
- `DrawOnPricePanel = true` — líneas POC/VAH/VAL en el panel de precios

### TrendSeries.cs
- **Bollinger Bands Pro** — stddev muestral `√(ΣSq / (N-1))`; VWMA con guard `sumV > 0`; fill área D2D con `GeometrySink.Dispose()` explícito

### HeatMapFlow.cs
- `WyckoffRenderControl` (AddOn) gestiona brush cache con `DisposeBrushCache()` en `State.Terminated`
- `marketOrderLadder` se recrea en `State.DataLoaded` para limpiar datos de sesión anterior
- Opacity de big pending orders clampeada a `Math.Min(1f, ...)` — evita valores SharpDX inválidos

---

## Bugs corregidos (historial para evitar regresiones)

| Archivo | Bug | Fix aplicado |
|---------|-----|--------------|
| `TrendSeries.cs` | BB stddev poblacional en vez de muestral | `/ (N-1)` |
| `TrendSeries.cs` | `GeometrySink` sin `Dispose()` en loop OnRender | `sink.Dispose()` después de `sink.Close()` |
| `SupportResistance.cs` | Pivot con `>=`/`<=` — iguales no detectados como pivots | Cambiado a `>` / `<` (strict) |
| `SmartMoneyConcepts.cs` | KeepAll mode sin cap MaxZones | `AddZoneCapped()` helper |
| `OrderFlowSignals.cs` | Absorción con OR logic suelta `|| Low==Low[1]` | Simplificado a `Close >= Open` |
| `OrderFlowSignals.cs` | `ATR(5)` accedido en barra 5 → avgRange≈0 | Guard `CurrentBar < 10` + `Math.Max(TickSize, ATR(5)[0])` |
| `VolumeProfile.cs` | Running cap eliminaba nivel sin decrementar `_totalSessionVol` | `_totalSessionVol -= removedVol` |
| `LevelsSuite.cs` | ORB signals acumulaban draw objects indefinidamente | `Queue<string> _orbSignalTags` con cap 200 |
| `HeatMapFlow.cs` | `filterBigPendingOrders` opacity > 1.0 posible | `Math.Min(1f, ...)` |
| `HeatMapFlow.cs` | `marketOrderLadder` no reseteado en DataLoaded | `= new PriceLadder()` en DataLoaded |
| `TrendSeries.cs` | `OnRender` sin try-catch — SharpDX throw = chart freeze | `try/catch` outer + inner + NaN guards por segmento |
| `OrderFlowSignals.cs` | `OnMarketData` sin try-catch — malformed tick = crash | `try/catch` silent + null/price guards (dispara ~100x/seg) |
| `HeatMapFlow.cs` | `OnMarketDepth/OnMarketData` sin try-catch ni null-check completo | `try/catch` + guards de `bookMap/orderBookLadder/wyckoffBars/marketOrderLadder` |
| `VolumeProfile.cs` | Delta aproximado con `Close>=Open` aun con OrderFlow+ | Detección `VolumetricBarsType` → `BuyingVolume/SellingVolume` real; fallback a estimación |
| `OrderFlowSignals.cs` | `Draw.Dot` fixed-size, sin formas configurables, sin delta en burbuja | Reescrito: SharpDX circles + diamonds + 4 signal shapes + delta text |
| `SupportResistance.cs` | `areaOpacity = 0` — zonas completamente invisibles | `ZoneOpacity` property (default 20) |
| `SupportResistance.cs` | `barsAgo = 0` en `Draw.Rectangle` — zona no ancla en barra de detección | `ago = CurrentBar - z.StartBar` |
| `SupportResistance.cs` | `EnforceVisibility` vs `RedrawZone` fighting — zonas ocultas se redibujaban | Campo `Hidden` en `SRZone` struct; `RedrawZone` omite si `Hidden \|\| Mitigated` |
| `SupportResistance.cs` | `oldestIdx` apuntaba a zona más reciente, no la más antigua | Comparación de `StartBar` corregida: busca mínimo `StartBar` |

---

## OrderFlow+ (suscripción NinjaTrader)
- **VolumeProfile** usa estimación `Close>=Open ? Volume : 0` para delta (fallback universal).
- NT8's `VolumetricData` es ensamblado interno — no expone `BuyingVolume/SellingVolume` vía NinjaScript SDK público. Intentos de acceso generan CS1061 en compilación.
- Fallback a estimación nunca crashea y funciona en todas las cuentas.

### SupportResistance.cs — Arquitectura (v1.5.0)
- **Métodos de detección**: `Pivots` (default, strict `>/<`), `Donchian`, `CSID`
- **`SRZone` struct**: `Tag, StartBar, Top, Bot, Base, IsSupport, Mitigated, Hidden, RetestCount`
- **`RedrawZone`**: ancla borde izquierdo con `ago = CurrentBar - z.StartBar`; extiende derecho con `-300`; omite si `Hidden || Mitigated`
- **`EnforceVisibility`**: solo setea/limpia flag `Hidden` — no pelea con RedrawZone
- **`ZoneOpacity`** property (1–100, default 20) — controla opacidad del fill
- **`HandleStructure`**: evict por `StartBar` mínimo (más antiguo), no máximo

---

## CI/CD
- `.github/workflows/publish-nt8-package.yml`
- Push a `main` → copia `*.cs` → ZIP versionado desde `VERSION` → Release en GitHub
- El ZIP se llama `OrderFlow-Suite-NT8-v{VERSION}-Import.zip`

---

## Próximos pasos sugeridos (ideas, no compromisos)
- Panel de dashboard en `HeatMapFlow` mostrando estado de todos los motores activos
- Alerts configurables (email/push) cuando se dispara Triple-A Aggression o Kill Zone break
- Modo "Confluence Scanner" que combina señales de los 8 indicadores
