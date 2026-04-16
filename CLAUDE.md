# OrderFlow Suite NT8 — Estado del Proyecto

## ⚡ ACCIÓN INMEDIATA AL LEER ESTO
**Lee este archivo, verifica el git log, y sin preguntar nada empieza a crear el primer archivo PENDIENTE de la lista de abajo. Hazlo todo: crear archivo, git rm los originales, git add, git commit, git push. Luego sigue con el siguiente.**

## Contexto General
- **Proyecto**: NinjaTrader 8 C# indicators — conversión desde Pine Script
- **Rama activa**: `main` (única rama, no feature branches)
- **Versión actual**: 1.1.0 (en VERSION file)
- **CI/CD**: Push a main → GitHub Actions genera ZIP → crea release en GitHub

## Objetivo: Consolidar 20 indicadores → 8 archivos modulares

### COMPLETADOS ✅
| # | Archivo final | Estado |
|---|---------------|--------|
| 1 | `TrendSeries.cs` | consolidado |
| 2 | `SupportResistance.cs` | consolidado |
| 3 | `LevelsSuite.cs` | consolidado |
| 4 | `SmartMoneyConcepts.cs` | consolidado |
| 5 | `StructureSuite.cs` | consolidado |
| 6 | `OrderFlowSignals.cs` | consolidado |
| 7 | `HeatMapFlow.cs` | consolidado (rename de Bookmap) |
| 8 | `VolumeProfile.cs` | consolidado |

### Bitácora de iteraciones (paridad TradingView)
- 2026-04-16 — `LevelsSuite.cs`: mejora de lógica TV-style (pre-market VWAP en vivo, evaluación de gap sólo en apertura NY, ORB con break labels y reset estable por sesión).
- 2026-04-16 — `SmartMoneyConcepts.cs`: mejora de FVG/OB con filtros de calidad (Displaced/Strong), fusión opcional de zonas solapadas y parámetros visuales para acercar comportamiento a TradingView.
- 2026-04-16 — `SmartMoneyConcepts.cs`: exposición de estado para estrategias (`ActiveFvgZoneCount`, `ActiveObZoneCount`, `LastFvgTop/Bottom`, `LastObTop/Bottom`).
- 2026-04-16 — `StructureSuite.cs`: mejora de Market Structure TV-style (BOS + CHoCH), sweeps de liquidez con etiquetas y zonas premium/discount con opacidad configurable.
- 2026-04-16 — `StructureSuite.cs`: exposición de estado para estrategias (`TrendState`, `LastSwingHigh`, `LastSwingLow`, `IsBullishStructure`).
- 2026-04-16 — `OrderFlowSignals.cs`: mejora TV-style con filtros de absorción e imbalance, manteniendo señales TripleA en primer tick y bubbles de big prints.
- 2026-04-16 — `OrderFlowSignals.cs`: exposición de estado para estrategias (`LastAbsorption`, `LastAbsorptionPrice`, `LastBullImbalance`, `LastBearImbalance`).
- 2026-04-16 — `VolumeProfile.cs`: mejora TV-style con delta acumulado por sesión, filtro de volumen por z-score y salida separada para volumen relativo/filtrado.
- 2026-04-16 — `VolumeProfile.cs`: exposición de series (`Delta`, `RelativeVolume`, `FilteredVolume`, `CumulativeDelta`) para consumo directo en estrategias.
- 2026-04-16 — `HeatMapFlow.cs`: ajuste de identidad pública (`Name`/`Description`) para alinear rename Bookmap → HeatMapFlow sin romper compatibilidad interna.
- 2026-04-16 — `HeatMapFlow.cs`: normalización de labels/grupos de propiedades (`Book Map` → `Heat Map`) para UX consistente con el rename.
- 2026-04-16 — `HeatMapFlow.cs`: corrección de textos/typos visibles en panel de propiedades (`Aggressive`, `Cumulative`, `Heat map margin`) sin alterar contratos internos.
- 2026-04-16 — `README.md`: actualización de documentación pública a la suite consolidada (`HeatMapFlow`, `LevelsSuite`, `OrderFlowSignals`, `SmartMoneyConcepts`, `StructureSuite`, `VolumeProfile`, etc.).

