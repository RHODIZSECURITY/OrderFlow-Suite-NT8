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
// Section: MARKET STRUCTURE (lines 4366-4451)
// Logic: pivot high/low based BOS (Break of Structure) and CHoCH (Change of Character)
// BOS  = break in trend direction  (continuation)
// CHoCH = break against trend      (reversal signal)
// MSS  = Market Structure Shift    (first break, trend uninitialized)

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class MarketStructure : Indicator
    {
        // ── Pivot state ───────────────────────────────────────────────────────
        private double _lastHigh = double.NaN, _lastLow = double.NaN;
        private int    _lastHighBar = -1, _lastLowBar = -1;
        private bool   _lastHighBroken, _lastLowBroken;
        private bool   _trendBull, _trendInit;
        private int    _lastBreakBar = -1, _lastBreakDir;
        private double _lastBreakExtreme = double.NaN;

        // For draw tag cleanup (keep last N)
        private readonly Queue<string> _lineTagsH = new Queue<string>();
        private readonly Queue<string> _lineTagsL = new Queue<string>();
        private const int MaxLines = 60;

        // ── Settings ──────────────────────────────────────────────────────────
        private int  _pivotLen;
        private bool _showBOS, _showCHoCH, _showMSS, _showLabels;
        private Brush _bullColor, _bearColor;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "MarketStructure";
                Description        = "SMC Market Structure: BOS (Break of Structure), CHoCH (Change of Character), MSS (Market Structure Shift). Ported from SMC/ICT Suite Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                DisplayInDataBox   = false;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                _pivotLen  = 5;
                _showBOS   = true;
                _showCHoCH = true;
                _showMSS   = true;
                _showLabels = true;

                _bullColor = new SolidColorBrush(Color.FromArgb(255,  76, 175,  79)); // Green
                _bearColor = new SolidColorBrush(Color.FromArgb(255, 244,  67,  54)); // Red
            }
            else if (State == State.DataLoaded)
            {
                _lastHigh = double.NaN; _lastLow = double.NaN;
                _lastHighBar = -1; _lastLowBar = -1;
                _trendBull = false; _trendInit = false;
                _lastBreakBar = -1; _lastBreakDir = 0;
                _lineTagsH.Clear(); _lineTagsL.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < _pivotLen * 2 + 1) return;

            // ── Detect pivot high (confirmed _pivotLen bars ago) ───────────────
            double ph = double.NaN;
            bool isPH = true;
            double pivotPrice = High[_pivotLen];
            for (int i = 0; i < _pivotLen * 2 + 1; i++)
            {
                if (i == _pivotLen) continue;
                if (High[i] >= pivotPrice) { isPH = false; break; }
            }
            if (isPH) ph = pivotPrice;

            double pl = double.NaN;
            bool isPL = true;
            double pivotPriceL = Low[_pivotLen];
            for (int i = 0; i < _pivotLen * 2 + 1; i++)
            {
                if (i == _pivotLen) continue;
                if (Low[i] <= pivotPriceL) { isPL = false; break; }
            }
            if (isPL) pl = pivotPriceL;

            // Update last pivot references
            if (!double.IsNaN(ph))
            {
                _lastHigh       = ph;
                _lastHighBar    = CurrentBar - _pivotLen;
                _lastHighBroken = false;
            }
            if (!double.IsNaN(pl))
            {
                _lastLow        = pl;
                _lastLowBar     = CurrentBar - _pivotLen;
                _lastLowBroken  = false;
            }

            // ── Structure break detection ─────────────────────────────────────
            bool crossHigh = !double.IsNaN(_lastHigh) && Close[0] > _lastHigh && !_lastHighBroken;
            bool crossLow  = !double.IsNaN(_lastLow)  && Close[0] < _lastLow  && !_lastLowBroken;

            if (crossHigh && (_lastBreakBar < 0 || CurrentBar > _lastBreakBar))
            {
                bool isBOS   = _trendInit && _trendBull;
                bool isCHoCH = _trendInit && !_trendBull;
                bool isMSS   = !_trendInit;

                string label = isCHoCH ? "CHoCH" : isMSS ? "MSS" : "BOS";
                bool allowed = isCHoCH ? _showCHoCH : isMSS ? _showMSS : _showBOS;

                if (allowed)
                {
                    string tag = "MSH_" + CurrentBar;
                    Draw.Line(this, tag, false, CurrentBar - _lastHighBar, _lastHigh, 0, _lastHigh, _bullColor, DashStyleHelper.Solid, 1);
                    _lineTagsH.Enqueue(tag);
                    if (_lineTagsH.Count > MaxLines)
                        RemoveDrawObject(_lineTagsH.Dequeue());

                    if (_showLabels)
                    {
                        string tagLbl = "MSHLbl_" + CurrentBar;
                        int midOffset = (CurrentBar - _lastHighBar) / 2;
                        Draw.Text(this, tagLbl, true, label, -midOffset, _lastHigh + TickSize * 2, 0,
                            _bullColor, new SimpleFont("Arial", 8), TextAlignment.Center,
                            Brushes.Transparent, Brushes.Transparent, 0);
                    }
                }

                _lastBreakBar     = CurrentBar;
                _lastBreakDir     = 1;
                _lastBreakExtreme = High[0];
                _trendBull        = true;
                _trendInit        = true;
                _lastHighBroken   = true;
            }

            if (crossLow && (_lastBreakBar < 0 || CurrentBar > _lastBreakBar))
            {
                bool isBOS   = _trendInit && !_trendBull;
                bool isCHoCH = _trendInit && _trendBull;
                bool isMSS   = !_trendInit;

                string label = isCHoCH ? "CHoCH" : isMSS ? "MSS" : "BOS";
                bool allowed = isCHoCH ? _showCHoCH : isMSS ? _showMSS : _showBOS;

                if (allowed)
                {
                    string tag = "MSL_" + CurrentBar;
                    Draw.Line(this, tag, false, CurrentBar - _lastLowBar, _lastLow, 0, _lastLow, _bearColor, DashStyleHelper.Solid, 1);
                    _lineTagsL.Enqueue(tag);
                    if (_lineTagsL.Count > MaxLines)
                        RemoveDrawObject(_lineTagsL.Dequeue());

                    if (_showLabels)
                    {
                        string tagLbl = "MSLLbl_" + CurrentBar;
                        int midOffset = (CurrentBar - _lastLowBar) / 2;
                        Draw.Text(this, tagLbl, true, label, -midOffset, _lastLow - TickSize * 4, 0,
                            _bearColor, new SimpleFont("Arial", 8), TextAlignment.Center,
                            Brushes.Transparent, Brushes.Transparent, 0);
                    }
                }

                _lastBreakBar    = CurrentBar;
                _lastBreakDir    = -1;
                _lastBreakExtreme = Low[0];
                _trendBull       = false;
                _trendInit       = true;
                _lastLowBroken   = true;
            }

            // Track trailing extreme for last break direction
            if (_lastBreakDir == 1 && High[0] > _lastBreakExtreme)
            { _lastBreakExtreme = High[0]; _lastBreakBar = CurrentBar; }
            if (_lastBreakDir == -1 && Low[0] < _lastBreakExtreme)
            { _lastBreakExtreme = Low[0]; _lastBreakBar = CurrentBar; }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Pivot Length", Order = 1, GroupName = "Market Structure")]
        public int PivotLen { get => _pivotLen; set => _pivotLen = Math.Max(2, value); }

        [NinjaScriptProperty]
        [Display(Name = "Show BOS", Order = 2, GroupName = "Market Structure")]
        public bool ShowBOS { get => _showBOS; set => _showBOS = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show CHoCH", Order = 3, GroupName = "Market Structure")]
        public bool ShowCHoCH { get => _showCHoCH; set => _showCHoCH = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show MSS", Order = 4, GroupName = "Market Structure")]
        public bool ShowMSS { get => _showMSS; set => _showMSS = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 5, GroupName = "Market Structure")]
        public bool ShowLabels { get => _showLabels; set => _showLabels = value; }

        [XmlIgnore]
        [Display(Name = "Bullish Color", Order = 6, GroupName = "Market Structure")]
        public Brush BullColor { get => _bullColor; set => _bullColor = value; }

        [Browsable(false)]
        public string BullColorSerializable
        {
            get { return Serialize.BrushToString(_bullColor); }
            set { _bullColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Bearish Color", Order = 7, GroupName = "Market Structure")]
        public Brush BearColor { get => _bearColor; set => _bearColor = value; }

        [Browsable(false)]
        public string BearColorSerializable
        {
            get { return Serialize.BrushToString(_bearColor); }
            set { _bearColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)] public bool TrendBull => _trendBull;
        [Browsable(false)] public bool TrendInit => _trendInit;

        #endregion
    }
}
