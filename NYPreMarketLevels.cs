// Última actualización: 2026-04-14

#region Using declarations
using System;
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
    public class NYPreMarketLevels : Indicator
    {
        private DateTime _currentDate;
        private double   _preHigh, _preLow;
        private double   _pmVwapNumer, _pmVwapDenom, _pmVwap;
        private bool     _finalized;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description              = "Pre-market High / Low / VWAP for NY session (04:00-09:30 ET).";
                Name                     = "NY PreMarket Levels";
                Calculate                = Calculate.OnEachTick;
                IsOverlay                = true;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel         = true;
                ScaleJustification       = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                PreMarketStart = 40000;
                PreMarketEnd   = 93000;
                ShowHighLow   = true;
                ShowPMVwap    = true;
                HighColor     = Brushes.DeepSkyBlue;
                LowColor      = Brushes.MediumPurple;
                VwapColor     = Brushes.Orchid;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;
            if (_currentDate != Time[0].Date)
            {
                _currentDate = Time[0].Date;
                _preHigh     = double.MinValue;
                _preLow      = double.MaxValue;
                _pmVwapNumer = 0; _pmVwapDenom = 0;
                _pmVwap      = double.NaN;
                _finalized   = false;
            }
            int now = ToTime(Time[0]);
            if (now >= PreMarketStart && now <= PreMarketEnd)
            {
                _preHigh = Math.Max(_preHigh, High[0]);
                _preLow  = Math.Min(_preLow,  Low[0]);
                double hlc3 = (High[0] + Low[0] + Close[0]) / 3.0;
                _pmVwapNumer += hlc3 * Volume[0];
                _pmVwapDenom += Volume[0];
                _pmVwap = _pmVwapDenom > 0 ? _pmVwapNumer / _pmVwapDenom : double.NaN;
            }
            else if (now > PreMarketEnd) { _finalized = true; }
            if (_preHigh == double.MinValue || _preLow == double.MaxValue) return;
            string dateKey = _currentDate.ToString("yyyyMMdd");
            if (ShowHighLow)
            {
                Draw.HorizontalLine(this, "NYPMH_" + dateKey, _preHigh, HighColor, DashStyleHelper.Dash, 1);
                Draw.HorizontalLine(this, "NYPML_" + dateKey, _preLow,  LowColor,  DashStyleHelper.Dash, 1);
            }
            if (ShowPMVwap && !double.IsNaN(_pmVwap))
                Draw.HorizontalLine(this, "NYPVWAP_" + dateKey, _pmVwap, VwapColor, DashStyleHelper.Dot, 1);
        }

        #region Properties
        [NinjaScriptProperty][Range(0,235959)][Display(Name="PreMarket Start (HHmmss)",GroupName="NY PreMarket",Order=0)] public int PreMarketStart { get; set; }
        [NinjaScriptProperty][Range(0,235959)][Display(Name="PreMarket End (HHmmss)",GroupName="NY PreMarket",Order=1)] public int PreMarketEnd { get; set; }
        [NinjaScriptProperty][Display(Name="Show High / Low",GroupName="NY PreMarket",Order=2)] public bool ShowHighLow { get; set; }
        [NinjaScriptProperty][Display(Name="Show PM VWAP",GroupName="NY PreMarket",Order=3)] public bool ShowPMVwap { get; set; }
        [XmlIgnore][Display(Name="High Color",GroupName="NY PreMarket",Order=4)] public Brush HighColor { get; set; }
        [Browsable(false)] public string HighColorSerializable { get { return Serialize.BrushToString(HighColor); } set { HighColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Low Color",GroupName="NY PreMarket",Order=5)] public Brush LowColor { get; set; }
        [Browsable(false)] public string LowColorSerializable { get { return Serialize.BrushToString(LowColor); } set { LowColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="PM VWAP Color",GroupName="NY PreMarket",Order=6)] public Brush VwapColor { get; set; }
        [Browsable(false)] public string VwapColorSerializable { get { return Serialize.BrushToString(VwapColor); } set { VwapColor = Serialize.StringToBrush(value); } }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.
namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private WyckoffZen.NYPreMarketLevels[] cacheNYPreMarketLevels;
        public WyckoffZen.NYPreMarketLevels NYPreMarketLevels(int preMarketStart, int preMarketEnd, bool showHighLow, bool showPMVwap)
        { return NYPreMarketLevels(Input, preMarketStart, preMarketEnd, showHighLow, showPMVwap); }
        public WyckoffZen.NYPreMarketLevels NYPreMarketLevels(ISeries<double> input, int preMarketStart, int preMarketEnd, bool showHighLow, bool showPMVwap)
        {
            if (cacheNYPreMarketLevels != null)
                for (int idx = 0; idx < cacheNYPreMarketLevels.Length; idx++)
                    if (cacheNYPreMarketLevels[idx] != null && cacheNYPreMarketLevels[idx].PreMarketStart == preMarketStart && cacheNYPreMarketLevels[idx].PreMarketEnd == preMarketEnd && cacheNYPreMarketLevels[idx].ShowHighLow == showHighLow && cacheNYPreMarketLevels[idx].ShowPMVwap == showPMVwap && cacheNYPreMarketLevels[idx].EqualsInput(input))
                        return cacheNYPreMarketLevels[idx];
            return CacheIndicator<WyckoffZen.NYPreMarketLevels>(new WyckoffZen.NYPreMarketLevels(){ PreMarketStart = preMarketStart, PreMarketEnd = preMarketEnd, ShowHighLow = showHighLow, ShowPMVwap = showPMVwap }, input, ref cacheNYPreMarketLevels);
        }
    }
}
namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.WyckoffZen.NYPreMarketLevels NYPreMarketLevels(int preMarketStart, int preMarketEnd, bool showHighLow, bool showPMVwap)
        { return indicator.NYPreMarketLevels(Input, preMarketStart, preMarketEnd, showHighLow, showPMVwap); }
    }
}
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.WyckoffZen.NYPreMarketLevels NYPreMarketLevels(int preMarketStart, int preMarketEnd, bool showHighLow, bool showPMVwap)
        { return indicator.NYPreMarketLevels(Input, preMarketStart, preMarketEnd, showHighLow, showPMVwap); }
    }
}
#endregion
