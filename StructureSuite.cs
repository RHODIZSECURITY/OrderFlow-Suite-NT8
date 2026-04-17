#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    [Gui.CategoryOrder("Market Structure", 1)]
    [Gui.CategoryOrder("Liquidity Suite", 2)]
    [Gui.CategoryOrder("Premium/Discount Zones", 3)]
    public class StructureSuite : Indicator
    {
        private double _lastSwingHigh;
        private double _lastSwingLow;
        private int _trend; // 1 bullish, -1 bearish, 0 neutral

        private double _prevPivotHigh = double.NaN;
        private double _prevPivotLow  = double.NaN;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "StructureSuite";
                Description = "Market Structure (BOS/CHoCH) + Liquidity Sweeps + Premium/Discount Zones + Internal Structure.";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                MaxLookBack = MaximumBarsLookBack.Infinite;

                SwingStrength = 5;
                ShowBos = true;
                ShowChoch = true;
                ShowLiquiditySweeps = true;
                ShowPremiumDiscount = true;
                ZoneOpacity = 12;
                ShowSwingDots = true;
                ShowEqhEql = true;
                EqTolerance = 3;
                ShowInternalStructure = false;
                InternalStrength = 2;

                AddPlot(new Stroke(Brushes.Gold, 2), PlotStyle.Line, "Equilibrium");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < SwingStrength + 2)
            {
                Values[0][0] = double.NaN;
                return;
            }

            _lastSwingHigh = MAX(High, SwingStrength)[1];
            _lastSwingLow = MIN(Low, SwingStrength)[1];
            double eq = (_lastSwingHigh + _lastSwingLow) * 0.5;

            Values[0][0] = ShowPremiumDiscount ? eq : double.NaN;

            DetectStructure(eq);
            DetectLiquiditySweeps(eq);
            DrawPremiumDiscount(eq);
            DetectPivotsAndEqhEql();
            DetectInternalStructure();
        }

        private void DetectStructure(double eq)
        {
            bool breakUp = Close[0] > _lastSwingHigh;
            bool breakDown = Close[0] < _lastSwingLow;

            if (ShowBos)
            {
                if (breakUp && _trend >= 0)
                {
                    _trend = 1;
                    Draw.Text(this, $"SS_BOS_U_{CurrentBar}", "BOS↑", 0, High[0] + TickSize * 4, Brushes.LimeGreen);
                }
                if (breakDown && _trend <= 0)
                {
                    _trend = -1;
                    Draw.Text(this, $"SS_BOS_D_{CurrentBar}", "BOS↓", 0, Low[0] - TickSize * 4, Brushes.IndianRed);
                }
            }

            if (ShowChoch)
            {
                if (breakUp && _trend < 0)
                {
                    _trend = 1;
                    Draw.Text(this, $"SS_CHOCH_U_{CurrentBar}", "CHoCH↑", 0, eq, Brushes.DeepSkyBlue);
                }
                if (breakDown && _trend > 0)
                {
                    _trend = -1;
                    Draw.Text(this, $"SS_CHOCH_D_{CurrentBar}", "CHoCH↓", 0, eq, Brushes.Magenta);
                }
            }
        }

        private void DetectLiquiditySweeps(double eq)
        {
            if (!ShowLiquiditySweeps)
                return;

            bool sweepHigh = High[0] > _lastSwingHigh && Close[0] < _lastSwingHigh;
            bool sweepLow = Low[0] < _lastSwingLow && Close[0] > _lastSwingLow;

            if (sweepHigh)
            {
                Draw.Dot(this, $"SS_LIQ_H_{CurrentBar}", false, 0, High[0], Brushes.Magenta);
                Draw.Text(this, $"SS_LIQ_H_TXT_{CurrentBar}", "Liquidity Sweep", 0, High[0] + TickSize * 2, Brushes.Magenta);
            }
            if (sweepLow)
            {
                Draw.Dot(this, $"SS_LIQ_L_{CurrentBar}", false, 0, Low[0], Brushes.DeepSkyBlue);
                Draw.Text(this, $"SS_LIQ_L_TXT_{CurrentBar}", "Liquidity Sweep", 0, Low[0] - TickSize * 2, Brushes.DeepSkyBlue);
            }
        }

        private void DrawPremiumDiscount(double eq)
        {
            if (!ShowPremiumDiscount)
                return;

            Draw.HorizontalLine(this, "SS_EQ", eq, Brushes.Gold);
            Draw.Region(this, "SS_PREMIUM", 0, 0, _lastSwingHigh, eq, Brushes.Transparent, Brushes.IndianRed, ZoneOpacity);
            Draw.Region(this, "SS_DISCOUNT", 0, 0, eq, _lastSwingLow, Brushes.Transparent, Brushes.LimeGreen, ZoneOpacity);
        }

        private void DetectInternalStructure()
        {
            if (!ShowInternalStructure || CurrentBar < InternalStrength * 2 + 2) return;
            if (InternalStrength >= SwingStrength) return;  // internal must be smaller than external

            double iHigh = MAX(High, InternalStrength)[1];
            double iLow  = MIN(Low,  InternalStrength)[1];

            bool iBreakUp   = Close[0] > iHigh;
            bool iBreakDown = Close[0] < iLow;

            if (iBreakUp && _trend > 0)
                Draw.Text(this, $"SS_iBOS_U_{CurrentBar}", "iBOS↑", 0, High[0] + TickSize * 2, Brushes.SpringGreen);
            if (iBreakDown && _trend < 0)
                Draw.Text(this, $"SS_iBOS_D_{CurrentBar}", "iBOS↓", 0, Low[0] - TickSize * 2, Brushes.Salmon);
            if (iBreakUp && _trend < 0)
                Draw.Text(this, $"SS_iCHoCH_U_{CurrentBar}", "iCHoCH↑", 0, High[0] + TickSize * 2, Brushes.Aquamarine);
            if (iBreakDown && _trend > 0)
                Draw.Text(this, $"SS_iCHoCH_D_{CurrentBar}", "iCHoCH↓", 0, Low[0] - TickSize * 2, Brushes.Violet);
        }

        private bool IsPivotHigh(int barsAgo)
        {
            if (CurrentBar < SwingStrength * 2 + 1) return false;
            double h = High[barsAgo];
            for (int i = 0; i < SwingStrength * 2 + 1; i++)
            {
                if (i == barsAgo) continue;
                if (High[i] >= h) return false;
            }
            return true;
        }

        private bool IsPivotLow(int barsAgo)
        {
            if (CurrentBar < SwingStrength * 2 + 1) return false;
            double l = Low[barsAgo];
            for (int i = 0; i < SwingStrength * 2 + 1; i++)
            {
                if (i == barsAgo) continue;
                if (Low[i] <= l) return false;
            }
            return true;
        }

        private void DetectPivotsAndEqhEql()
        {
            if (CurrentBar < SwingStrength * 2 + 2) return;
            double tol = EqTolerance * TickSize;

            if (IsPivotHigh(SwingStrength))
            {
                double ph = High[SwingStrength];
                if (ShowSwingDots)
                    Draw.Dot(this, $"SS_SH_{CurrentBar}", false, SwingStrength, ph, Brushes.IndianRed);

                if (ShowEqhEql && !double.IsNaN(_prevPivotHigh) && Math.Abs(ph - _prevPivotHigh) <= tol)
                    Draw.Text(this, $"SS_EQH_{CurrentBar}", "EQH", SwingStrength, ph + TickSize * 4, Brushes.OrangeRed);

                _prevPivotHigh = ph;
            }

            if (IsPivotLow(SwingStrength))
            {
                double pl = Low[SwingStrength];
                if (ShowSwingDots)
                    Draw.Dot(this, $"SS_SL_{CurrentBar}", false, SwingStrength, pl, Brushes.LimeGreen);

                if (ShowEqhEql && !double.IsNaN(_prevPivotLow) && Math.Abs(pl - _prevPivotLow) <= tol)
                    Draw.Text(this, $"SS_EQL_{CurrentBar}", "EQL", SwingStrength, pl - TickSize * 4, Brushes.DeepSkyBlue);

                _prevPivotLow = pl;
            }
        }

        [NinjaScriptProperty, Range(2, 100), Display(Name = "Swing Strength", GroupName = "Market Structure", Order = 1)]
        public int SwingStrength { get; set; }

        [NinjaScriptProperty, Display(Name = "Show BOS", GroupName = "Market Structure", Order = 2)]
        public bool ShowBos { get; set; }

        [NinjaScriptProperty, Display(Name = "Show CHoCH", GroupName = "Market Structure", Order = 3)]
        public bool ShowChoch { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Liquidity Sweeps", GroupName = "Liquidity Suite", Order = 1)]
        public bool ShowLiquiditySweeps { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Premium/Discount", GroupName = "Premium/Discount Zones", Order = 1)]
        public bool ShowPremiumDiscount { get; set; }

        [NinjaScriptProperty, Range(1, 60), Display(Name = "Zone Opacity", GroupName = "Premium/Discount Zones", Order = 2)]
        public int ZoneOpacity { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Swing Dots", GroupName = "Market Structure", Order = 4)]
        public bool ShowSwingDots { get; set; }

        [NinjaScriptProperty, Display(Name = "Show EQH/EQL", GroupName = "Market Structure", Order = 5)]
        public bool ShowEqhEql { get; set; }

        [NinjaScriptProperty, Range(1, 20), Display(Name = "EQ Tolerance (ticks)", GroupName = "Market Structure", Order = 6)]
        public int EqTolerance { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Internal Structure (iBOS/iCHoCH)", GroupName = "Market Structure", Order = 7)]
        public bool ShowInternalStructure { get; set; }

        [NinjaScriptProperty, Range(1, 20), Display(Name = "Internal Strength", GroupName = "Market Structure", Order = 8)]
        public int InternalStrength { get; set; }

        [Browsable(false)]
        public Series<double> Equilibrium => Values[0];

        [Browsable(false)]
        public int TrendState => _trend;

        [Browsable(false)]
        public double LastSwingHigh => _lastSwingHigh;

        [Browsable(false)]
        public double LastSwingLow => _lastSwingLow;

        [Browsable(false)]
        public bool IsBullishStructure => _trend > 0;
    }
}
