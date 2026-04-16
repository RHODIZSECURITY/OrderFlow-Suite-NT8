# CLAUDE.md — OrderFlow Suite NT8

Guía de referencia para Claude Code al trabajar en este repositorio.

## Descripción del proyecto

Suite de indicadores de **order flow y análisis de volumen** para **NinjaTrader 8.1** (C#), convertida desde Pine Script (TradingView). Foco en futuros **NQ** y **ES**.

- Versión actual: ver archivo `VERSION` en la raíz
- Namespace principal: `NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0`
- Namespace secundario (indicadores SMC/ICT): `NinjaTrader.NinjaScript.Indicators.WyckoffZen`

---

## Estructura del repositorio

```
.
├── *.cs                         # Indicadores NT8 (fuente principal)
├── AddOns/
│   ├── SE.cs                    # Librería matemática/cálculo (requerida)
│   └── OrderFlow-Suite.cs       # Capa de renderizado SharpDX (requerida)
│   # *.pine — scripts TradingView (privados, en .gitignore, no distribuidos)
├── dist/
│   ├── NT8_Import_Package/      # Paquete para importar en NT8
│   │   ├── Indicators/          # Sincronizado por CI (copia de *.cs raíz)
│   │   └── AddOns/              # SE.cs + OrderFlow-Suite.cs (copiado por CI)
│   └── *.zip                    # ZIPs versionados (generados por CI)
├── RESUMEN.md                   # Resumen detallado de indicadores y versiones
├── VERSION                      # Versión semántica (ej. 1.1.0); controla los releases
├── Dockerfile.package           # Imagen Docker para GHCR
└── .github/workflows/
    └── publish-nt8-package.yml  # CI: genera release en cada push a main
```

---

## Flujo de CI/CD

El pipeline se dispara en cada push a `main`:

1. Lee la versión desde `VERSION`
2. Sincroniza `*.cs` de la raíz → `dist/NT8_Import_Package/Indicators/`
3. Copia `AddOns/SE.cs` y `AddOns/OrderFlow-Suite.cs` → `dist/NT8_Import_Package/AddOns/`
4. Genera `dist/OrderFlow-Suite-NT8-v{VERSION}-Import.zip`
5. Borra releases anteriores y crea uno nuevo en GitHub
6. Publica imagen Docker en GHCR

**Para publicar un nuevo release:** incrementar `VERSION`, hacer commit y push a `main`.

---

## Convenciones de código

### Estructura de un indicador NT8

```csharp
#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

// Comentario: origen Pine Script (sección y líneas de referencia)

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
    public class NombreIndicador : Indicator
    {
        // Campos privados con prefijo _camelCase
        private double _miCampo;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name        = "Nombre del Indicador";
                Description = "Descripción breve";
                Calculate   = Calculate.OnBarClose; // o OnEachTick para order flow
                IsOverlay   = true;                 // false para panel separado
            }
            else if (State == State.Configure) { }
            else if (State == State.DataLoaded) { }
        }

        protected override void OnBarUpdate() { }

        // Propiedades públicas con atributos de UI
        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "Período", GroupName = "Configuración", Order = 1)]
        public int Periodo { get; set; }
    }
}
```

### Reglas de namespace

- Indicadores core: `OrderFlow_Suite_RHODIZ_v1_0_0`
- Indicadores SMC/ICT nuevos: `WyckoffZen`
- **No mezclar** namespaces dentro de un mismo archivo `.cs`

### Campos privados

- Prefijo `_` + camelCase: `_lastHigh`, `_orbReady`, `_trendBull`
- Structs internos en PascalCase sin prefijo

### Propiedades públicas

- PascalCase sin prefijo
- Siempre incluir `[NinjaScriptProperty]`, `[Range(...)]` y `[Display(...)]`
- Atributo `[Range]` **obligatorio** en períodos numéricos para evitar asignación de memoria descontrolada

---

## Indicadores y sus archivos

### Namespace `OrderFlow_Suite_RHODIZ_v1_0_0`

| Archivo | Clase | Descripción |
|---|---|---|
| `OrderFlow.cs` | `OrderFlow` | Delta bid/ask en tiempo real |
| `VolumeAnalysisProfile.cs` | `VolumeAnalysisProfile` | Perfil de volumen (POC, VAH, VAL) |
| `VolumeFilter.cs` | `VolumeFilter` | Filtro de velas por volumen |
| `MarketVolume.cs` | `MarketVolume` | Histograma de volumen acumulado |
| `BigTrades.cs` | `BigTrades` | Burbujas de trades grandes |
| `Bookmap.cs` | `Bookmap` | Heatmap de liquidez |
| `PreviousDayLevels.cs` | `PreviousDayLevels` | Niveles del día anterior |
| `NYPreMarketLevels.cs` | `NYPreMarketLevels` | Niveles pre-mercado NY + VWAP overnight |
| `SupportResistance.cs` | `SupportResistance` | Zonas S/R dinámicas (ATR-based) |
| `OrderBlocks.cs` | `OrderBlocks` | Order Blocks institucionales |
| `FairValueGaps.cs` | `FairValueGaps` | Fair Value Gaps (FVGs) |
| `TrendSeries.cs` | `TrendSeries` | EMA ×4 + SMA ×5 + Bollinger Bands |

### Namespace `WyckoffZen`

| Archivo | Clase | Descripción |
|---|---|---|
| `TripleA.cs` | `TripleA` | Señales SMC/ICT: Absorción→Acumulación→Agresión |
| `SessionVWAP.cs` | `SessionVWAP` | VWAP de sesión con SD1/SD2/SD3 |
| `SessionGap.cs` | `SessionGap` | Gaps de apertura vs. cierre anterior |
| `LiquiditySuite.cs` | `LiquiditySuite` | Pools de liquidez y detección de sweeps |
| `MarketStructure.cs` | `MarketStructure` | BOS, CHoCH y MSS |
| `ORBPro.cs` | `ORBPro` | Opening Range Breakout (Break/Trap/Reversal) |
| `PremiumDiscountZones.cs` | `PremiumDiscountZones` | Zonas Premium/Equilibrio/Descuento (Fibonacci) |

---

## AddOns requeridos

Todos los indicadores dependen de:

- **`AddOns/SE.cs`** — funciones matemáticas compartidas (`Math2.Percent()`, `Atan2inDeg()`, etc.)
- **`AddOns/OrderFlow-Suite.cs`** — capa SharpDX para renderizado en gráficos

Ambos archivos **deben estar instalados** antes de compilar cualquier indicador.

---

## Seguridad y validación

Reglas críticas aprendidas en auditorías anteriores:

1. **División por cero** — siempre verificar denominadores antes de dividir (`if (denom == 0) return;`)
2. **NaN guards** — comprobar `double.IsNaN(value)` antes de usar en `Math.Sqrt()` o cálculos encadenados
3. **Períodos EMA/SMA** — usar `[Range(1, 5000)]` y `Math.Min(5000, value)` en setters para evitar allocación descontrolada
4. **ATR NaN** — retornar early si `ATR(14)[0]` es NaN para evitar propagación en zonas S/R
5. **Tick Replay** — los indicadores de order flow requieren `Calculate = Calculate.OnEachTick` y Tick Replay activo en NT8

---

## Cómo agregar un nuevo indicador

1. Crear `NuevoIndicador.cs` en la raíz siguiendo la estructura de clase estándar
2. Usar el namespace correcto según el tipo (`OrderFlow_Suite_RHODIZ_v1_0_0` o `WyckoffZen`)
3. Incluir comentario de origen si se convierte desde Pine Script (archivo y líneas de referencia)
4. Agregar entrada en `RESUMEN.md` (tabla correspondiente)
5. La CI sincroniza automáticamente la raíz → `dist/NT8_Import_Package/Indicators/` en cada push a `main`
6. Incrementar `VERSION` para publicar el nuevo release

---

## Ramas de desarrollo

- `main` — rama estable; cada push dispara CI y genera release
- `claude/*` — ramas de trabajo de Claude Code (se mergean a `main` via PR)

---

## Archivos que NO se distribuyen

Los siguientes archivos están en `.gitignore` y no se incluyen en los ZIPs de release:

- `*.pine` — scripts TradingView (privados)
- Carpetas de imágenes (`book_map_imgs/`, `crypto_imgs/`, etc.)
- `how_install/`
