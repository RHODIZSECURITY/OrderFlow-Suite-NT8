# Resumen — OrderFlow Suite NT8 v1.1.0

Suite de indicadores de **order flow y análisis de volumen** para **NinjaTrader 8.1**, convertida y adaptada desde Pine Script (TradingView) con foco en futuros **NQ** y **ES**.

---

## Indicadores incluidos

### Volumen y Order Flow

| Indicador | Descripción |
|---|---|
| `OrderFlow.cs` | Lectura de flujo de órdenes en tiempo real (bid/ask delta, imbalances) |
| `VolumeAnalysisProfile.cs` | Perfil de volumen por sesión (POC, VAH, VAL) |
| `VolumeFilter.cs` | Filtro de volumen: destaca velas con volumen superior al umbral configurado |
| `MarketVolume.cs` | Volumen de mercado acumulado con histograma y alertas |
| `BigTrades.cs` | Burbujas visuales para operaciones de gran tamaño; ajustable por tamaño mínimo de trade |
| `Bookmap.cs` | Visualización de book de órdenes estilo Bookmap (heatmap de liquidez) |

### Niveles y Zonas

| Indicador | Descripción |
|---|---|
| `PreviousDayLevels.cs` | Niveles del día anterior: High, Low, Close, Open y VWAP |
| `NYPreMarketLevels.cs` | Niveles pre-mercado de Nueva York (incluyendo VWAP overnight) |
| `SupportResistance.cs` | Zonas de soporte y resistencia dinámicas basadas en ATR |
| `OrderBlocks.cs` | Detección de Order Blocks (bloques institucionales de órdenes) |
| `FairValueGaps.cs` | Fair Value Gaps (FVGs): brechas de precio sin cubrir entre velas |
| `PremiumDiscountZones.cs` | Zonas Premium/Equilibrio/Descuento basadas en rango de swing con lógica Fibonacci |

### Estructura de Mercado y Liquidez

| Indicador | Descripción |
|---|---|
| `MarketStructure.cs` | Detecta BOS (Break of Structure), CHoCH (Change of Character) y MSS (Market Structure Shift) |
| `LiquiditySuite.cs` | Pools de liquidez (swing highs/lows sin testear) y detección de sweep (barridas) |
| `ORBPro.cs` | Opening Range Breakout con tres modos de entrada: Break, Trap y Reversal |

### Series y Tendencia

| Indicador | Descripción |
|---|---|
| `TrendSeries.cs` | Consolidación de MASeries + BollingerBandsPro: hasta 4 EMAs, 5 SMAs y Bandas de Bollinger configurables |
| `TripleA.cs` | Triple A (Aggressive, Average, Attentive): sistema de señales de entrada basado en SMC/ICT |
| `SessionVWAP.cs` | VWAP de sesión con desviaciones estándar (SD1, SD2, SD3) |
| `SessionGap.cs` | Detección de gaps de sesión (apertura vs. cierre anterior) |

---

## Scripts TradingView (Pine Script v6)

| Script | Descripción |
|---|---|
| `OrderFlowScalperPro.pine` | Indicador de order flow y scalping para TradingView |
| `SMC_ICT_Suite_Pro_*.pine` | Suite SMC/ICT completa: Order Blocks, FVGs, BOS, CHoCH, Liquidity, ORB, Premium/Discount |

---

## AddOns requeridos

| Archivo | Descripción |
|---|---|
| `AddOns/SE.cs` | Librería de cálculo y matemáticas (requerida por todos los indicadores) |
| `AddOns/OrderFlow-Suite.cs` | Capa de renderizado SharpDX (requerida para dibujo en gráficos) |

---

## Evolución de versiones

| Versión | Cambios principales |
|---|---|
| v1.0.2 | Migración inicial de indicadores + CI/CD versionado |
| v1.0.3 | Auditoría de código, integración TradingView, CI mejorado |
| v1.0.4 | +TripleA, SessionGap, SessionVWAP, MASeries, BollingerBandsPro |
| v1.0.5 | +9 indicadores convertidos desde Pine Script (LiquiditySuite, MarketStructure, ORBPro, PremiumDiscountZones, etc.) |
| v1.0.6 | Reescritura de 5 indicadores con lógica exacta del Pine Script original |
| **v1.1.0** | Arquitectura modular: MASeries + BollingerBandsPro → `TrendSeries`; workflow unificado |

---

## Instalación rápida

1. En NinjaTrader: **Control Center → Tools → Import → NinjaScript Add-On...**
2. Selecciona el ZIP del release (ej. `OrderFlow-Suite-NT8-v1.1.0-Import.zip`).
3. Reinicia NinjaTrader y compila con `F5`.

> **Requisito:** Activar **Tick Replay** en cada gráfico (**Data Series → Tick Replay**) para obtener datos correctos de volumen y order flow.

---

## Configuración recomendada — Big Trades (NQ/ES)

| Parámetro | NQ | ES |
|---|---|---|
| Min trade size | 20–50 | 50–150 |
| Min bubble (ticks) | 2 | 2 |
| Max bubble (ticks) | 12–18 | 12–18 |
| Volume scale | 25 | 40 |
| Bubble opacity % | 55 | 55 |

---

## Estructura del repositorio

```
.
├── *.cs                        # Indicadores NT8 (raíz)
├── AddOns/                     # Librerías requeridas + scripts Pine
├── dist/
│   ├── NT8_Import_Package/     # Paquete ZIP para importar en NT8
│   ├── SourceOnly/             # Archivos .cs para copia manual
│   └── *.zip                   # ZIPs versionados (generados por CI)
└── .github/workflows/
    └── publish-nt8-package.yml # CI: genera releases automáticamente en cada push a main
```
