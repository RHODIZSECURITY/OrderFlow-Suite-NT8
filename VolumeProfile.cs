#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.BarsTypes;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum ProfileMode { Session, Running }

    [Gui.CategoryOrder("Order Flow", 1)]
    [Gui.CategoryOrder("Market Volume", 2)]
    [Gui.CategoryOrder("Volume Analysis Profile", 3)]
    [Gui.CategoryOrder("Volume Filter", 4)]
    public class VolumeProfile : Indicator
    {
        private double _cumDelta;

        // POC / VAH / VAL
        private readonly SortedList<long, double> _sortedLevels = new SortedList<long, double>();
        private double _totalSessionVol;
        private double _poc = double.NaN;
        private double _vah = double.NaN;
        private double _val = double.NaN;

        // OrderFlow+ volumetric bars (real bid/ask volume per price)
        private VolumetricBarsType _volumetricBars;
        private bool _volumetricChecked;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name        = "VolumeProfile";
                Description = "Order Flow analysis + Market Volume + Volume Profile (POC/VAH/VAL) with advanced filtering.";
                Calculate   = Calculate.OnBarClose;
                IsOverlay   = false;
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;
                DrawOnPricePanel = true;

                ShowOrderFlow      = true;
                ShowMarketVolume   = true;
                ShowProfile        = true;
                ShowVolumeFilter   = true;

                DeltaLength        = 20;
                MinVolumeFactor    = 1.5;
                UseCumulativeDelta = true;
                VolumeZScoreLen    = 50;
                ZScoreThreshold    = 1.0;

                VpProfileMode      = ProfileMode.Session;
                ValueAreaPct       = 70;
                ShowVpLabels       = true;

                AddPlot(new Stroke(Brushes.DodgerBlue,  2), PlotStyle.Bar,  "Delta");
                AddPlot(new Stroke(Brushes.MediumPurple, 2), PlotStyle.Line, "RelativeVolume");
                AddPlot(new Stroke(Brushes.DarkOrange,  2), PlotStyle.Line, "FilteredVolume");
                AddPlot(new Stroke(Brushes.Gold,        2), PlotStyle.Line, "CumulativeDelta");
            }
            else if (State == State.DataLoaded)
            {
                _cumDelta        = 0;
                _totalSessionVol = 0;
                _sortedLevels.Clear();
                _volumetricBars   = null;
                _volumetricChecked = false;
            }
        }

        // OrderFlow+ detection — real bid/ask volume per price when user has
        // volumetric bars on chart. Graceful fallback to Close-vs-Open estimate.
        private void EnsureVolumetricProbe()
        {
            if (_volumetricChecked) return;
            _volumetricChecked = true;
            try
            {
                if (BarsArray != null && BarsArray.Length > 0 && BarsArray[0] != null)
                    _volumetricBars = BarsArray[0].BarsType as VolumetricBarsType;
            }
            catch { _volumetricBars = null; }
        }

        // Returns (askVol - bidVol) from volumetric bars when available,
        // otherwise (upVol - dnVol) using Close vs Open approximation.
        private double GetBarDelta(out double upVol, out double dnVol)
        {
            EnsureVolumetricProbe();
            if (_volumetricBars != null && _volumetricBars.Volumes != null && CurrentBar < _volumetricBars.Volumes.Length)
            {
                try
                {
                    var vol = _volumetricBars.Volumes[CurrentBar];
                    if (vol != null)
                    {
                        upVol = vol.BuyVolume;
                        dnVol = vol.SellVolume;
                        return upVol - dnVol;
                    }
                }
                catch { /* fall through to estimate */ }
            }
            upVol = Close[0] >= Open[0] ? Volume[0] : 0;
            dnVol = Close[0] <  Open[0] ? Volume[0] : 0;
            return upVol - dnVol;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Math.Max(DeltaLength, VolumeZScoreLen))
            {
                Values[0][0] = Values[1][0] = Values[2][0] = Values[3][0] = double.NaN;
                return;
            }

            if (Bars.IsFirstBarOfSession)
            {
                _cumDelta = 0;
                if (VpProfileMode == ProfileMode.Session)
                {
                    _sortedLevels.Clear();
                    _totalSessionVol = 0;
                    _poc = _vah = _val = double.NaN;
                }
            }

            double upVol, dnVol;
            double delta = GetBarDelta(out upVol, out dnVol);
            _cumDelta += delta;

            double avgVol = SMA(Volume, DeltaLength)[0];
            double rv     = avgVol > 0 ? Volume[0] / avgVol : 0;

            double sigma = StdDev(Volume, VolumeZScoreLen)[0];
            double mean  = SMA(Volume, VolumeZScoreLen)[0];
            double z     = sigma > 0 ? (Volume[0] - mean) / sigma : 0;
            double fVol  = (rv >= MinVolumeFactor && z >= ZScoreThreshold) ? Volume[0] : 0;

            Values[0][0] = ShowOrderFlow      ? delta   : double.NaN;
            Values[1][0] = ShowMarketVolume || ShowProfile ? rv : double.NaN;
            Values[2][0] = ShowVolumeFilter   ? fVol    : double.NaN;
            Values[3][0] = (ShowOrderFlow && UseCumulativeDelta) ? _cumDelta : double.NaN;

            if (ShowProfile)
            {
                long key = (long)Math.Round(Close[0] / TickSize);
                if (_sortedLevels.ContainsKey(key))
                    _sortedLevels[key] += Volume[0];
                else
                    _sortedLevels[key] = Volume[0];
                _totalSessionVol += Volume[0];

                // Cap Running mode to prevent unbounded memory growth
                if (VpProfileMode == ProfileMode.Running && _sortedLevels.Count > 5000)
                {
                    int minIdx = 0;
                    for (int i = 1; i < _sortedLevels.Count; i++)
                        if (_sortedLevels.Values[i] < _sortedLevels.Values[minIdx]) minIdx = i;
                    _totalSessionVol -= _sortedLevels.Values[minIdx];  // keep denominator consistent
                    _sortedLevels.RemoveAt(minIdx);
                }

                CalculatePocValueArea();
            }
        }

        private void CalculatePocValueArea()
        {
            if (_sortedLevels.Count == 0 || _totalSessionVol <= 0) return;

            // POC = price with highest volume
            long pocKey = 0;
            double maxVol = double.MinValue;
            for (int i = 0; i < _sortedLevels.Count; i++)
            {
                if (_sortedLevels.Values[i] > maxVol)
                {
                    maxVol = _sortedLevels.Values[i];
                    pocKey = _sortedLevels.Keys[i];
                }
            }
            _poc = pocKey * TickSize;

            // Value area: expand from POC until ValueAreaPct% of volume is captured
            double target      = _totalSessionVol * ValueAreaPct / 100.0;
            double accumulated = maxVol;
            int pocIdx = _sortedLevels.IndexOfKey(pocKey);
            int loIdx  = pocIdx;
            int hiIdx  = pocIdx;

            while (accumulated < target)
            {
                bool canUp   = hiIdx < _sortedLevels.Count - 1;
                bool canDown = loIdx > 0;
                if (!canUp && !canDown) break;

                double nextHiVol = canUp   ? _sortedLevels.Values[hiIdx + 1] : 0;
                double nextLoVol = canDown ? _sortedLevels.Values[loIdx - 1] : 0;

                if (canUp && (!canDown || nextHiVol >= nextLoVol))
                    accumulated += _sortedLevels.Values[++hiIdx];
                else
                    accumulated += _sortedLevels.Values[--loIdx];
            }

            _vah = _sortedLevels.Keys[hiIdx] * TickSize;
            _val = _sortedLevels.Keys[loIdx]  * TickSize;

            // Draw on price panel (DrawOnPricePanel = true in SetDefaults)
            Draw.HorizontalLine(this, "VP_POC", _poc, PocColor,    DashStyleHelper.Solid, 2);
            Draw.HorizontalLine(this, "VP_VAH", _vah, VahValColor, DashStyleHelper.Dash,  1);
            Draw.HorizontalLine(this, "VP_VAL", _val, VahValColor, DashStyleHelper.Dash,  1);

            if (ShowVpLabels)
            {
                Draw.Text(this, "VP_POC_LBL", $"POC {_poc:F2}", 0, _poc, PocColor);
                Draw.Text(this, "VP_VAH_LBL", $"VAH {_vah:F2}", 0, _vah, VahValColor);
                Draw.Text(this, "VP_VAL_LBL", $"VAL {_val:F2}", 0, _val, VahValColor);
            }
        }

        #region Properties

        [NinjaScriptProperty, Display(Name = "Show Order Flow",    GroupName = "Order Flow",            Order = 1)] public bool ShowOrderFlow    { get; set; }
        [NinjaScriptProperty, Display(Name = "Show Market Volume", GroupName = "Market Volume",          Order = 1)] public bool ShowMarketVolume { get; set; }
        [NinjaScriptProperty, Display(Name = "Show Profile",       GroupName = "Volume Analysis Profile", Order = 1)] public bool ShowProfile   { get; set; }
        [NinjaScriptProperty, Display(Name = "Show Volume Filter", GroupName = "Volume Filter",          Order = 1)] public bool ShowVolumeFilter { get; set; }

        [NinjaScriptProperty, Range(1, 500), Display(Name = "Delta Length",       GroupName = "Order Flow",    Order = 2)] public int    DeltaLength        { get; set; }
        [NinjaScriptProperty,               Display(Name = "Use Cumulative Delta", GroupName = "Order Flow",    Order = 3)] public bool   UseCumulativeDelta { get; set; }

        [NinjaScriptProperty, Range(0.1, 20.0), Display(Name = "Min Volume Factor",   GroupName = "Volume Filter", Order = 2)] public double MinVolumeFactor  { get; set; }
        [NinjaScriptProperty, Range(5, 500),    Display(Name = "Volume ZScore Length", GroupName = "Volume Filter", Order = 3)] public int    VolumeZScoreLen { get; set; }
        [NinjaScriptProperty, Range(-5.0, 10.0),Display(Name = "ZScore Threshold",    GroupName = "Volume Filter", Order = 4)] public double ZScoreThreshold { get; set; }

        [NinjaScriptProperty, Display(Name = "Profile Mode",    GroupName = "Volume Analysis Profile", Order = 2)] public ProfileMode VpProfileMode { get; set; }
        [NinjaScriptProperty, Range(50, 100), Display(Name = "Value Area %", GroupName = "Volume Analysis Profile", Order = 3)] public int ValueAreaPct { get; set; }
        [NinjaScriptProperty, Display(Name = "Show VP Labels",  GroupName = "Volume Analysis Profile", Order = 4)] public bool ShowVpLabels { get; set; }

        [XmlIgnore, Display(Name = "POC Color",    GroupName = "Volume Analysis Profile", Order = 5)]
        public Brush PocColor { get; set; } = Brushes.Red;
        [Browsable(false)]
        public string PocColorSerializable { get => Serialize.BrushToString(PocColor); set => PocColor = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "VAH/VAL Color", GroupName = "Volume Analysis Profile", Order = 6)]
        public Brush VahValColor { get; set; } = Brushes.DodgerBlue;
        [Browsable(false)]
        public string VahValColorSerializable { get => Serialize.BrushToString(VahValColor); set => VahValColor = Serialize.StringToBrush(value); }

        [Browsable(false)] public Series<double> Delta          => Values[0];
        [Browsable(false)] public Series<double> RelativeVolume => Values[1];
        [Browsable(false)] public Series<double> FilteredVolume => Values[2];
        [Browsable(false)] public Series<double> CumulativeDelta => Values[3];
        [Browsable(false)] public double Poc => _poc;
        [Browsable(false)] public double Vah => _vah;
        [Browsable(false)] public double Val => _val;

        #endregion
    }
}
