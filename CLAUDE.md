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
# 1. Crear el nuevo archivo consolidado (leer los originales primero)
# 2. Borrar los originales
git rm PreviousDayLevels.cs ORBPro.cs NYPreMarketLevels.cs SessionVWAP.cs SessionGap.cs
# 3. Añadir nuevo
git add LevelsSuite.cs
# 4. Commit
git commit -m "v1.1.0: consolidar X indicadores → NuevoArchivo"
# 5. Push (dispara CI)
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
