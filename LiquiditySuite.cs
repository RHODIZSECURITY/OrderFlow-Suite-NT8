#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

// Converted from SMC / ICT Suite Pro Pine Script
// Section: LIQUIDITY — swing high/low pools and sweep detection
// Liquidity pools = untested swing highs (sell-side) and swing lows (buy-side)
// Sweeps = price wicks above/below a pool then closes back

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class LiquiditySuite : Indicator
    {
        // ── Pool storage ──────────────────────────────────────────────────────
        private struct LiqPool
        {
            public double Price;
            public int    Bar;
            public bool   IsHigh;  // true = swing high pool (sell-side liq)
            public bool   Swept;
            public string Tag;
        }

        private readonly List<LiqPool> _pools = new List<LiqPool>();

        // ── Settings ──────────────────────────────────────────────────────────
        private int  _swingLen;
        private int  _maxPools;
        private bool _showPools, _showSweeps, _showLabels;
        private Brush _sellSideLiqColor;   // swing highs
        private Brush _buySideLiqColor;    // swing lows
        private Brush _sweepColor;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "LiquiditySuite";
                Description        = "Swing High/Low liquidity pools with sweep detection. Sell-side (above swing highs) and buy-side (below swing lows). Ported from SMC/ICT Suite Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                DisplayInDataBox   = false;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                _swingLen          = 5;
                _maxPools          = 20;
                _showPools         = true;
                _showSweeps        = true;
                _showLabels        = true;

                _sellSideLiqColor  = new SolidColorBrush(Color.FromArgb(200, 244,  67,  54)); // Red
                _buySideLiqColor   = new SolidColorBrush(Color.FromArgb(200,  76, 175,  79)); // Green
                _sweepColor        = new SolidColorBrush(Color.FromArgb(230, 255, 193,   7)); // Amber
            }
            else if (State == State.DataLoaded)
            {
                _pools.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < _swingLen * 2 + 1) return;

            // ── Detect new swing pivot ────────────────────────────────────────
            double ph = double.NaN;
            bool isPH = true;
            double pivH = High[_swingLen];
            for (int i = 0; i < _swingLen * 2 + 1; i++)
            {
                if (i == _swingLen) continue;
                if (High[i] >= pivH) { isPH = false; break; }
            }
            if (isPH) ph = pivH;

            double pl = double.NaN;
            bool isPL = true;
            double pivL = Low[_swingLen];
            for (int i = 0; i < _swingLen * 2 + 1; i++)
            {
                if (i == _swingLen) continue;
                if (Low[i] <= pivL) { isPL = false; break; }
            }
            if (isPL) pl = pivL;

            int confirmedBar = CurrentBar - _swingLen;

            // ── Add new pools ─────────────────────────────────────────────────
            if (!double.IsNaN(ph) && _showPools)
            {
                string tag = "LiqH_" + CurrentBar;
                _pools.Add(new LiqPool { Price = ph, Bar = confirmedBar, IsHigh = true, Swept = false, Tag = tag });
                Draw.HorizontalLine(this, tag, ph, _sellSideLiqColor, DashStyleHelper.Dot, 1);
                if (_showLabels)
                    Draw.Text(this, "LiqHTxt_" + CurrentBar, true, "SSL",
                        -(CurrentBar - confirmedBar), ph + TickSize * 2, 0,
                        _sellSideLiqColor, new SimpleFont("Arial", 7), TextAlignment.Left,
                        Brushes.Transparent, Brushes.Transparent, 0);

                // Trim excess
                while (_pools.Count > _maxPools * 2)
                    _pools.RemoveAt(0);
            }

            if (!double.IsNaN(pl) && _showPools)
            {
                string tag = "LiqL_" + CurrentBar;
                _pools.Add(new LiqPool { Price = pl, Bar = confirmedBar, IsHigh = false, Swept = false, Tag = tag });
                Draw.HorizontalLine(this, tag, pl, _buySideLiqColor, DashStyleHelper.Dot, 1);
                if (_showLabels)
                    Draw.Text(this, "LiqLTxt_" + CurrentBar, true, "BSL",
                        -(CurrentBar - confirmedBar), pl - TickSize * 3, 0,
                        _buySideLiqColor, new SimpleFont("Arial", 7), TextAlignment.Left,
                        Brushes.Transparent, Brushes.Transparent, 0);
            }

            // ── Sweep detection ───────────────────────────────────────────────
            if (_showSweeps)
            {
                for (int p = 0; p < _pools.Count; p++)
                {
                    LiqPool pool = _pools[p];
                    if (pool.Swept) continue;

                    // Sweep of sell-side liquidity (wick above swing high, close back below)
                    if (pool.IsHigh && High[0] > pool.Price && Close[0] < pool.Price)
                    {
                        LiqPool updated = pool;
                        updated.Swept = true;
                        _pools[p] = updated;

                        RemoveDrawObject(pool.Tag);

                        Draw.HorizontalLine(this, pool.Tag + "_swept", pool.Price, _sweepColor, DashStyleHelper.Dash, 1);
                        Draw.Text(this, "SweepH_" + CurrentBar, true, "Sweep",
                            0, High[0] + TickSize * 5, 0, _sweepColor,
                            new SimpleFont("Arial", 8), TextAlignment.Center,
                            Brushes.Transparent, Brushes.Transparent, 0);
                    }

                    // Sweep of buy-side liquidity (wick below swing low, close back above)
                    if (!pool.IsHigh && Low[0] < pool.Price && Close[0] > pool.Price)
                    {
                        LiqPool updated = pool;
                        updated.Swept = true;
                        _pools[p] = updated;

                        RemoveDrawObject(pool.Tag);

                        Draw.HorizontalLine(this, pool.Tag + "_swept", pool.Price, _sweepColor, DashStyleHelper.Dash, 1);
                        Draw.Text(this, "SweepL_" + CurrentBar, true, "Sweep",
                            0, Low[0] - TickSize * 5, 0, _sweepColor,
                            new SimpleFont("Arial", 8), TextAlignment.Center,
                            Brushes.Transparent, Brushes.Transparent, 0);
                    }
                }
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Swing Length", Order = 1, GroupName = "Liquidity Suite")]
        public int SwingLen { get => _swingLen; set => _swingLen = Math.Max(2, value); }

        [NinjaScriptProperty]
        [Range(5, 100)]
        [Display(Name = "Max Pools", Order = 2, GroupName = "Liquidity Suite")]
        public int MaxPools { get => _maxPools; set => _maxPools = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Pools", Order = 3, GroupName = "Liquidity Suite")]
        public bool ShowPools { get => _showPools; set => _showPools = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Sweeps", Order = 4, GroupName = "Liquidity Suite")]
        public bool ShowSweeps { get => _showSweeps; set => _showSweeps = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 5, GroupName = "Liquidity Suite")]
        public bool ShowLabels { get => _showLabels; set => _showLabels = value; }

        [XmlIgnore]
        [Display(Name = "Sell-Side Liq Color", Order = 6, GroupName = "Liquidity Suite")]
        public Brush SellSideLiqColor { get => _sellSideLiqColor; set => _sellSideLiqColor = value; }

        [Browsable(false)]
        public string SellSideLiqColorSerializable
        {
            get { return Serialize.BrushToString(_sellSideLiqColor); }
            set { _sellSideLiqColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Buy-Side Liq Color", Order = 7, GroupName = "Liquidity Suite")]
        public Brush BuySideLiqColor { get => _buySideLiqColor; set => _buySideLiqColor = value; }

        [Browsable(false)]
        public string BuySideLiqColorSerializable
        {
            get { return Serialize.BrushToString(_buySideLiqColor); }
            set { _buySideLiqColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Sweep Color", Order = 8, GroupName = "Liquidity Suite")]
        public Brush SweepColor { get => _sweepColor; set => _sweepColor = value; }

        [Browsable(false)]
        public string SweepColorSerializable
        {
            get { return Serialize.BrushToString(_sweepColor); }
            set { _sweepColor = Serialize.StringToBrush(value); }
        }

        #endregion
    }
}
