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
// Sections: [PM1] Pre-Market High/Low + [PM2] Overnight VWAP + Session VWAP
// Pine source: OrderFlowScalperPro.pine lines 315-364

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class SessionVWAP : Indicator
    {
        // ── Overnight High/Low tracking ───────────────────────────────────────
        private double _pmHigh, _pmLow;
        private double _pmHighFinal, _pmLowFinal;
        private bool   _pmReady;
        private bool   _inPM;
        private DateTime _currentDate;

        // ── Overnight VWAP (PM2) ──────────────────────────────────────────────
        private double _pmVwapNumer, _pmVwapDenom;
        private double _pmVwapFinal;

        // ── Session VWAP (anchored per session) ───────────────────────────────
        private double _sessVwapNumer, _sessVwapDenom;

        // ── Settings ──────────────────────────────────────────────────────────
        private bool  _showPMLines, _showPMVwap, _showSessionVWAP;
        private int   _pmStartHour, _pmStartMin, _pmEndHour, _pmEndMin;
        private Brush _pmHighColor, _pmLowColor, _pmVwapColor, _sessVwapColor;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "SessionVWAP";
                Description        = "Overnight High/Low, Overnight VWAP and anchored Session VWAP. Ported from OrderFlow Scalper Pro Pine Script.";
                Calculate          = Calculate.OnEachTick;
                IsOverlay          = true;
                DisplayInDataBox   = true;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                // Pre-market session: 04:00 - 09:30 ET (Pine: pmSession = "0400-0930")
                _pmStartHour = 4;  _pmStartMin = 0;
                _pmEndHour   = 9;  _pmEndMin   = 30;

                _showPMLines    = true;
                _showPMVwap     = true;
                _showSessionVWAP = true;

                _pmHighColor   = new SolidColorBrush(Color.FromArgb(200, 255, 152, 0));   // Orange
                _pmLowColor    = new SolidColorBrush(Color.FromArgb(200, 33,  150, 243)); // Blue
                _pmVwapColor   = new SolidColorBrush(Color.FromArgb(180, 156, 39,  176)); // Purple
                _sessVwapColor = new SolidColorBrush(Color.FromArgb(130, 255, 255, 255)); // White

                AddPlot(new Stroke(Brushes.Orange,     2), PlotStyle.HLine, "OvernightHigh");
                AddPlot(new Stroke(Brushes.DodgerBlue, 2), PlotStyle.HLine, "OvernightLow");
                AddPlot(new Stroke(Brushes.MediumPurple, 1), PlotStyle.Line, "OvernightVWAP");
                AddPlot(new Stroke(Brushes.WhiteSmoke,   2), PlotStyle.Line, "SessionVWAPLine");
            }
            else if (State == State.DataLoaded)
            {
                _pmHigh = double.MinValue;
                _pmLow  = double.MaxValue;
                _pmHighFinal = double.NaN;
                _pmLowFinal  = double.NaN;
                _pmReady     = false;
                _pmVwapNumer = 0; _pmVwapDenom = 0;
                _pmVwapFinal = double.NaN;
                _sessVwapNumer = 0; _sessVwapDenom = 0;
                _currentDate = DateTime.MinValue;
            }
        }

        private bool IsInPMSession(DateTime t)
        {
            int hhmm = t.Hour * 100 + t.Minute;
            int start = _pmStartHour * 100 + _pmStartMin;
            int end   = _pmEndHour   * 100 + _pmEndMin;
            return hhmm >= start && hhmm < end;
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 1) return;

            DateTime barTime = Time[0].ToLocalTime();

            // ── Day change reset ────────────────────────────────────────────────
            bool newDay = barTime.Date != _currentDate;
            if (newDay)
            {
                _currentDate   = barTime.Date;
                _pmHigh        = double.MinValue;
                _pmLow         = double.MaxValue;
                _pmReady       = false;
                _pmVwapNumer   = 0;
                _pmVwapDenom   = 0;
                // Session VWAP resets on new day
                _sessVwapNumer = 0;
                _sessVwapDenom = 0;
            }

            bool wasInPM = _inPM;
            _inPM = IsInPMSession(barTime);

            // ── PM session: track High/Low and VWAP ────────────────────────────
            if (_inPM)
            {
                _pmHigh = Math.Max(_pmHigh == double.MinValue ? High[0] : _pmHigh, High[0]);
                _pmLow  = Math.Min(_pmLow  == double.MaxValue ? Low[0]  : _pmLow,  Low[0]);

                double hlc3 = (High[0] + Low[0] + Close[0]) / 3.0;
                _pmVwapNumer += hlc3 * Volume[0];
                _pmVwapDenom += Volume[0];
            }

            // ── PM end: finalize levels ─────────────────────────────────────────
            if (wasInPM && !_inPM)
            {
                if (_pmHigh > double.MinValue) _pmHighFinal = _pmHigh;
                if (_pmLow  < double.MaxValue) _pmLowFinal  = _pmLow;
                _pmReady = !double.IsNaN(_pmHighFinal) && !double.IsNaN(_pmLowFinal);
                _pmVwapFinal = _pmVwapDenom > 0 ? _pmVwapNumer / _pmVwapDenom : double.NaN;
            }

            // ── Session VWAP (anchors at NY open 09:30 or day start) ────────────
            bool isNYOpen = barTime.Hour == 9 && barTime.Minute == 30;
            if (isNYOpen)
            {
                _sessVwapNumer = 0;
                _sessVwapDenom = 0;
            }
            double hlc3Sess = (High[0] + Low[0] + Close[0]) / 3.0;
            _sessVwapNumer += hlc3Sess * Volume[0];
            _sessVwapDenom += Volume[0];

            // ── Plot values ─────────────────────────────────────────────────────
            Values[0][0] = (_showPMLines && _pmReady) ? _pmHighFinal : double.NaN;
            Values[1][0] = (_showPMLines && _pmReady) ? _pmLowFinal  : double.NaN;
            Values[2][0] = (_showPMVwap  && !double.IsNaN(_pmVwapFinal)) ? _pmVwapFinal : double.NaN;
            Values[3][0] = (_showSessionVWAP && _sessVwapDenom > 0) ? _sessVwapNumer / _sessVwapDenom : double.NaN;
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Show Overnight High/Low", Order = 1, GroupName = "Session VWAP")]
        public bool ShowPMLines { get => _showPMLines; set => _showPMLines = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Overnight VWAP", Order = 2, GroupName = "Session VWAP")]
        public bool ShowPMVwap { get => _showPMVwap; set => _showPMVwap = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Session VWAP", Order = 3, GroupName = "Session VWAP")]
        public bool ShowSessionVWAP { get => _showSessionVWAP; set => _showSessionVWAP = value; }

        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "Pre-Market Start Hour (ET)", Order = 4, GroupName = "Session VWAP")]
        public int PmStartHour { get => _pmStartHour; set => _pmStartHour = value; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Pre-Market Start Min", Order = 5, GroupName = "Session VWAP")]
        public int PmStartMin { get => _pmStartMin; set => _pmStartMin = value; }

        [NinjaScriptProperty]
        [Range(0, 23)]
        [Display(Name = "Pre-Market End Hour (ET)", Order = 6, GroupName = "Session VWAP")]
        public int PmEndHour { get => _pmEndHour; set => _pmEndHour = value; }

        [NinjaScriptProperty]
        [Range(0, 59)]
        [Display(Name = "Pre-Market End Min", Order = 7, GroupName = "Session VWAP")]
        public int PmEndMin { get => _pmEndMin; set => _pmEndMin = value; }

        [XmlIgnore]
        [Display(Name = "Overnight High Color", Order = 8, GroupName = "Session VWAP")]
        public Brush PmHighColor { get => _pmHighColor; set => _pmHighColor = value; }

        [Browsable(false)]
        public string PmHighColorSerializable
        {
            get { return Serialize.BrushToString(_pmHighColor); }
            set { _pmHighColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Overnight Low Color", Order = 9, GroupName = "Session VWAP")]
        public Brush PmLowColor { get => _pmLowColor; set => _pmLowColor = value; }

        [Browsable(false)]
        public string PmLowColorSerializable
        {
            get { return Serialize.BrushToString(_pmLowColor); }
            set { _pmLowColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Overnight VWAP Color", Order = 10, GroupName = "Session VWAP")]
        public Brush PmVwapColor { get => _pmVwapColor; set => _pmVwapColor = value; }

        [Browsable(false)]
        public string PmVwapColorSerializable
        {
            get { return Serialize.BrushToString(_pmVwapColor); }
            set { _pmVwapColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Session VWAP Color", Order = 11, GroupName = "Session VWAP")]
        public Brush SessVwapColor { get => _sessVwapColor; set => _sessVwapColor = value; }

        [Browsable(false)]
        public string SessVwapColorSerializable
        {
            get { return Serialize.BrushToString(_sessVwapColor); }
            set { _sessVwapColor = Serialize.StringToBrush(value); }
        }

        // Public accessors for other indicators
        [Browsable(false)] [XmlIgnore]
        public Series<double> OvernightHigh => Values[0];

        [Browsable(false)] [XmlIgnore]
        public Series<double> OvernightLow => Values[1];

        [Browsable(false)] [XmlIgnore]
        public Series<double> OvernightVWAP => Values[2];

        [Browsable(false)] [XmlIgnore]
        public Series<double> SessionVWAPLine => Values[3];

        #endregion
    }
}
