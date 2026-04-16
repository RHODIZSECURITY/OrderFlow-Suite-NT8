# OrderFlow Suite NT8 — Estado del Proyecto

## Contexto General
- **Proyecto**: NinjaTrader 8 C# indicators — conversión desde Pine Script
- **Rama activa**: `main` (única rama, no feature branches)
- **Versión actual**: 1.1.0 (en VERSION file)
- **CI/CD**: Push a main → GitHub Actions genera ZIP → crea release en GitHub

## Objetivo: Consolidar 20 indicadores → 8 archivos modulares

### COMPLETADOS ✅
| Archivo | Consolida | Commit |
|---------|-----------|--------|
| `TrendSeries.cs` | MASeries + BollingerBandsPro | `915a02a` |
| `SupportResistance.cs` | standalone, sin cambios | — |

### PENDIENTES (en orden)
| # | Archivo nuevo | Consolida (borrar estos) |
|---|---------------|--------------------------|
| 3 | `LevelsSuite.cs` | PreviousDayLevels.cs, ORBPro.cs, NYPreMarketLevels.cs, SessionVWAP.cs, SessionGap.cs |
| 4 | `SmartMoneyConcepts.cs` | FairValueGaps.cs, OrderBlocks.cs |
| 5 | `StructureSuite.cs` | MarketStructure.cs, LiquiditySuite.cs, PremiumDiscountZones.cs |
| 6 | `OrderFlowSignals.cs` | BigTrades.cs, TripleA.cs |
| 7 | `HeatMapFlow.cs` | Bookmap.cs (`git mv`, rename class + Name="HeatMapFlow") |
| 8 | `VolumeProfile.cs` | OrderFlow.cs, MarketVolume.cs, VolumeAnalysisProfile.cs, VolumeFilter.cs |

## Proceso por cada indicador
```bash
# 1. Crear el nuevo archivo consolidado
# 2. Borrar los originales
git rm PreviousDayLevels.cs ORBPro.cs NYPreMarketLevels.cs SessionVWAP.cs SessionGap.cs
# 3. Añadir nuevo
git add LevelsSuite.cs
# 4. Commit
git commit -m "v1.1.0: consolidar X indicadores → NuevoArchivo"
# 5. Push (dispara CI)
git push -u origin main
```

## Especificaciones LevelsSuite.cs (PRÓXIMO)
- **Consolida**: PreviousDayLevels + ORBPro + NYPreMarketLevels + SessionVWAP + SessionGap
- **Calculate**: `OnEachTick` (requerido por SessionVWAP y SessionGap)
- **AddDataSeries**: `BarsPeriodType.Day, 1` en `State.Configure` (para PDH/PDL)
- **BarsInProgress == 1**: actualiza `_prevHigh/_prevLow` y retorna
- **Plots** (4, de SessionVWAP): `OvernightHigh`, `OvernightLow`, `OvernightVWAP`, `SessionVWAPLine`
- **Grupos de propiedades**:
  1. `Previous Day Levels` — ShowPDH, ShowPDL, ShowPDMid, ShowPDLabels, colores PDH/PDL/Mid
  2. `NY Pre-Market Levels` — PmStart, PmEnd (HHmmss), ShowPMHigh, ShowPMVwap, ShowPMLabels, colores
  3. `Session VWAP` — ShowSessionVWAP, color (anclado a NY open 09:30)
  4. `ORB Pro` — OrbDuration (min), OrbCutoffHour, Enable Break/Trap/Reversal, Show Fill/Labels, colores
  5. `Session Gap` — GapMinAtr, ShowGapLine, ShowGapLabel, colores Up/Down/Filled
- **Helper**: `IsNYCash(DateTime t)` usa `TimeSpan` (no `h*100+m`)
- **Helper**: `IsInPM(int hhmmss)` compara vs `_pmStart/_pmEnd`
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.WyckoffZen`

## Especificaciones SmartMoneyConcepts.cs
- **Consolida**: FairValueGaps + OrderBlocks
- **Enums dentro del namespace**: `FvgQuality`, `ObRangeMode`, `ObOverlapMode`
- **Grupos**: `FVG Delta`, `Order Blocks`
- **Calculate**: `OnBarClose`

## Especificaciones StructureSuite.cs
- **Consolida**: MarketStructure + LiquiditySuite + PremiumDiscountZones
- **Grupos**: `Market Structure`, `Liquidity Suite`, `Premium/Discount Zones`
- **Plots**: 1 plot `Equilibrium` (de PremiumDiscountZones)
- **Calculate**: `OnBarClose`

## Especificaciones OrderFlowSignals.cs
- **Consolida**: BigTrades + TripleA
- **Calculate**: `OnEachTick` (BigTrades lo requiere)
- **Guard TripleA**: `if (!IsFirstTickOfBar) return;` para lógica de TripleA
- **OnMarketData**: preservar de BigTrades (para bubbles de tick)
- **Plots**: `PhaseLong`, `PhaseShort` (de TripleA)
- **Grupos**: `Big Trades — Bubbles`, `Absorption`, `Big Trades — Signals`, `Imbalances`, `TripleA`, `TripleA Visuals`, `Colors`

## Especificaciones HeatMapFlow.cs
- `git mv Bookmap.cs HeatMapFlow.cs`
- Cambiar `class Bookmap` → `class HeatMapFlow`
- Cambiar `Name = "Bookmap"` → `Name = "HeatMapFlow"`

## Especificaciones VolumeProfile.cs
- **Consolida**: OrderFlow + MarketVolume + VolumeAnalysisProfile + VolumeFilter
- Indicadores NT8-nativos (no tienen fuente Pine)
- Unificar con toggles por sección

## Archivos AddOns (NO tocar)
- `AddOns/SE.cs` — ya auditado (fix div/0 + Math.PI)
- `AddOns/OrderFlow-Suite.cs` — no modificar

## CI/CD — Workflow
- Archivo: `.github/workflows/publish-nt8-package.yml`
- Usa `cp *.cs` glob → copia todos los .cs raíz automáticamente
- Push a main → limpia dist/ → copia .cs → genera ZIP → actualiza release v1.1.0

## Reglas de desarrollo
- **Rama única**: solo `main`
- **Push después de cada indicador**
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.WyckoffZen`
- **Rangos seguros**: `[Range(1, 5000)]` con `Math.Min(5000, value)` clamp
- **NaN guards** en ATR, sqrt, divisiones
- `IsNYCash()` usa `TimeSpan` (no entero `h*100+m`)
