# OrderFlow Suite NT8 — v1.2.0

> Última actualización: 2026-04-17

Suite profesional de **Order Flow**, **Smart Money Concepts** y **análisis de volumen** para **NinjaTrader 8**, construida y calibrada para futuros de alta liquidez — **NQ (Nasdaq-100)** y **ES (S&P 500 E-mini)**.

Todos los motores están portados desde los indicadores más vendidos de TradingView (OrderFlow Scalper Pro · SMC/ICT Suite Pro) y reescritos en C# con lógica completa, gestión de memoria y validación de zonas.

---

## Indicadores (8 módulos)

| # | Archivo | Motores principales |
|---|---------|---------------------|
| 1 | `TrendSeries.cs` | EMA Series (×4) · SMA Series (×5) · Bollinger Bands Pro (SMA/EMA/WMA/VWMA, fill D2D) |
| 2 | `SupportResistance.cs` | Zonas S/R — Pivots / Donchian / CSID · Zone depth ATR · Mitigation · Sweep counter · Strength counter (retests) · Overlap merge/hide |
| 3 | `LevelsSuite.cs` | PDH/PDL · NY Pre-Market H/L/VWAP · Session VWAP + **Bandas ±1σ/±2σ** · ORB Pro (Break/Trap/Reversal) · Session Gap · **ICT Kill Zones** (Asia / London / NY AM) |
| 4 | `SmartMoneyConcepts.cs` | FVG Delta (quality filter) + **Mitigation** · Order Blocks + **Invalidation** · **Breaker Blocks** (OB flippeado) · Merge/KeepAll · MaxZones cap |
| 5 | `StructureSuite.cs` | BOS/CHoCH · **Internal Structure (iBOS/iCHoCH)** · Liquidity Sweeps · Premium/Discount · **EQH/EQL** · **Swing Dots** |
| 6 | `OrderFlowSignals.cs` | Big Trades (bid/ask clasificados) · Absorption · Stacked Imbalances · **Triple-A Phase Machine** · LVN Engine |
| 7 | `HeatMapFlow.cs` | Order book depth heatmap · Bid/Ask imbalance · Large order detection |
| 8 | `VolumeProfile.cs` | Delta · Relative Volume · Z-Score filter · Cumulative Delta · **POC / VAH / VAL** (value area 70%) |

### AddOns requeridos (no modificar)
- `AddOns/SE.cs`
- `AddOns/OrderFlow-Suite.cs`

---

## Motores Pro implementados

### SmartMoneyConcepts
- **FVG Mitigation** — zona se atenúa cuando precio cierra dentro del gap (zona visitada vs. fresca)
- **OB Invalidation** — Order Block se elimina cuando precio cierra al otro lado
- **Breaker Blocks** — OB invalidado flipea a BB de dirección opuesta (concepto ICT avanzado)
- Quality filter: `Displaced` (body ≥ 40% ATR) / `Strong` (body ≥ 70% ATR)

### StructureSuite
- **EQH/EQL** — Equal Highs/Lows con tolerancia en ticks (liquidez doble)
- **Swing Dots** — pivot exacto con `IsPivotHigh/Low` (comparación estricta)
- **Internal Structure** — `iBOS↑/↓` (break menor con tendencia) y `iCHoCH↑/↓` (reversión temprana)
- BOS / CHoCH externo sobre `SwingStrength` bars

### LevelsSuite
- **VWAP SD Bands** — ±1σ y ±2σ calculadas incrementalmente: `σ = √(Σvol·p²/Σvol − VWAP²)`
- **ICT Kill Zones** — Asia (20:00–00:00 ET) · London (02:00–05:00 ET) · NY AM (07:00–10:00 ET)
- ORB Pro: Break / Trap (fakeout) / Reversal (diamond)

