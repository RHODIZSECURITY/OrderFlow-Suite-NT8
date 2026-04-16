#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    [Gui.CategoryOrder("Market Structure", 1)]
    [Gui.CategoryOrder("Liquidity Suite", 2)]
    [Gui.CategoryOrder("Premium/Discount Zones", 3)]
    public class StructureSuite : Indicator
    {
        private double _lastSwingHigh;
        private double _lastSwingLow;
        private int _trend; // 1 bullish, -1 bearish, 0 neutral

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "StructureSuite";
                Description = "TradingView-style MarketStructure + Liquidity + Premium/Discount Zones.";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;

                SwingStrength = 5;
                ShowBos = true;
                ShowChoch = true;
                ShowLiquiditySweeps = true;
                ShowPremiumDiscount = true;
                ZoneOpacity = 12;

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
