#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

// Converted from OrderFlow Scalper Pro Pine Script
// Section: [PM4] SESSION / OVERNIGHT GAP CONTEXT (lines 366-428)
// Logic: detects overnight gap (equities: NY open vs prev RTH close,
//        futures: time-gap between bars > 10x normal spacing)

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class SessionGap : Indicator
    {
        // ── Gap state ─────────────────────────────────────────────────────────
        private double _prevRthClose = double.NaN;
        private double _gapTarget    = double.NaN;
        private bool   _gapUp, _gapDown, _gapFilled;
        private double _gapSize;

        // ── Session tracking ──────────────────────────────────────────────────
        private bool     _wasInNY;
        private DateTime _currentDate;

        // ── Settings ──────────────────────────────────────────────────────────
        private double _gapMinAtr;
        private bool   _showGapLine, _showGapLabel, _showFillSignal;
        private Brush  _gapUpColor, _gapDownColor, _gapFilledColor;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "SessionGap";
                Description        = "Overnight/session gap detection with fill tracking. Supports equities (NY open) and futures (time-gap method). Ported from OrderFlow Scalper Pro.";
                Calculate          = Calculate.OnEachTick;
                IsOverlay          = true;
                DisplayInDataBox   = false;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                _gapMinAtr     = 0.5;   // Pine: gapMinATR = 0.5 ATR units
                _showGapLine   = true;
                _showGapLabel  = true;
                _showFillSignal = true;

                _gapUpColor     = new SolidColorBrush(Color.FromArgb(200, 255, 23, 68));   // Red  — gap up (price above ref)
                _gapDownColor   = new SolidColorBrush(Color.FromArgb(200, 33, 150, 243));  // Blue — gap down
                _gapFilledColor = new SolidColorBrush(Color.FromArgb(120, 120, 120, 120)); // Gray — filled
            }
            else if (State == State.DataLoaded)
            {
                _prevRthClose = double.NaN;
                _gapTarget    = double.NaN;
                _gapUp        = false;
                _gapDown      = false;
                _gapFilled    = false;
                _gapSize      = 0;
                _wasInNY      = false;
                _currentDate  = DateTime.MinValue;
            }
        }

        private bool IsNYCashSession(DateTime t)
        {
            int hhmm = t.Hour * 100 + t.Minute;
            return hhmm >= 930 && hhmm < 1600;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 2) return;

            DateTime barTime = Time[0].ToLocalTime();
            bool inNY  = IsNYCashSession(barTime);
            bool nyOpen  = inNY && !_wasInNY;
            bool nyClose = !inNY && _wasInNY;

            // Capture last RTH close on session transition
            if (nyClose)
                _prevRthClose = Close[1];

            // ── Gap detection ─────────────────────────────────────────────────
            double atr14 = ATR(14)[0];
            double atrSafe = Math.Max(atr14, TickSize);
            bool gapEvent = false;
            double gapReference = double.NaN;

            // Futures: detect via time gap between bars (>10x normal bar duration)
            double normalMs = Bars.BarsPeriod.Value * 60000.0;
            double barGapMs = (Time[0] - Time[1]).TotalMilliseconds;
            bool futuresGap = Instrument.MasterInstrument.InstrumentType == NinjaTrader.Cbi.InstrumentType.Future
                              && barGapMs > normalMs * 10
                              && !double.IsNaN(Close[1]);

            if (futuresGap)
            {
                gapEvent     = true;
                gapReference = Close[1];
            }
            else if (nyOpen)
            {
                if (!double.IsNaN(_prevRthClose))
                {
                    gapEvent     = true;
                    gapReference = _prevRthClose;
                }
                else if (CurrentBar >= 2)
                {
                    gapEvent     = true;
                    gapReference = Close[1];
                }
            }

            if (gapEvent && !double.IsNaN(gapReference))
            {
                _gapSize   = Open[0] - gapReference;
                _gapUp     = _gapSize >  atrSafe * _gapMinAtr;
                _gapDown   = _gapSize < -atrSafe * _gapMinAtr;
                _gapTarget = gapReference;
                _gapFilled = false;

                if ((_gapUp || _gapDown) && _showGapLine)
                {
                    string tag = "GapTarget_" + CurrentBar;
                    Brush lineColor = _gapUp ? _gapUpColor : _gapDownColor;
                    Draw.HorizontalLine(this, tag, _gapTarget, lineColor, DashStyleHelper.Dash, 1);

                    if (_showGapLabel)
                    {
                        string dir    = _gapUp ? "Gap Up" : "Gap Down";
                        string sizeTxt = (_gapSize / TickSize).ToString("F0") + " ticks";
                        Draw.Text(this, "GapLabel_" + CurrentBar, true, dir + " (" + sizeTxt + ")",
                            0, _gapTarget + (_gapUp ? -TickSize * 3 : TickSize * 3),
                            0, lineColor, new SimpleFont("Arial", 9), TextAlignment.Right,
                            Brushes.Transparent, Brushes.Transparent, 0);
                    }
                }
            }
            else if (nyOpen && !gapEvent)
            {
                // Reset stale gap at next NY open with no new gap
                _gapUp = _gapDown = _gapFilled = false;
                _gapTarget = double.NaN;
                _gapSize   = 0;
            }

            // ── Gap fill tracking ─────────────────────────────────────────────
            if (_gapUp && !_gapFilled && !double.IsNaN(_gapTarget) && Close[0] <= _gapTarget)
            {
                _gapFilled = true;
                if (_showGapLabel)
                    Draw.Text(this, "GapFilled_" + CurrentBar, true, "Gap Filled",
                        0, Low[0] - TickSize * 4, 0, _gapFilledColor,
                        new SimpleFont("Arial", 9), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
            }
            if (_gapDown && !_gapFilled && !double.IsNaN(_gapTarget) && Close[0] >= _gapTarget)
            {
                _gapFilled = true;
                if (_showGapLabel)
                    Draw.Text(this, "GapFilled_" + CurrentBar, true, "Gap Filled",
                        0, High[0] + TickSize * 4, 0, _gapFilledColor,
                        new SimpleFont("Arial", 9), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
            }

            _wasInNY = inNY;
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Min Gap Size (ATR mult)", Order = 1, GroupName = "Session Gap")]
        public double GapMinAtr { get => _gapMinAtr; set => _gapMinAtr = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Gap Line", Order = 2, GroupName = "Session Gap")]
        public bool ShowGapLine { get => _showGapLine; set => _showGapLine = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Gap Label", Order = 3, GroupName = "Session Gap")]
        public bool ShowGapLabel { get => _showGapLabel; set => _showGapLabel = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Fill Signal", Order = 4, GroupName = "Session Gap")]
        public bool ShowFillSignal { get => _showFillSignal; set => _showFillSignal = value; }

        [XmlIgnore]
        [Display(Name = "Gap Up Color", Order = 5, GroupName = "Session Gap")]
        public Brush GapUpColor { get => _gapUpColor; set => _gapUpColor = value; }

        [Browsable(false)]
        public string GapUpColorSerializable
        {
            get { return Serialize.BrushToString(_gapUpColor); }
            set { _gapUpColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Gap Down Color", Order = 6, GroupName = "Session Gap")]
        public Brush GapDownColor { get => _gapDownColor; set => _gapDownColor = value; }

        [Browsable(false)]
        public string GapDownColorSerializable
        {
            get { return Serialize.BrushToString(_gapDownColor); }
            set { _gapDownColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Gap Filled Color", Order = 7, GroupName = "Session Gap")]
        public Brush GapFilledColor { get => _gapFilledColor; set => _gapFilledColor = value; }

        [Browsable(false)]
        public string GapFilledColorSerializable
        {
            get { return Serialize.BrushToString(_gapFilledColor); }
            set { _gapFilledColor = Serialize.StringToBrush(value); }
        }

        // Expose gap state for other indicators/strategies
        [Browsable(false)] public bool   GapUp     => _gapUp;
        [Browsable(false)] public bool   GapDown   => _gapDown;
        [Browsable(false)] public bool   GapFilled => _gapFilled;
        [Browsable(false)] public double GapTarget => _gapTarget;
        [Browsable(false)] public double GapSize   => _gapSize;

        #endregion
    }
}
