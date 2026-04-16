#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    [Gui.CategoryOrder("Big Trades — Bubbles", 1)]
    [Gui.CategoryOrder("Absorption", 2)]
    [Gui.CategoryOrder("Big Trades — Signals", 3)]
    [Gui.CategoryOrder("Imbalances", 4)]
    [Gui.CategoryOrder("TripleA", 5)]
    [Gui.CategoryOrder("TripleA Visuals", 6)]
    [Gui.CategoryOrder("Colors", 7)]
    public class OrderFlowSignals : Indicator
    {
        private double _avgVol;
        private bool _lastAbsorption;
        private bool _lastBullImbalance;
        private bool _lastBearImbalance;
        private double _lastAbsorptionPrice;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "OrderFlowSignals";
                Description = "TradingView-style BigTrades + TripleA signals with absorption/imbalance filters.";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;

                BigTradeMultiplier = 3.0;
                TripleALookback = 20;
                ShowBubbles = true;
                BigPrintSize = 50;

                EnableAbsorption = true;
                AbsorptionAtrFactor = 0.25;

                EnableImbalance = true;
                ImbalanceRatio = 1.5;

                AddPlot(new Stroke(Brushes.LimeGreen, 2), PlotStyle.TriangleUp, "PhaseLong");
                AddPlot(new Stroke(Brushes.IndianRed, 2), PlotStyle.TriangleDown, "PhaseShort");
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(20, TripleALookback))
            {
                Values[0][0] = double.NaN;
                Values[1][0] = double.NaN;
                return;
            }

            _avgVol = SMA(Volume, 20)[0];
            bool bigBull = Close[0] > Open[0] && Volume[0] > _avgVol * BigTradeMultiplier;
            bool bigBear = Close[0] < Open[0] && Volume[0] > _avgVol * BigTradeMultiplier;
            _lastAbsorption = false;
            _lastBullImbalance = false;
            _lastBearImbalance = false;
            _lastAbsorptionPrice = double.NaN;

            if (EnableAbsorption)
                DetectAbsorption();
            if (EnableImbalance)
                DetectImbalances();

            if (ShowBubbles && bigBull)
                Draw.Dot(this, $"OFS_BB_{CurrentBar}", false, 0, Low[0] - TickSize * 2, Brushes.LimeGreen);
            if (ShowBubbles && bigBear)
                Draw.Dot(this, $"OFS_BS_{CurrentBar}", false, 0, High[0] + TickSize * 2, Brushes.IndianRed);

            Values[0][0] = double.NaN;
            Values[1][0] = double.NaN;

            if (IsFirstTickOfBar)
            {
                double hh = MAX(High, TripleALookback)[1];
                double ll = MIN(Low, TripleALookback)[1];

                if (Close[0] > hh)
                    Values[0][0] = Low[0] - 2 * TickSize;
                else if (Close[0] < ll)
                    Values[1][0] = High[0] + 2 * TickSize;
            }
        }

        private void DetectAbsorption()
        {
            double atr = ATR(14)[0];
            if (atr <= 0) return;

            double move = High[0] - Low[0];
            bool highVolume = Volume[0] > _avgVol * BigTradeMultiplier;
            bool absorbed = highVolume && move < atr * AbsorptionAtrFactor;
            if (!absorbed) return;

            Brush c = Close[0] >= Open[0] ? Brushes.DeepSkyBlue : Brushes.Magenta;
            Draw.Text(this, $"OFS_ABS_{CurrentBar}", "ABS", 0, (High[0] + Low[0]) * 0.5, c);
            _lastAbsorption = true;
            _lastAbsorptionPrice = (High[0] + Low[0]) * 0.5;
        }

        private void DetectImbalances()
        {
            double range = High[0] - Low[0];
            if (range <= 0) return;

            double bodyTop = Math.Max(Open[0], Close[0]);
            double bodyBot = Math.Min(Open[0], Close[0]);
            double upper = High[0] - bodyTop;
            double lower = bodyBot - Low[0];

            bool bullImb = lower > upper * ImbalanceRatio;
            bool bearImb = upper > lower * ImbalanceRatio;
            _lastBullImbalance = bullImb;
            _lastBearImbalance = bearImb;

            if (bullImb)
                Draw.Dot(this, $"OFS_IMB_B_{CurrentBar}", false, 0, Low[0], Brushes.LimeGreen);
            if (bearImb)
                Draw.Dot(this, $"OFS_IMB_S_{CurrentBar}", false, 0, High[0], Brushes.IndianRed);
        }

        protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        {
            if (!ShowBubbles) return;
            if (marketDataUpdate.MarketDataType != MarketDataType.Last) return;
            if (marketDataUpdate.Volume <= 0) return;

            if (marketDataUpdate.Volume >= (long)Math.Max(1, BigPrintSize))
                Draw.Dot(this, $"OFS_TICK_{CurrentBar}_{Time[0].Ticks}", false, 0, marketDataUpdate.Price, Brushes.Gold);
        }

        [NinjaScriptProperty, Range(1.0, 20.0), Display(Name = "Big Trade Multiplier", GroupName = "Big Trades — Signals", Order = 1)]
        public double BigTradeMultiplier { get; set; }

        [NinjaScriptProperty, Range(1, 200), Display(Name = "TripleA Lookback", GroupName = "TripleA", Order = 1)]
        public int TripleALookback { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Bubbles", GroupName = "Big Trades — Bubbles", Order = 1)]
        public bool ShowBubbles { get; set; }

        [NinjaScriptProperty, Range(1, int.MaxValue), Display(Name = "Big Print Size", GroupName = "Big Trades — Bubbles", Order = 2)]
        public int BigPrintSize { get; set; }

        [NinjaScriptProperty, Display(Name = "Enable Absorption", GroupName = "Absorption", Order = 1)]
        public bool EnableAbsorption { get; set; }

        [NinjaScriptProperty, Range(0.05, 2.0), Display(Name = "Absorption ATR Factor", GroupName = "Absorption", Order = 2)]
        public double AbsorptionAtrFactor { get; set; }

        [NinjaScriptProperty, Display(Name = "Enable Imbalance", GroupName = "Imbalances", Order = 1)]
        public bool EnableImbalance { get; set; }

        [NinjaScriptProperty, Range(1.0, 5.0), Display(Name = "Imbalance Ratio", GroupName = "Imbalances", Order = 2)]
        public double ImbalanceRatio { get; set; }

        [Browsable(false)] public Series<double> PhaseLong => Values[0];
        [Browsable(false)] public Series<double> PhaseShort => Values[1];
        [Browsable(false)] public bool LastAbsorption => _lastAbsorption;
        [Browsable(false)] public double LastAbsorptionPrice => _lastAbsorptionPrice;
        [Browsable(false)] public bool LastBullImbalance => _lastBullImbalance;
        [Browsable(false)] public bool LastBearImbalance => _lastBearImbalance;
    }
}