### PENDIENTES (nueva fase)
1. **Validación de paridad TradingView** indicador por indicador (inputs, plots, señales y edge cases).
2. **Pruebas en NT8** en histórico + real-time (sessions NY, PM, gaps, imbalances).
3. **Ajustes finos** de defaults visuales y rendimiento.

## Proceso por cada ajuste a partir de ahora
```bash
# 1. Editar indicador + actualizar este CLAUDE.md en el mismo commit
# 2. Commit atómico con scope claro
git add <archivo_indicador>.cs CLAUDE.md
git commit -m "v1.1.0: ajuste <indicador> + update CLAUDE.md"
# 3. Push (dispara CI)
git push -u origin main
```

---

## Especificaciones LevelsSuite.cs (PRÓXIMO — leer los 5 .cs originales antes de escribir)

**Consolida**: PreviousDayLevels.cs + ORBPro.cs + NYPreMarketLevels.cs + SessionVWAP.cs + SessionGap.cs

### Estructura obligatoria
```csharp
// Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class LevelsSuite : Indicator { ... }
}
```

### OnStateChange — SetDefaults
- `Calculate = Calculate.OnEachTick`
- `IsSuspendedWhileInactive = true`
- 4 plots: `AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.HLine, "OvernightHigh")`
- 4 plots: OvernightHigh, OvernightLow, OvernightVWAP (PlotStyle.Line), SessionVWAPLine (PlotStyle.Line)

### OnStateChange — Configure
```csharp
AddDataSeries(BarsPeriodType.Day, 1);  // para PDH/PDL
```

### OnBarUpdate — primeras líneas obligatorias
```csharp
// Daily series handler
if (BarsInProgress == 1)
{
    if (CurrentBars[1] >= 2)
    {
        _prevHigh = Highs[1][1];
        _prevLow  = Lows[1][1];
    }
    return;
}
if (CurrentBars[0] < 1 || CurrentBars[1] < 2) return;
```

### Helpers obligatorios
```csharp
// CORRECTO — usar TimeSpan, NO h*100+m
private bool IsNYCash(DateTime t)
{
    TimeSpan ts = t.TimeOfDay;
    return ts >= new TimeSpan(9, 30, 0) && ts < new TimeSpan(16, 0, 0);
}

// PM check con HHmmss entero
private bool IsInPM(int hhmmss) => hhmmss >= _pmStart && hhmmss <= _pmEnd;
```

### Plots — asignación en OnBarUpdate
```csharp
Values[0][0] = (_showPMHigh && _pmReady) ? _pmHighFinal : double.NaN;
Values[1][0] = (_showPMHigh && _pmReady) ? _pmLowFinal  : double.NaN;
Values[2][0] = (_showPMVwap && !double.IsNaN(_pmVwapFinal)) ? _pmVwapFinal : double.NaN;
Values[3][0] = (_showSessionVWAP && _sessVwapDenom > 0)
               ? _sessVwapNumer / _sessVwapDenom : double.NaN;
```

### Grupos de propiedades (orden)
1. `Previous Day Levels` — ShowPDH, ShowPDL, ShowPDMid, ShowPDLabels + colores PDH/PDL/Mid
2. `NY Pre-Market Levels` — PmStart(40000), PmEnd(93000) HHmmss, ShowPMHigh, ShowPMVwap, ShowPMLabels + colores
3. `Session VWAP` — ShowSessionVWAP + color
4. `ORB Pro` — OrbDuration(5min), OrbCutoffHour(11), EnableBreak, EnableTrap, EnableReversal, ShowFill, ShowLabels + colores
5. `Session Gap` — GapMinAtr(0.5), ShowGapLine, ShowGapLabel + colores Up/Down/Filled

