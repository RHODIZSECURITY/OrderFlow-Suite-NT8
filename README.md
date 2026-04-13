# OrderFlow Suite NT8.1 (Español)

> Última actualización: 2026-04-13

Suite de análisis de volumen y order flow para **NinjaTrader 8.1**, enfocada en **Futuros**, especialmente en **Nasdaq (NQ)** y **S&P 500 E-mini (ES)**.

## ¿Qué incluye?

Indicadores principales:

- `Bookmap.cs`
- `OrderFlow.cs`
- `VolumeAnalysisProfile.cs`
- `VolumeFilter.cs`
- `MarketVolume.cs`
- `BigTrades.cs`
- `PreviousDayLevels.cs`
- `NYPreMarketLevels.cs`
- `SupportResistance.cs`
- `OrderBlocks.cs`
- `FairValueGaps.cs`

AddOns requeridos:

- `AddOns/SE.cs`
- `AddOns/WyckoffRender.cs`

---

## Enfoque del Kit

Este kit está optimizado para lectura de flujo de órdenes en mercados de futuros con mayor liquidez intradía, con foco principal en:

- **NQ (Nasdaq-100 E-mini)**
- **ES (S&P 500 E-mini)**

También puede utilizarse en otros futuros compatibles con NinjaTrader, pero la configuración recomendada está pensada para NQ/ES.

---

## Instalación rápida (recomendada)

### Opción A: Importar ZIP (rápido)

1. En NinjaTrader abre: **Control Center → Tools → Import → NinjaScript Add-On...**
2. Selecciona: `OrderFlow-Suite-NT8.1-Import.zip`.
3. Reinicia NinjaTrader.
4. Compila en NinjaScript Editor (`F5`).

> El paquete ZIP principal se genera desde `dist/OrderFlow-Suite-NT8.1-Import.zip`.
> Si NinjaTrader muestra error de versión al importar, usa el paquete source-only (`dist/OrderFlow-Suite-NT8.1-SourceOnly.zip`) y compila con `F5`.

### Opción B: Instalación manual

1. Copia los archivos de `AddOns/` a:
   `Documents\NinjaTrader 8\bin\Custom\AddOns\`
2. Copia los indicadores (`Bookmap.cs`, `OrderFlow.cs`, etc.) a:
   `Documents\NinjaTrader 8\bin\Custom\Indicators\`
3. Abre NinjaScript Editor y compila (`F5`).

### Opción C: Source-Only (máxima compatibilidad entre builds)

1. Descarga `OrderFlow-Suite-NT8.1-SourceOnly.zip`.
2. Copia `SourceOnly/AddOns/*.cs` en `Documents\NinjaTrader 8\bin\Custom\AddOns\`.
3. Copia `SourceOnly/Indicators/*.cs` en `Documents\NinjaTrader 8\bin\Custom\Indicators\`.
4. Compila en NinjaScript Editor (`F5`).

---

## Configuración obligatoria

Activa **Tick Replay**:

1. **Tools → Options → Market Data → Show Tick Replay**.
2. En cada chart: **Data Series → Tick Replay**.

Sin Tick Replay, parte del cálculo de volumen/order flow puede no mostrarse correctamente.

---

## Uso básico

En un gráfico:

1. Clic derecho → **Indicators**.
2. Agrega alguno de estos:
   - **Book Map**
   - **Order Flow**
   - **Volume Analysis Profile**
   - **Volume Filter**
   - **Market Volume**
   - **Big Trades**
   - **Previous Day Levels**
   - **NY PreMarket Levels**
   - **Support Resistance**
   - **Order Blocks**
   - **Fair Value Gaps**

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

- `AddOns/` → utilidades internas de cálculo/render.
- `*.cs` (raíz) → indicadores principales.
- `dist/NT8_Import_Package/` → paquete listo para importación.
- `dist/OrderFlow-Suite-NT8.1-Import.zip` → ZIP principal para **Import NinjaScript Add-On**.
- `dist/OrderFlow-Suite-NT8.1-SourceOnly.zip` → ZIP source-only para copiar/pegar y compilar (evita problemas de versión de import).
- `dist/WyckoffZen-NT8-Import.zip` → ZIP legado (compatibilidad).
- `.github/workflows/publish-nt8-package.yml` → publica automáticamente ZIP en **Releases** y paquete en **GitHub Packages (GHCR)**.

---

## Nota

Este proyecto está enfocado a análisis de mercado de **futuros (NQ/ES)** y requiere correcta configuración de datos/tick replay en NinjaTrader para resultados consistentes.
