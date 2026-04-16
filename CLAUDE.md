# OrderFlow Suite NT8 — Estado del Proyecto

## Contexto General
- **Proyecto**: NinjaTrader 8 C# indicators — conversión desde Pine Script
- **Rama activa**: `main`
- **Versión actual**: 1.1.0 (en VERSION file)
- **CI/CD**: Push a main → GitHub Actions genera ZIP → crea release en GitHub

## Objetivo: Consolidar 20 indicadores → 8 archivos modulares ✅ COMPLETADO

### COMPLETADOS ✅
| # | Archivo | Consolida | Estado |
|---|---------|-----------|--------|
| 1 | `TrendSeries.cs` | MASeries + BollingerBandsPro | ✅ namespace WyckoffZen |
| 2 | `SupportResistance.cs` | standalone | ✅ |
| 3 | `LevelsSuite.cs` | PreviousDayLevels + ORBPro + NYPreMarketLevels + SessionVWAP + SessionGap | ✅ ORB trap/reversal/cutoff |
| 4 | `SmartMoneyConcepts.cs` | FairValueGaps + OrderBlocks | ✅ XmlSerialization fix |
| 5 | `StructureSuite.cs` | MarketStructure + LiquiditySuite + PremiumDiscountZones | ✅ |
| 6 | `OrderFlowSignals.cs` | BigTrades + TripleA | ✅ |
| 7 | `HeatMapFlow.cs` | Bookmap (rename) | ✅ namespace WyckoffZen |
| 8 | `VolumeProfile.cs` | OrderFlow + MarketVolume + VolumeAnalysisProfile + VolumeFilter | ✅ |

### AddOns (NO tocar)
- `AddOns/SE.cs` — auditado
- `AddOns/OrderFlow-Suite.cs` — no modificar

---

## Reglas de desarrollo — CRÍTICAS
- **Rama única**: solo `main`, nunca feature branches
- **Push después de cada cambio**
- **Namespace**: `NinjaTrader.NinjaScript.Indicators.WyckoffZen` (todos los indicadores)
- **Rangos**: `[Range(1, 5000)]` con `Math.Min(5000, value)` en setter
- **NaN guards**: siempre antes de ATR, sqrt, divisiones
- **IsNYCash()**: usa `TimeSpan`, NUNCA `h*100+m`
- **XmlIgnore**: siempre añadir `using System.Xml.Serialization;` cuando se use `[XmlIgnore]`

## CI/CD — Workflow
- `.github/workflows/publish-nt8-package.yml`
- `cp *.cs` glob → copia todos los .cs raíz automáticamente
- Push a main → limpia dist/ → copia .cs → genera ZIP → actualiza release v1.1.0

## Arquitectura final — 8 indicadores modulares

| Indicador | Grupos de propiedades | Calculate |
|-----------|----------------------|-----------|
| `TrendSeries` | EMA Series, SMA Series, Bollinger Bands | OnBarClose |
| `SupportResistance` | Pivots, Donchian, CSID | OnBarClose |
| `LevelsSuite` | Previous Day Levels, NY Pre-Market Levels, Session VWAP, ORB Pro, Session Gap | OnEachTick |
| `SmartMoneyConcepts` | FVG Delta, Order Blocks | OnBarClose |
| `StructureSuite` | Market Structure, Liquidity Suite, Premium/Discount Zones | OnBarClose |
| `OrderFlowSignals` | Big Trades Bubbles, Absorption, Big Trades Signals, Imbalances, TripleA, TripleA Visuals, Colors | OnEachTick |
| `HeatMapFlow` | Heat Map Calculation, Heat Map Filters, Heat Map Style, Cumulative Book | OnEachTick |
| `VolumeProfile` | Order Flow, Market Volume, Volume Analysis Profile, Volume Filter | OnBarClose |
