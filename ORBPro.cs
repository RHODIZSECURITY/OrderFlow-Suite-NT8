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

// Converted from SMC / ICT Suite Pro Pine Script
// Section: ORB — OPENING RANGE BREAKOUT (lines 1310-1610)
// Supports: Break Entry, Trap Entry, Reversal Entry
// Opening range duration configurable (default 5 min from NY open 09:30 ET)

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class ORBPro : Indicator
    {
        // ── ORB state ─────────────────────────────────────────────────────────
        private double   _orbHigh = double.NaN, _orbLow = double.NaN;
        private bool     _orbReady;
        private DateTime _orbRangeEnd;
        private DateTime _currentDate;
        private bool     _breakLongFired, _breakShortFired;
        private bool     _trapLongFired,  _trapShortFired;

        // ── Settings ──────────────────────────────────────────────────────────
        private int  _rangeDuration;   // minutes from NY open
        private int  _signalCutoffHour;
        private bool _enableBreakout, _enableTrap, _enableReversal;
        private bool _showRangeFill,  _showPriceLabels;
        private Brush _highColor, _lowColor, _fillColor;
        private Brush _breakLongColor, _breakShortColor;
        private Brush _trapLongColor, _trapShortColor;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "ORBPro";
                Description        = "Opening Range Breakout with Break, Trap and Reversal entries. Ported from SMC/ICT Suite Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                DisplayInDataBox   = false;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                _rangeDuration    = 5;    // Pine default: 5 min
                _signalCutoffHour = 11;   // Stop signals after 11:00 ET
                _enableBreakout   = true;
                _enableTrap       = true;
                _enableReversal   = true;
                _showRangeFill    = true;
                _showPriceLabels  = false;

                _highColor        = new SolidColorBrush(Color.FromArgb(255,  0, 255, 157)); // #00ff9d
                _lowColor         = new SolidColorBrush(Color.FromArgb(255, 255,   0, 110)); // #ff006e
                _fillColor        = new SolidColorBrush(Color.FromArgb(30,  33, 150, 243)); // light blue
                _breakLongColor   = new SolidColorBrush(Color.FromArgb(220,  76, 175,  79)); // green
                _breakShortColor  = new SolidColorBrush(Color.FromArgb(220, 244,  67,  54)); // red
                _trapLongColor    = new SolidColorBrush(Color.FromArgb(220, 255, 193,   7)); // amber
                _trapShortColor   = new SolidColorBrush(Color.FromArgb(220, 255, 193,   7));
            }
            else if (State == State.DataLoaded)
            {
                _orbHigh = double.NaN; _orbLow = double.NaN;
                _orbReady = false;
                _currentDate = DateTime.MinValue;
                _breakLongFired = _breakShortFired = false;
                _trapLongFired  = _trapShortFired  = false;
            }
        }

        private bool IsNYCashSession(DateTime t)
        {
            int hhmm = t.Hour * 100 + t.Minute;
            return hhmm >= 930 && hhmm < 1600;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;

            DateTime barTime = Time[0].ToLocalTime();

            // ── Day reset ─────────────────────────────────────────────────────
            if (barTime.Date != _currentDate)
            {
                _currentDate = barTime.Date;
                _orbHigh = double.NaN; _orbLow = double.NaN;
                _orbReady = false;

                // ORB range end = NY open + rangeDuration minutes
                _orbRangeEnd = new DateTime(barTime.Year, barTime.Month, barTime.Day, 9, 30, 0)
                               .AddMinutes(_rangeDuration);

                _breakLongFired = _breakShortFired = false;
                _trapLongFired  = _trapShortFired  = false;
            }

            bool inNY    = IsNYCashSession(barTime);
            bool inRange = barTime >= new DateTime(barTime.Year, barTime.Month, barTime.Day, 9, 30, 0)
                        && barTime < _orbRangeEnd;

            // ── Capture opening range ─────────────────────────────────────────
            if (inRange && inNY)
            {
                _orbHigh = double.IsNaN(_orbHigh) ? High[0] : Math.Max(_orbHigh, High[0]);
                _orbLow  = double.IsNaN(_orbLow)  ? Low[0]  : Math.Min(_orbLow,  Low[0]);
            }

            // ── Mark range as ready once we pass the range window ─────────────
            if (!_orbReady && barTime >= _orbRangeEnd && !double.IsNaN(_orbHigh))
            {
                _orbReady = true;

                // Draw ORB range box and lines
                if (_showRangeFill)
                    Draw.Rectangle(this, "ORB_Box_" + _currentDate.Ticks, false,
                        0, _orbHigh, -(int)((_rangeDuration * 60) / Bars.BarsPeriod.Value + 1), _orbLow,
                        _fillColor, _fillColor, 0);

                Draw.HorizontalLine(this, "ORB_High_" + _currentDate.Ticks, _orbHigh, _highColor, DashStyleHelper.Solid, 2);
                Draw.HorizontalLine(this, "ORB_Low_"  + _currentDate.Ticks, _orbLow,  _lowColor,  DashStyleHelper.Solid, 2);

                if (_showPriceLabels)
                {
                    Draw.Text(this, "ORB_HL_" + _currentDate.Ticks, true,
                        "ORB H: " + _orbHigh.ToString("F2"), 0, _orbHigh + TickSize * 3, 0,
                        _highColor, new SimpleFont("Arial", 8), TextAlignment.Left,
                        Brushes.Transparent, Brushes.Transparent, 0);
                    Draw.Text(this, "ORB_LL_" + _currentDate.Ticks, true,
                        "ORB L: " + _orbLow.ToString("F2"), 0, _orbLow - TickSize * 3, 0,
                        _lowColor, new SimpleFont("Arial", 8), TextAlignment.Left,
                        Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            if (!_orbReady || !inNY) return;

            // Past signal cutoff hour?
            if (barTime.Hour >= _signalCutoffHour) return;

            // ── Entry signals ─────────────────────────────────────────────────

            // ① Break Entry: close breaks through ORB level
            if (_enableBreakout)
            {
                if (!_breakLongFired && Close[0] > _orbHigh && Close[1] <= _orbHigh)
                {
                    _breakLongFired = true;
                    Draw.ArrowUp(this, "BrkL_" + CurrentBar, false, 0,
                        Low[0] - TickSize * 3, _breakLongColor);
                    Draw.Text(this, "BrkLTxt_" + CurrentBar, true, "① Break",
                        0, Low[0] - TickSize * 7, 0, _breakLongColor,
                        new SimpleFont("Arial", 8), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
                }
                if (!_breakShortFired && Close[0] < _orbLow && Close[1] >= _orbLow)
                {
                    _breakShortFired = true;
                    Draw.ArrowDown(this, "BrkS_" + CurrentBar, false, 0,
                        High[0] + TickSize * 3, _breakShortColor);
                    Draw.Text(this, "BrkSTxt_" + CurrentBar, true, "① Break",
                        0, High[0] + TickSize * 7, 0, _breakShortColor,
                        new SimpleFont("Arial", 8), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // ② Trap Entry: price breaks ORB then reverses back inside
            if (_enableTrap)
            {
                bool brokeAbove = High[1] > _orbHigh && Close[1] > _orbHigh;
                bool brokeBelow = Low[1]  < _orbLow  && Close[1] < _orbLow;

                // Trap Short: broke above but closes back inside
                if (!_trapShortFired && brokeAbove && Close[0] < _orbHigh)
                {
                    _trapShortFired = true;
                    Draw.ArrowDown(this, "TrpS_" + CurrentBar, false, 0,
                        High[0] + TickSize * 3, _trapShortColor);
                    Draw.Text(this, "TrpSTxt_" + CurrentBar, true, "② Trap",
                        0, High[0] + TickSize * 7, 0, _trapShortColor,
                        new SimpleFont("Arial", 8), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
                }

                // Trap Long: broke below but closes back inside
                if (!_trapLongFired && brokeBelow && Close[0] > _orbLow)
                {
                    _trapLongFired = true;
                    Draw.ArrowUp(this, "TrpL_" + CurrentBar, false, 0,
                        Low[0] - TickSize * 3, _trapLongColor);
                    Draw.Text(this, "TrpLTxt_" + CurrentBar, true, "② Trap",
                        0, Low[0] - TickSize * 7, 0, _trapLongColor,
                        new SimpleFont("Arial", 8), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // ③ Reversal Entry: failed breakout (break + immediate rejection)
            if (_enableReversal)
            {
                // Failed break up: new high above ORB but closes weak (below midpoint of candle)
                double mid0 = (High[0] + Low[0]) / 2.0;
                bool failedBreakUp = High[0] > _orbHigh && Close[0] < mid0 && Close[0] < _orbHigh;
                bool failedBreakDn = Low[0]  < _orbLow  && Close[0] > mid0 && Close[0] > _orbLow;

                if (failedBreakUp)
                    Draw.Diamond(this, "RevS_" + CurrentBar, false, 0,
                        High[0] + TickSize * 5, _breakShortColor);
                if (failedBreakDn)
                    Draw.Diamond(this, "RevL_" + CurrentBar, false, 0,
                        Low[0] - TickSize * 5, _breakLongColor);
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, 60)]
        [Display(Name = "Opening Range (min)", Order = 1, GroupName = "ORB Pro")]
        public int RangeDuration { get => _rangeDuration; set => _rangeDuration = value; }

        [NinjaScriptProperty]
        [Range(9, 16)]
        [Display(Name = "Signal Cutoff Hour (ET)", Order = 2, GroupName = "ORB Pro")]
        public int SignalCutoffHour { get => _signalCutoffHour; set => _signalCutoffHour = value; }

        [NinjaScriptProperty]
        [Display(Name = "① Break Entry", Order = 3, GroupName = "ORB Pro")]
        public bool EnableBreakout { get => _enableBreakout; set => _enableBreakout = value; }

        [NinjaScriptProperty]
        [Display(Name = "② Trap Entry", Order = 4, GroupName = "ORB Pro")]
        public bool EnableTrap { get => _enableTrap; set => _enableTrap = value; }

        [NinjaScriptProperty]
        [Display(Name = "③ Reversal Entry", Order = 5, GroupName = "ORB Pro")]
        public bool EnableReversal { get => _enableReversal; set => _enableReversal = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Range Fill", Order = 6, GroupName = "ORB Pro")]
        public bool ShowRangeFill { get => _showRangeFill; set => _showRangeFill = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Price Labels", Order = 7, GroupName = "ORB Pro")]
        public bool ShowPriceLabels { get => _showPriceLabels; set => _showPriceLabels = value; }

        [XmlIgnore]
        [Display(Name = "High Line Color", Order = 8, GroupName = "ORB Pro")]
        public Brush HighColor { get => _highColor; set => _highColor = value; }

        [Browsable(false)]
        public string HighColorSerializable
        {
            get { return Serialize.BrushToString(_highColor); }
            set { _highColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Low Line Color", Order = 9, GroupName = "ORB Pro")]
        public Brush LowColor { get => _lowColor; set => _lowColor = value; }

        [Browsable(false)]
        public string LowColorSerializable
        {
            get { return Serialize.BrushToString(_lowColor); }
            set { _lowColor = Serialize.StringToBrush(value); }
        }

        // Expose state
        [Browsable(false)] public bool   OrbReady => _orbReady;
        [Browsable(false)] public double OrbHigh  => _orbHigh;
        [Browsable(false)] public double OrbLow   => _orbLow;

        #endregion
    }
}