### Exponer para estrategias (al final de Properties)
```csharp
[Browsable(false)] [XmlIgnore] public Series<double> OvernightHigh    => Values[0];
[Browsable(false)] [XmlIgnore] public Series<double> OvernightLow     => Values[1];
[Browsable(false)] [XmlIgnore] public Series<double> OvernightVWAP    => Values[2];
[Browsable(false)] [XmlIgnore] public Series<double> SessionVWAPLine  => Values[3];
[Browsable(false)] public double PrevHigh  => _prevHigh;
[Browsable(false)] public double PrevLow   => _prevLow;
[Browsable(false)] public bool   OrbReady  => _orbReady;
[Browsable(false)] public double OrbHigh   => _orbHigh;
[Browsable(false)] public double OrbLow    => _orbLow;
[Browsable(false)] public bool   GapUp     => _gapUp;
[Browsable(false)] public bool   GapDown   => _gapDown;
[Browsable(false)] public bool   GapFilled => _gapFilled;
```

---

## Especificaciones SmartMoneyConcepts.cs
- **Lee**: FairValueGaps.cs y OrderBlocks.cs antes de escribir
- **Enums ANTES de la clase** (dentro del namespace):
  ```csharp
  public enum FvgQuality { Any, Displaced, Strong }
  public enum ObRangeMode { FullCandle, BodyOnly }
  public enum ObOverlapMode { Merge, KeepAll }
  ```
- **Grupos**: `FVG Delta`, `Order Blocks`
- `Calculate = Calculate.OnBarClose`

---

## Especificaciones StructureSuite.cs
- **Lee**: MarketStructure.cs, LiquiditySuite.cs, PremiumDiscountZones.cs
- **1 plot**: `Equilibrium` (de PremiumDiscountZones)
- **Grupos**: `Market Structure`, `Liquidity Suite`, `Premium/Discount Zones`
- `Calculate = Calculate.OnBarClose`

---

## Especificaciones OrderFlowSignals.cs
- **Lee**: BigTrades.cs, TripleA.cs antes de escribir
- `Calculate = Calculate.OnEachTick` (BigTrades lo requiere)
- **Guard TripleA en OnBarUpdate**:
  ```csharp
  // Lógica TripleA solo en primer tick de barra
  if (IsFirstTickOfBar) { /* lógica TripleA */ }
  ```
- **Preservar OnMarketData** de BigTrades (para bubbles de tick)
- **2 plots**: `PhaseLong`, `PhaseShort`
- **Grupos**: `Big Trades — Bubbles`, `Absorption`, `Big Trades — Signals`, `Imbalances`, `TripleA`, `TripleA Visuals`, `Colors`

---

## Especificaciones HeatMapFlow.cs
```bash
git mv Bookmap.cs HeatMapFlow.cs
# Luego editar el archivo:
# - "class Bookmap" → "class HeatMapFlow"
# - Name = "Bookmap" → Name = "HeatMapFlow"
git add HeatMapFlow.cs
git commit -m "v1.1.0: Bookmap → HeatMapFlow (rename)"
git push -u origin main
```

---

## Especificaciones VolumeProfile.cs
- **Lee**: OrderFlow.cs, MarketVolume.cs, VolumeAnalysisProfile.cs, VolumeFilter.cs
- Indicadores NT8-nativos (no Pine Script)
- Unificar con bool toggles por sección

---

## Archivos AddOns (NO tocar)
- `AddOns/SE.cs` — ya auditado
- `AddOns/OrderFlow-Suite.cs` — no modificar

## CI/CD — Workflow
- `.github/workflows/publish-nt8-package.yml`
- `cp *.cs` glob → copia todos los .cs raíz automáticamente
- Push a main → limpia dist/ → copia .cs → genera ZIP → actualiza release v1.1.0

## Reglas de desarrollo — CRÍTICAS
- **Rama única**: solo `main`, nunca feature branches
- **Push después de cada indicador** (no agrupar)
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.WyckoffZen`
- **Rangos**: `[Range(1, 5000)]` con `Math.Min(5000, value)` en setter
- **NaN guards**: siempre antes de ATR, sqrt, divisiones
- **IsNYCash()**: usa `TimeSpan`, NUNCA `h*100+m`
- **NO preguntar** — leer archivos originales y ejecutar directamente
