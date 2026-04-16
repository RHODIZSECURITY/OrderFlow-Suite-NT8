# OrderFlow Suite NT8 — Estado del Proyecto

## Contexto General
- **Proyecto**: NinjaTrader 8 C# indicators — conversión desde Pine Script
- **Rama activa**: `main`
- **Versión actual**: 1.1.0 (en VERSION file)
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ`
- **CI/CD**: Push a main → GitHub Actions genera ZIP → crea release en GitHub

## Objetivo: 8 indicadores modulares ✅ COMPLETADO

| # | Archivo | Contenido | Estado |
|---|---------|-----------|--------|
| 1 | `TrendSeries.cs` | MASeries + BollingerBandsPro | ✅ |
| 2 | `SupportResistance.cs` | Zonas S/R (Pivots/Donchian/CSID) | ✅ |
| 3 | `LevelsSuite.cs` | PDH/PDL · ORB Pro · NY PreMarket · Session VWAP · Session Gap | ✅ |
| 4 | `SmartMoneyConcepts.cs` | FVG Delta + Order Blocks (con límite de zonas) | ✅ |
| 5 | `StructureSuite.cs` | BOS/CHoCH · Liquidity Sweeps · Premium/Discount | ✅ |
| 6 | `OrderFlowSignals.cs` | Big Trades · Absorption · Stacked Imbalances · Triple-A · LVN Engine | ✅ |
| 7 | `HeatMapFlow.cs` | Heatmap de profundidad de mercado (ex-Bookmap) | ✅ |
| 8 | `VolumeProfile.cs` | Delta · RelVol · FilteredVol · CumulativeDelta | ✅ |

### AddOns (NO tocar)
- `AddOns/SE.cs`
- `AddOns/OrderFlow-Suite.cs`

---

## Reglas de desarrollo — CRÍTICAS
- **Rama única**: `main` — nunca feature branches
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ` (todos)
- **MaxLookBack**: `MaxLookBack = MaximumBarsLookBack.Infinite;` en SetDefaults de TODOS
- **XmlIgnore**: siempre con `using System.Xml.Serialization;` si se usa `[XmlIgnore]`
- **Brushes**: toda propiedad Brush necesita su par `*Serializable` con `Serialize.BrushToString`
- **Memory**: DrawObjects con tag único por barra; usar Queue con límite para bubbles/dots
- **NaN guards**: antes de ATR, sqrt, divisiones
- **IsNYCash()**: usa `TimeSpan`, NUNCA `h*100+m`

## Arquitectura — motores implementados

| Indicador | Motor clave | Pine ref |
|-----------|-------------|----------|
| `OrderFlowSignals` | Triple-A Phase Machine (None→Absorption→Accumulation→Aggression) | OrderFlowScalperPro.pine:665-752 |
| `OrderFlowSignals` | LVN Engine (seed→freshness→retest) | OrderFlowScalperPro.pine:645-663 |
| `OrderFlowSignals` | Stacked Imbalances (contador consecutivo) | OrderFlowScalperPro.pine:619-642 |
| `OrderFlowSignals` | Absorción direccional (Up/Down) | OrderFlowScalperPro.pine:602-603 |
| `SmartMoneyConcepts` | FVG quality filter (Displaced/Strong) | SMC_ICT_Suite_Pro.pine |
| `SmartMoneyConcepts` | OB impulse detection + merge/keepall | SMC_ICT_Suite_Pro.pine |
| `LevelsSuite` | ORB Pro (Break/Trap/Reversal + cutoff) | OrderFlowScalperPro.pine |
| `LevelsSuite` | Session Gap (futures time-gap + equities) | OrderFlowScalperPro.pine |
| `StructureSuite` | BOS/CHoCH + Liquidity Sweeps + Equilibrium | OrderFlowScalperPro.pine |

## CI/CD
- `.github/workflows/publish-nt8-package.yml`
- `cp *.cs` copia todos los indicadores a dist/
- Push a main → ZIP → release v1.1.0
