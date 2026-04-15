// Última actualización: 2026-04-14

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class PreviousDayLevels : Indicator
    {
        private double _prevHigh, _prevLow;
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Previous day High and Low with optional mid-point.";
                Name = "Previous Day Levels";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                ShowLabels = true; ShowMidPoint = false;
                HighColor = Brushes.LimeGreen; LowColor = Brushes.OrangeRed; MidColor = Brushes.DimGray;
            }
            else if (State == State.Configure) { AddDataSeries(BarsPeriodType.Day, 1); }
        }
        protected override void OnBarUpdate()
        {
            if (CurrentBars[0] < 1 || CurrentBars[1] < 2) return;
            if (BarsInProgress == 1) { _prevHigh = Highs[1][1]; _prevLow = Lows[1][1]; return; }
            if (_prevHigh <= 0 || _prevLow <= 0) return;
            Draw.HorizontalLine(this, "PDH", _prevHigh, HighColor, DashStyleHelper.Solid, 2);
            Draw.HorizontalLine(this, "PDL", _prevLow,  LowColor,  DashStyleHelper.Solid, 2);
            if (ShowMidPoint)
            {
                double mid = (_prevHigh + _prevLow) / 2.0;
                Draw.HorizontalLine(this, "PDMid", mid, MidColor, DashStyleHelper.Dash, 1);
            }
            if (ShowLabels)
                Draw.TextFixed(this, "PDLevels", string.Format("PDH: {0:F2}  |  PDL: {1:F2}", _prevHigh, _prevLow), TextPosition.TopRight);
        }
        #region Properties
        [NinjaScriptProperty][Display(Name="Show Labels",GroupName="Previous Day Levels",Order=0)] public bool ShowLabels { get; set; }
        [NinjaScriptProperty][Display(Name="Show Mid Point",GroupName="Previous Day Levels",Order=1)] public bool ShowMidPoint { get; set; }
        [XmlIgnore][Display(Name="High Color",GroupName="Previous Day Levels",Order=2)] public Brush HighColor { get; set; }
        [Browsable(false)] public string HighColorSerializable { get { return Serialize.BrushToString(HighColor); } set { HighColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Low Color",GroupName="Previous Day Levels",Order=3)] public Brush LowColor { get; set; }
        [Browsable(false)] public string LowColorSerializable { get { return Serialize.BrushToString(LowColor); } set { LowColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Mid Color",GroupName="Previous Day Levels",Order=4)] public Brush MidColor { get; set; }
        [Browsable(false)] public string MidColorSerializable { get { return Serialize.BrushToString(MidColor); } set { MidColor = Serialize.StringToBrush(value); } }
        #endregion
    }
}
#region NinjaScript generated code. Neither change nor remove.
namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private WyckoffZen.PreviousDayLevels[] cachePreviousDayLevels;
        public WyckoffZen.PreviousDayLevels PreviousDayLevels(bool showLabels, bool showMidPoint)
        { return PreviousDayLevels(Input, showLabels, showMidPoint); }
        public WyckoffZen.PreviousDayLevels PreviousDayLevels(ISeries<double> input, bool showLabels, bool showMidPoint)
        {
            if (cachePreviousDayLevels != null)
                for (int idx = 0; idx < cachePreviousDayLevels.Length; idx++)
                    if (cachePreviousDayLevels[idx] != null && cachePreviousDayLevels[idx].ShowLabels == showLabels && cachePreviousDayLevels[idx].ShowMidPoint == showMidPoint && cachePreviousDayLevels[idx].EqualsInput(input))
                        return cachePreviousDayLevels[idx];
            return CacheIndicator<WyckoffZen.PreviousDayLevels>(new WyckoffZen.PreviousDayLevels(){ ShowLabels = showLabels, ShowMidPoint = showMidPoint }, input, ref cachePreviousDayLevels);
        }
    }
}
namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.WyckoffZen.PreviousDayLevels PreviousDayLevels(bool showLabels, bool showMidPoint)
        { return indicator.PreviousDayLevels(Input, showLabels, showMidPoint); }
    }
}
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.WyckoffZen.PreviousDayLevels PreviousDayLevels(bool showLabels, bool showMidPoint)
        { return indicator.PreviousDayLevels(Input, showLabels, showMidPoint); }
    }
}
#endregion