### OrderFlowSignals
- **Triple-A Phase Machine** — `None → Absorption → Accumulation → Aggression` con timeout configurable
- **LVN Engine** — seed → freshness (60 bars) → retest proximity (ATR × 0.35)
- **Big prints clasificados** — bid/ask en tiempo real; print al ask = buy (verde), al bid = sell (rojo)

### VolumeProfile
- **POC** — precio con mayor volumen acumulado en la sesión
- **VAH / VAL** — Value Area al 70% (configurable), expansión bilateral desde POC
- `SortedList` por tick-rounding, reset por sesión o modo acumulativo (Running)

---

## Instalación en NinjaTrader 8

### Opción A — Import ZIP (recomendado)
1. **Control Center → Tools → Import → NinjaScript Add-On...**
2. Selecciona el ZIP del último Release (ej. `OrderFlow-Suite-NT8-v1.2.0-Import.zip`)
3. Reinicia NinjaTrader
4. Compila en NinjaScript Editor (`F5`)

### Opción B — Manual
1. Copia `AddOns/*.cs` → `Documents\NinjaTrader 8\bin\Custom\AddOns\`
2. Copia los `*.cs` de la raíz → `Documents\NinjaTrader 8\bin\Custom\Indicators\`
3. Compila con `F5`

> **Tick Replay obligatorio** para clasificación bid/ask en OrderFlowSignals:
> `Tools → Options → Market Data → Enable Tick Replay` + activarlo en el chart.

---

## Configuración recomendada NQ/ES

### OrderFlowSignals
| Parámetro | NQ | ES |
|-----------|----|----|
| Big Trade Multiplier | 3.0 | 3.0 |
| Big Print Min Size | 20–50 | 50–150 |
| Absorption ATR Factor | 0.25 | 0.25 |
| Stacked Min Count | 2 | 2 |
| Phase Timeout (bars) | 10 | 10 |

### SmartMoneyConcepts
| Parámetro | Valor sugerido |
|-----------|----------------|
| Quality Filter | Displaced |
| Min FVG Ticks | 2–4 |
| Pivot Strength (OB) | 3 |
| Show Breaker Blocks | ✓ |

### LevelsSuite
| Parámetro | Valor sugerido |
|-----------|----------------|
| ORB Duration | 5 min |
| ORB Cutoff Hour | 11 |
| Show VWAP Bands | ✓ |
| Kill Zones activas | Asia + London + NY AM |

---

## Solución de problemas

| Problema | Solución |
|----------|----------|
| Indicador no aparece | Verifica que `AddOns/SE.cs` y `AddOns/OrderFlow-Suite.cs` estén compilados |
| Error al compilar | Revisa **NinjaScript Output** → archivo y línea |
| Big prints todos verdes | Activa Tick Replay en el chart |
| POC/VAH/VAL no visible | Añade VolumeProfile al chart de precios (`DrawOnPricePanel = true`) |
| FVG/OB no desaparece | Correcto — zona activa hasta mitigación (FVG: toque) o invalidación (OB: cierre más allá) |

---

## Estructura del repositorio

```
.
├── *.cs                          # 8 indicadores NT8 (raíz)
├── AddOns/
│   ├── SE.cs                     # Librería matemáticas (requerida)
│   ├── OrderFlow-Suite.cs        # AddOn principal (requerido)
│   ├── OrderFlowScalperPro.pine  # Pine Script fuente (referencia)
│   └── SMC_ICT_Suite_Pro_v25_*.pine
├── dist/
│   └── NT8_Import_Package/       # Paquete generado por CI
├── .github/workflows/
│   └── publish-nt8-package.yml   # CI: push a main → ZIP → Release
├── VERSION                       # 1.2.0
└── CLAUDE.md                     # Instrucciones de desarrollo con IA
```

---

## CI/CD

Cada push a `main` genera automáticamente:
1. Copia `*.cs` → `dist/NT8_Import_Package/Indicators/`
2. ZIP versionado desde `VERSION`
3. Release en GitHub con el ZIP adjunto

---

## Namespace

```
NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
```
