# OrderFlow Suite NT8 (Español)

> Última actualización: 2026-04-16 — v1.1.0

Suite de análisis de volumen y order flow para **NinjaTrader 8.1**, enfocada en **Futuros**, especialmente en **Nasdaq (NQ)** y **S&P 500 E-mini (ES)**.

---

## ¿Qué incluye?

### Indicadores NinjaTrader 8.1

**Volumen y Order Flow**
- `OrderFlow.cs` — Delta bid/ask en tiempo real
- `VolumeAnalysisProfile.cs` — Perfil de volumen (POC, VAH, VAL)
- `VolumeFilter.cs` — Filtro de velas por volumen
- `MarketVolume.cs` — Histograma de volumen acumulado
- `BigTrades.cs` — Burbujas visuales para trades de gran tamaño
- `Bookmap.cs` — Heatmap de liquidez estilo Bookmap

**Niveles y Zonas**
- `PreviousDayLevels.cs` — High, Low, Close, Open y VWAP del día anterior
- `NYPreMarketLevels.cs` — Niveles pre-mercado NY + VWAP overnight
- `SupportResistance.cs` — Zonas S/R dinámicas (ATR-based)
- `OrderBlocks.cs` — Order Blocks institucionales
- `FairValueGaps.cs` — Fair Value Gaps (FVGs)
- `PremiumDiscountZones.cs` — Zonas Premium/Equilibrio/Descuento (Fibonacci)

**Estructura de Mercado y Liquidez**
- `MarketStructure.cs` — BOS, CHoCH y MSS
- `LiquiditySuite.cs` — Pools de liquidez y detección de sweeps
- `ORBPro.cs` — Opening Range Breakout (Break / Trap / Reversal)

**Series y Tendencia**
- `TrendSeries.cs` — EMA ×4 + SMA ×5 + Bollinger Bands (todo en uno)
- `TripleA.cs` — Señales SMC/ICT: Absorción → Acumulación → Agresión
- `SessionVWAP.cs` — VWAP de sesión con SD1/SD2/SD3
- `SessionGap.cs` — Gaps de apertura vs. cierre anterior

**AddOns requeridos**
- `AddOns/SE.cs` — Librería de cálculo y matemáticas
- `AddOns/OrderFlow-Suite.cs` — Capa de renderizado SharpDX

---

## Enfoque del Kit

Optimizado para lectura de flujo de órdenes en futuros con alta liquidez intradía:

- **NQ (Nasdaq-100 E-mini)**
- **ES (S&P 500 E-mini)**

También compatible con otros futuros en NinjaTrader, pero la configuración recomendada está pensada para NQ/ES.

---

## Instalación rápida (recomendada)

1. Descarga el ZIP del último release: `OrderFlow-Suite-NT8-v1.1.0-Import.zip`
2. En NinjaTrader: **Control Center → Tools → Import → NinjaScript Add-On...**
3. Selecciona el ZIP descargado.
4. Reinicia NinjaTrader.
5. Compila en NinjaScript Editor (`F5`).

### Instalación manual (alternativa)

1. Copia `AddOns/SE.cs` y `AddOns/OrderFlow-Suite.cs` a:
   `Documents\NinjaTrader 8\bin\Custom\AddOns\`
2. Copia todos los `*.cs` de la raíz a:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`
3. Abre NinjaScript Editor y compila (`F5`).

---

## Configuración obligatoria (NinjaTrader)

Activa **Tick Replay** — requerido para order flow en tiempo real:

1. **Tools → Options → Market Data → Show Tick Replay**
2. En cada chart: **Data Series → Tick Replay**

Sin Tick Replay, el cálculo de volumen/order flow no se mostrará correctamente.

---

## Uso básico

En un gráfico de NinjaTrader:

1. Clic derecho → **Indicators**
2. Agrega los indicadores deseados (todos disponibles bajo el namespace `OrderFlow_Suite_RHODIZ_v1_0_0` o `WyckoffZen`)

### Preset recomendado NQ/ES — Big Trades

| Parámetro | NQ | ES |
|---|---|---|
| Min trade size | 20–50 | 50–150 |
| Min bubble (ticks) | 2 | 2 |
| Max bubble (ticks) | 12–18 | 12–18 |
| Volume scale | 25 | 40 |
| Bubble width (sec) | 2 | 2 |
| Bubble opacity % | 55 | 55 |
| Show size text | false | false |

---

## Solución de problemas

**1) No aparece el indicador**
- Verifica que `AddOns/SE.cs` y `AddOns/OrderFlow-Suite.cs` estén instalados.
- Recompila en NinjaScript Editor (`F5`).

**2) Error al compilar**
- Revisa **NinjaScript Output** para ver el archivo y línea del error.
- Asegura que no haya duplicados de indicadores con el mismo nombre.

**3) No se ve volumen/order flow en tiempo real**
- Confirma que Tick Replay esté activado en el chart activo.
- Verifica conexión de datos y permisos de mercado.

**4) Import ZIP falla**
- Reinicia NinjaTrader e intenta de nuevo.
- Haz backup de `Documents\NinjaTrader 8\bin\Custom` antes de reintentar.

---

## Estructura del repositorio

```
.
├── *.cs (raíz)                  # Indicadores NT8 principales
├── AddOns/
│   ├── SE.cs                    # Librería de cálculo/matemáticas (requerida)
│   └── OrderFlow-Suite.cs       # Capa de renderizado SharpDX (requerida)
├── dist/
│   ├── NT8_Import_Package/      # Paquete para Import NinjaScript Add-On
│   │   ├── Indicators/          # Sincronizado automáticamente por CI
│   │   └── AddOns/
│   └── *.zip                    # ZIPs versionados (generados por CI)
├── RESUMEN.md                   # Resumen detallado de indicadores y versiones
├── CLAUDE.md                    # Guía de desarrollo para Claude Code
└── .github/workflows/
    └── publish-nt8-package.yml  # CI: genera releases en cada push a main
```

---

## Versionado

Cada push a `main` genera automáticamente un nuevo release.
La versión se controla desde el archivo `VERSION` en la raíz del repositorio.

---

## Nota

Este proyecto está enfocado a análisis de mercado de **futuros (NQ/ES)** y requiere correcta configuración de Tick Replay en NinjaTrader para resultados consistentes.
