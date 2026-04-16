# OrderFlow Suite NT8.1 (Español)

> Última actualización: 2026-04-16 — v1.1.0

Suite de análisis de volumen y order flow para **NinjaTrader 8.1**, enfocada en **Futuros**, especialmente en **Nasdaq (NQ)** y **S&P 500 E-mini (ES)**.
Incluye también scripts **Pine para TradingView**.

## ¿Qué incluye?

### Indicadores NinjaTrader 8.1 (suite consolidada)

- `HeatMapFlow.cs`
- `LevelsSuite.cs`
- `OrderFlowSignals.cs`
- `SmartMoneyConcepts.cs`
- `StructureSuite.cs`
- `VolumeProfile.cs`
- `SupportResistance.cs`
- `TrendSeries.cs`

AddOns requeridos:

- `AddOns/SE.cs`
- `AddOns/WyckoffRender.cs`

### Scripts TradingView (Pine Script v6)

- `AddOns/OrderFlowScalperPro.pine` — Indicador de order flow y scalping para TradingView.
- `AddOns/SMC_ICT_Suite_Pro_v25_v10.11p_v30c_compile_fix_obstore_valuewhen.pine` — Suite SMC/ICT completa: Order Blocks, FVG, MSS, BOS, Liquidity, entre otros.

---

## Enfoque del Kit

Este kit está optimizado para lectura de flujo de órdenes en mercados de futuros con mayor liquidez intradía, con foco principal en:

- **NQ (Nasdaq-100 E-mini)**
- **ES (S&P 500 E-mini)**

También puede utilizarse en otros futuros compatibles con NinjaTrader, pero la configuración recomendada está pensada para NQ/ES.

---

## Instalación rápida NinjaTrader (recomendada)

### Opción A: Importar ZIP (rápido)

1. En NinjaTrader abre: **Control Center → Tools → Import → NinjaScript Add-On...**
2. Selecciona el ZIP versioned de los Releases, por ejemplo: `OrderFlow-Suite-NT8-v1.0.3-Import.zip`.
3. Reinicia NinjaTrader.
4. Compila en NinjaScript Editor (`F5`).

> Si NinjaTrader muestra error de versión al importar, usa el paquete source-only y compila con `F5`.

### Opción B: Instalación manual

1. Copia los archivos de `AddOns/` a:
   `Documents\NinjaTrader 8\bin\Custom\AddOns\`
2. Copia los indicadores (`HeatMapFlow.cs`, `LevelsSuite.cs`, etc.) a:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`
3. Abre NinjaScript Editor y compila (`F5`).

### Opción C: Source-Only (máxima compatibilidad entre builds)

1. Descarga `OrderFlow-Suite-NT8-v1.0.3-SourceOnly.zip`.
2. Copia `SourceOnly/AddOns/*.cs` en `Documents\NinjaTrader 8\bin\Custom\AddOns\`.
3. Copia `SourceOnly/Indicators/*.cs` en `Documents\NinjaTrader 8\bin\Custom\Indicators\`.
4. Compila en NinjaScript Editor (`F5`).

---

## Instalación scripts TradingView

1. Descarga `OrderFlow-Suite-TradingView-v1.0.3.zip` desde Releases.
2. En TradingView: abre **Pine Editor** → pega el contenido del archivo `.pine`.
3. Haz clic en **Add to chart**.

---

## Configuración obligatoria (NinjaTrader)

Activa **Tick Replay**:

1. **Tools → Options → Market Data → Show Tick Replay**.
2. En cada chart: **Data Series → Tick Replay**.

Sin Tick Replay, parte del cálculo de volumen/order flow puede no mostrarse correctamente.

---

## Uso básico

En un gráfico:

1. Clic derecho → **Indicators**.
2. Agrega alguno de estos:
   - **HeatMapFlow**
   - **LevelsSuite**
   - **OrderFlowSignals**
   - **SmartMoneyConcepts**
   - **StructureSuite**
   - **VolumeProfile**
   - **SupportResistance**
   - **TrendSeries**

### Preset recomendado NQ/ES (Big Trades)

- **NQ**: `Min trade size = 20 ~ 50`
- **ES**: `Min trade size = 50 ~ 150`
- `Min bubble (ticks) = 2`
- `Max bubble (ticks) = 12 ~ 18`
- `Volume scale = 25 (NQ) / 40 (ES)`
- `Bubble width (sec) = 2`
- `Bubble opacity % = 55`
- `Show size text = false` (actívalo solo si quieres ver el tamaño exacto encima de cada burbuja)

---

## Solución de problemas

### 1) No aparece el indicador

- Verifica que `AddOns/SE.cs` y `AddOns/WyckoffRender.cs` estén instalados.
- Recompila en NinjaScript Editor.

### 2) Error al compilar

- Revisa **NinjaScript Output** para ver el archivo y línea del error.
- Asegura que no haya duplicados de indicadores con el mismo nombre.

### 3) No se ve volumen/order flow en tiempo real

- Confirma Tick Replay activado.
- Verifica conexión de datos y permisos de mercado.

### 4) Import ZIP falla

- Reinicia NinjaTrader e intenta de nuevo.
- Haz backup de `Documents\NinjaTrader 8\bin\Custom` antes de reintentar.

---

## Estructura del repositorio

```
.
├── AddOns/
│   ├── SE.cs                          # Librería de cálculo/matemáticas (requerida)
│   ├── WyckoffRender.cs               # Capa de renderizado SharpDX (requerida)
│   ├── OrderFlowScalperPro.pine       # Script Pine TradingView
│   └── SMC_ICT_Suite_Pro_v25_*.pine   # Suite SMC/ICT Pine TradingView
├── *.cs (raíz)                        # Indicadores NT8 principales
├── dist/
│   ├── NT8_Import_Package/            # Paquete para Import NinjaScript Add-On
│   │   ├── Indicators/
│   │   ├── AddOns/
│   │   └── TradingView/               # Scripts Pine incluidos
│   ├── SourceOnly/                    # Copia manual de archivos .cs
│   │   ├── Indicators/
│   │   ├── AddOns/
│   │   └── TradingView/               # Scripts Pine incluidos
│   └── *.zip                          # ZIPs versionados (generados por CI)
└── .github/workflows/
    └── publish-nt8-package.yml        # CI: genera releases versionados automáticamente
```

---

## Versionado

Cada push a `main` genera automáticamente un nuevo release versionado (ej. `v1.0.3`).
La versión se controla desde el archivo `VERSION` en la raíz del repositorio.

---

## Nota

Este proyecto está enfocado a análisis de mercado de **futuros (NQ/ES)** y requiere correcta configuración de datos/tick replay en NinjaTrader para resultados consistentes.
