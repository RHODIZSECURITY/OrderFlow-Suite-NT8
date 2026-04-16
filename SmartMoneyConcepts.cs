#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum FvgQuality { Any, Displaced, Strong }
    public enum ObRangeMode { FullCandle, BodyOnly }
    public enum ObOverlapMode { Merge, KeepAll }

    [Gui.CategoryOrder("FVG Delta", 1)]
    [Gui.CategoryOrder("Order Blocks", 2)]
    public class SmartMoneyConcepts : Indicator
    {
        private struct Zone
        {
            public int StartBar;
            public double Top;
            public double Bottom;
            public bool Bull;
        }

        private readonly List<Zone> _fvgZones = new List<Zone>();
        private readonly List<Zone> _obZones = new List<Zone>();
        private double _lastFvgTop = double.NaN;
        private double _lastFvgBottom = double.NaN;
        private double _lastObTop = double.NaN;
        private double _lastObBottom = double.NaN;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "SmartMoneyConcepts";
                Description = "TradingView-style Fair Value Gaps + Order Blocks.";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                MaxLookBack = MaximumBarsLookBack.Infinite;

                FvgEnabled = true;
                MinFvgTicks = 2;
                QualityFilter = FvgQuality.Displaced;
                FvgExtendBars = 120;
                FvgOpacity = 20;
                FvgBullColor = Brushes.LimeGreen;
                FvgBearColor = Brushes.IndianRed;

                ObEnabled = true;
                PivotStrength = 3;
                RangeMode = ObRangeMode.FullCandle;
                OverlapMode = ObOverlapMode.Merge;
                ObExtendBars = 180;
                ObOpacity = 15;
                ShowObLabels = true;
                ObBullColor = Brushes.DodgerBlue;
                ObBearColor = Brushes.DarkOrange;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 10)
                return;

            if (FvgEnabled)
                ProcessFvg();

            if (ObEnabled)
                ProcessOrderBlocks();
        }

        private void ProcessFvg()
        {
            bool bullGap = Low[0] > High[2];
            bool bearGap = High[0] < Low[2];
            if (!bullGap && !bearGap)
                return;

            double top = bullGap ? Low[0] : Low[2];
            double bot = bullGap ? High[2] : High[0];
            double gapTicks = (top - bot) / TickSize;
            if (gapTicks < MinFvgTicks)
                return;

            double body = Math.Abs(Close[0] - Open[0]);
            double atr = ATR(14)[0];
            bool displaced = atr > 0 && body >= atr * 0.4;
            bool strong = atr > 0 && body >= atr * 0.7;

            if (QualityFilter == FvgQuality.Displaced && !displaced)
                return;
            if (QualityFilter == FvgQuality.Strong && !strong)
                return;

            Zone zone = new Zone { StartBar = CurrentBar, Top = top, Bottom = bot, Bull = bullGap };
            if (OverlapMode == ObOverlapMode.Merge)
                MergeZone(_fvgZones, zone);
            else
                _fvgZones.Add(zone);
            _lastFvgTop = zone.Top;
            _lastFvgBottom = zone.Bottom;

            Draw.Rectangle(this,
                $"SMC_FVG_{(bullGap ? "B" : "S")}_{CurrentBar}",
                false,
                0,
                top,
                -FvgExtendBars,
                bot,
                zone.Bull ? FvgBullColor : FvgBearColor,
                zone.Bull ? FvgBullColor : FvgBearColor,
                FvgOpacity);
        }

        private void ProcessOrderBlocks()
        {
            if (CurrentBar < PivotStrength + 2)
                return;

            // TV-like impulse candle check around previous bar.
            bool bullImpulse = Close[0] > Open[0] && Close[0] > High[1] && Open[1] > Close[1];
            bool bearImpulse = Close[0] < Open[0] && Close[0] < Low[1] && Open[1] < Close[1];
            if (!bullImpulse && !bearImpulse)
                return;

            int srcBar = 1;
            double hi = RangeMode == ObRangeMode.BodyOnly ? Math.Max(Open[srcBar], Close[srcBar]) : High[srcBar];
            double lo = RangeMode == ObRangeMode.BodyOnly ? Math.Min(Open[srcBar], Close[srcBar]) : Low[srcBar];

            Zone zone = new Zone { StartBar = CurrentBar, Top = hi, Bottom = lo, Bull = bullImpulse };
            if (OverlapMode == ObOverlapMode.Merge)
                MergeZone(_obZones, zone);
            else
                _obZones.Add(zone);
            _lastObTop = zone.Top;
            _lastObBottom = zone.Bottom;

            Draw.Rectangle(this,
                $"SMC_OB_{(bullImpulse ? "B" : "S")}_{CurrentBar}",
                false,
                1,
                hi,
                -ObExtendBars,
                lo,
                bullImpulse ? ObBullColor : ObBearColor,
                bullImpulse ? ObBullColor : ObBearColor,
                ObOpacity);

            if (ShowObLabels)
                Draw.Text(this, $"SMC_OB_LBL_{CurrentBar}", "OB", 0, (hi + lo) * 0.5, bullImpulse ? ObBullColor : ObBearColor);
        }

        private const int MaxZones = 300;

        private void MergeZone(List<Zone> zones, Zone incoming)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                Zone z = zones[i];
                bool overlaps = incoming.Top >= z.Bottom && incoming.Bottom <= z.Top;
                if (!overlaps || incoming.Bull != z.Bull) continue;

                z.Top      = Math.Max(z.Top, incoming.Top);
                z.Bottom   = Math.Min(z.Bottom, incoming.Bottom);
                z.StartBar = incoming.StartBar;
                zones[i]   = z;
                return;
            }
            // Enforce max zone limit — remove oldest if needed
            if (zones.Count >= MaxZones)
            {
                int oldestIdx = 0;
                for (int i = 1; i < zones.Count; i++)
                    if (zones[i].StartBar < zones[oldestIdx].StartBar) oldestIdx = i;
                zones.RemoveAt(oldestIdx);
            }
            zones.Add(incoming);
        }

        [NinjaScriptProperty, Display(Name = "FVG Enabled", GroupName = "FVG Delta", Order = 1)]
        public bool FvgEnabled { get; set; }

        [NinjaScriptProperty, Range(1, 100), Display(Name = "Min FVG Ticks", GroupName = "FVG Delta", Order = 2)]
        public int MinFvgTicks { get; set; }

        [NinjaScriptProperty, Display(Name = "Quality Filter", GroupName = "FVG Delta", Order = 3)]
        public FvgQuality QualityFilter { get; set; }

        [NinjaScriptProperty, Range(1, 1000), Display(Name = "FVG Extend Bars", GroupName = "FVG Delta", Order = 4)]
        public int FvgExtendBars { get; set; }

        [NinjaScriptProperty, Range(0, 100), Display(Name = "FVG Opacity", GroupName = "FVG Delta", Order = 5)]
        public int FvgOpacity { get; set; }

        [XmlIgnore, Display(Name = "FVG Bull Color", GroupName = "FVG Delta", Order = 6)]
        public Brush FvgBullColor { get; set; }
        [Browsable(false)]
        public string FvgBullColorSerializable { get => Serialize.BrushToString(FvgBullColor); set => FvgBullColor = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "FVG Bear Color", GroupName = "FVG Delta", Order = 7)]
        public Brush FvgBearColor { get; set; }
        [Browsable(false)]
        public string FvgBearColorSerializable { get => Serialize.BrushToString(FvgBearColor); set => FvgBearColor = Serialize.StringToBrush(value); }

        [NinjaScriptProperty, Display(Name = "OB Enabled", GroupName = "Order Blocks", Order = 1)]
        public bool ObEnabled { get; set; }

        [NinjaScriptProperty, Range(1, 20), Display(Name = "Pivot Strength", GroupName = "Order Blocks", Order = 2)]
        public int PivotStrength { get; set; }

        [NinjaScriptProperty, Display(Name = "Range Mode", GroupName = "Order Blocks", Order = 3)]
        public ObRangeMode RangeMode { get; set; }

        [NinjaScriptProperty, Display(Name = "Overlap Mode", GroupName = "Order Blocks", Order = 4)]
        public ObOverlapMode OverlapMode { get; set; }

        [NinjaScriptProperty, Range(1, 1000), Display(Name = "OB Extend Bars", GroupName = "Order Blocks", Order = 5)]
        public int ObExtendBars { get; set; }

        [NinjaScriptProperty, Range(0, 100), Display(Name = "OB Opacity", GroupName = "Order Blocks", Order = 6)]
        public int ObOpacity { get; set; }

        [NinjaScriptProperty, Display(Name = "Show OB Labels", GroupName = "Order Blocks", Order = 7)]
        public bool ShowObLabels { get; set; }

        [XmlIgnore, Display(Name = "OB Bull Color", GroupName = "Order Blocks", Order = 8)]
        public Brush ObBullColor { get; set; }
        [Browsable(false)]
        public string ObBullColorSerializable { get => Serialize.BrushToString(ObBullColor); set => ObBullColor = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "OB Bear Color", GroupName = "Order Blocks", Order = 9)]
        public Brush ObBearColor { get; set; }
        [Browsable(false)]
        public string ObBearColorSerializable { get => Serialize.BrushToString(ObBearColor); set => ObBearColor = Serialize.StringToBrush(value); }

        [Browsable(false)]
        public int ActiveFvgZoneCount => _fvgZones.Count;

        [Browsable(false)]
        public int ActiveObZoneCount => _obZones.Count;

        [Browsable(false)]
        public double LastFvgTop => _lastFvgTop;

        [Browsable(false)]
        public double LastFvgBottom => _lastFvgBottom;

        [Browsable(false)]
        public double LastObTop => _lastObTop;

        [Browsable(false)]
        public double LastObBottom => _lastObBottom;
    }
}
