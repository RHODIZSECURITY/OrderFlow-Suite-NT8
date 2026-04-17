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
    [Gui.CategoryOrder("Order Flow", 1)]
    [Gui.CategoryOrder("Market Volume", 2)]
    [Gui.CategoryOrder("Volume Analysis Profile", 3)]
    [Gui.CategoryOrder("Volume Filter", 4)]
    public class VolumeProfile : Indicator
    {
        private double _cumDelta;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "VolumeProfile";
                Description = "TradingView-style consolidated OrderFlow + MarketVolume + VolumeProfile + VolumeFilter.";
                Calculate = Calculate.OnBarClose;
                IsOverlay = false;
                MaxLookBack = MaximumBarsLookBack.Infinite;

                ShowOrderFlow = true;
                ShowMarketVolume = true;
                ShowProfile = true;
                ShowVolumeFilter = true;

                DeltaLength = 20;
                MinVolumeFactor = 1.5;
                UseCumulativeDelta = true;
                VolumeZScoreLen = 50;
                ZScoreThreshold = 1.0;

                AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.Bar, "Delta");
                AddPlot(new Stroke(Brushes.MediumPurple, 2), PlotStyle.Line, "RelativeVolume");
                AddPlot(new Stroke(Brushes.DarkOrange, 2), PlotStyle.Line, "FilteredVolume");
                AddPlot(new Stroke(Brushes.Gold, 2), PlotStyle.Line, "CumulativeDelta");
            }
            else if (State == State.DataLoaded)
            {
                _cumDelta = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(DeltaLength, VolumeZScoreLen))
            {
                Values[0][0] = Values[1][0] = Values[2][0] = Values[3][0] = double.NaN;
                return;
            }

            if (Bars.IsFirstBarOfSession)
                _cumDelta = 0;

            double upVol = Close[0] >= Open[0] ? Volume[0] : 0;
            double dnVol = Close[0] < Open[0] ? Volume[0] : 0;
            double delta = upVol - dnVol;
            _cumDelta += delta;

            double avgVol = SMA(Volume, DeltaLength)[0];
            double rv = avgVol > 0 ? Volume[0] / avgVol : 0;

            double sigma = StdDev(Volume, VolumeZScoreLen)[0];
            double mean = SMA(Volume, VolumeZScoreLen)[0];
            double z = sigma > 0 ? (Volume[0] - mean) / sigma : 0;
            double fVol = (rv >= MinVolumeFactor && z >= ZScoreThreshold) ? Volume[0] : 0;

            Values[0][0] = ShowOrderFlow ? delta : double.NaN;
            Values[1][0] = ShowMarketVolume || ShowProfile ? rv : double.NaN;
            Values[2][0] = ShowVolumeFilter ? fVol : double.NaN;
            Values[3][0] = (ShowOrderFlow && UseCumulativeDelta) ? _cumDelta : double.NaN;
        }

        [NinjaScriptProperty, Display(Name = "Show Order Flow", GroupName = "Order Flow", Order = 1)]
        public bool ShowOrderFlow { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Market Volume", GroupName = "Market Volume", Order = 1)]
        public bool ShowMarketVolume { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Profile", GroupName = "Volume Analysis Profile", Order = 1)]
        public bool ShowProfile { get; set; }

        [NinjaScriptProperty, Display(Name = "Show Volume Filter", GroupName = "Volume Filter", Order = 1)]
        public bool ShowVolumeFilter { get; set; }

        [NinjaScriptProperty, Range(1, 500), Display(Name = "Delta Length", GroupName = "Order Flow", Order = 2)]
        public int DeltaLength { get; set; }

        [NinjaScriptProperty, Display(Name = "Use Cumulative Delta", GroupName = "Order Flow", Order = 3)]
        public bool UseCumulativeDelta { get; set; }

        [NinjaScriptProperty, Range(0.1, 20.0), Display(Name = "Min Volume Factor", GroupName = "Volume Filter", Order = 2)]
        public double MinVolumeFactor { get; set; }

        [NinjaScriptProperty, Range(5, 500), Display(Name = "Volume ZScore Length", GroupName = "Volume Filter", Order = 3)]
        public int VolumeZScoreLen { get; set; }

        [NinjaScriptProperty, Range(-5.0, 10.0), Display(Name = "ZScore Threshold", GroupName = "Volume Filter", Order = 4)]
        public double ZScoreThreshold { get; set; }

        [Browsable(false)]
        public Series<double> Delta => Values[0];

        [Browsable(false)]
        public Series<double> RelativeVolume => Values[1];

        [Browsable(false)]
        public Series<double> FilteredVolume => Values[2];

        [Browsable(false)]
        public Series<double> CumulativeDelta => Values[3];
    }
}
