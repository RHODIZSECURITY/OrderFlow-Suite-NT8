// Ultima actualizacion: 2026-04-14

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class FairValueGaps : Indicator
    {
        private struct FVGZone
        {
            public string Tag;
            public double Top, Bot;
            public bool IsBull;
            public bool Mitigated;
        }

        private readonly List<FVGZone> _zones = new List<FVGZone>();

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description    = "Fair Value Gaps (bullish & bearish) with mitigation tracking.";
                Name           = "Fair Value Gaps";
                Calculate      = Calculate.OnBarClose;
                IsOverlay      = true;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                ForwardBars       = 20;
                MaxZones          = 6;
                Opacity           = 20;
                ShowMitigated     = false;
                MitigatedOpacity  = 8;
                BullColor         = Brushes.Lime;
                BearColor         = Brushes.Red;
                MitigatedColor    = Brushes.DimGray;
            }
            else if (State == State.DataLoaded) { _zones.Clear(); }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 2) return;

            bool bullFvg = Low[0]  > High[2];
            bool bearFvg = High[0] < Low[2];

            if (bullFvg || bearFvg)
            {
                double top    = bullFvg ? Low[0]  : Low[2];
                double bot    = bullFvg ? High[2] : High[0];
                bool   isBull = bullFvg;
                string tag    = (isBull ? "FVG_B_" : "FVG_R_") + CurrentBar;
                if (_zones.Count >= MaxZones) _zones.RemoveAt(0);
                _zones.Add(new FVGZone { Tag = tag, Top = top, Bot = bot, IsBull = isBull });
                Brush fillBrush = isBull ? BullColor : BearColor;
                Draw.Rectangle(this, tag, false, 0, top, -ForwardBars, bot, fillBrush, fillBrush, Opacity);
            }

            for (int i = 0; i < _zones.Count; i++)
            {
                var z = _zones[i];
                if (z.Mitigated) continue;
                bool mitigated = z.IsBull ? Low[0] <= z.Bot : High[0] >= z.Top;
                if (mitigated)
                {
                    var updated = z; updated.Mitigated = true; _zones[i] = updated;
                    if (ShowMitigated)
                    {
                        Draw.Rectangle(this, z.Tag, false, 0, z.Top, -ForwardBars, z.Bot, MitigatedColor, MitigatedColor, MitigatedOpacity);
                        Draw.Text(this, z.Tag + "_Mit", false, 0, z.Bot - TickSize * 3, "MIT", 0, MitigatedColor,
                            new System.Windows.Media.FontFamily("Arial"), 7,
                            NinjaTrader.Gui.Chart.TextAlignment.Center, false, false, null, 0, 0);
                    }
                    else { RemoveDrawObject(z.Tag); }
                }
            }
        }

        #region Properties
        [NinjaScriptProperty][Range(1,200)][Display(Name="Forward bars",GroupName="FVG",Order=0)] public int ForwardBars { get; set; }
        [NinjaScriptProperty][Range(1,30)][Display(Name="Max zones",GroupName="FVG",Order=1)] public int MaxZones { get; set; }
        [NinjaScriptProperty][Range(0,100)][Display(Name="Opacity %",GroupName="FVG",Order=2)] public int Opacity { get; set; }
        [NinjaScriptProperty][Display(Name="Show Mitigated",GroupName="FVG",Order=3)] public bool ShowMitigated { get; set; }
        [NinjaScriptProperty][Range(0,100)][Display(Name="Mitigated Opacity %",GroupName="FVG",Order=4)] public int MitigatedOpacity { get; set; }
        [XmlIgnore][Display(Name="Bull color",GroupName="FVG",Order=5)] public Brush BullColor { get; set; }
        [Browsable(false)] public string BullColorSerializable { get { return Serialize.BrushToString(BullColor); } set { BullColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Bear color",GroupName="FVG",Order=6)] public Brush BearColor { get; set; }
        [Browsable(false)] public string BearColorSerializable { get { return Serialize.BrushToString(BearColor); } set { BearColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Mitigated color",GroupName="FVG",Order=7)] public Brush MitigatedColor { get; set; }
        [Browsable(false)] public string MitigatedColorSerializable { get { return Serialize.BrushToString(MitigatedColor); } set { MitigatedColor = Serialize.StringToBrush(value); } }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.
namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private WyckoffZen.FairValueGaps[] cacheFairValueGaps;
        public WyckoffZen.FairValueGaps FairValueGaps(int forwardBars, int maxZones, int opacity, bool showMitigated)
        { return FairValueGaps(Input, forwardBars, maxZones, opacity, showMitigated); }
        public WyckoffZen.FairValueGaps FairValueGaps(ISeries<double> input, int forwardBars, int maxZones, int opacity, bool showMitigated)
        {
            if (cacheFairValueGaps != null)
                for (int idx = 0; idx < cacheFairValueGaps.Length; idx++)
                    if (cacheFairValueGaps[idx] != null && cacheFairValueGaps[idx].ForwardBars == forwardBars && cacheFairValueGaps[idx].MaxZones == maxZones && cacheFairValueGaps[idx].Opacity == opacity && cacheFairValueGaps[idx].ShowMitigated == showMitigated && cacheFairValueGaps[idx].EqualsInput(input))
                        return cacheFairValueGaps[idx];
            return CacheIndicator<WyckoffZen.FairValueGaps>(new WyckoffZen.FairValueGaps(){ ForwardBars = forwardBars, MaxZones = maxZones, Opacity = opacity, ShowMitigated = showMitigated }, input, ref cacheFairValueGaps);
        }
    }
}
namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.WyckoffZen.FairValueGaps FairValueGaps(int forwardBars, int maxZones, int opacity, bool showMitigated)
        { return indicator.FairValueGaps(Input, forwardBars, maxZones, opacity, showMitigated); }
    }
}
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.WyckoffZen.FairValueGaps FairValueGaps(int forwardBars, int maxZones, int opacity, bool showMitigated)
        { return indicator.FairValueGaps(Input, forwardBars, maxZones, opacity, showMitigated); }
    }
}
#endregion
