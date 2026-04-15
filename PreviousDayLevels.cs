// Converted from SMC / ICT Suite Pro Pine Script — MARKET LEVELS section
// Levels: PDH (Previous Day High), PDL (Previous Day Low)
//         PreH (current-day pre-market high), PreL (current-day pre-market low)
// Pre-market window: configurable start/end (default 04:00–09:30 ET)
// Pine: [prevHigh, prevLow] = request.security("D", [high[1], low[1]])
//       inPre → accumulate preHigh/preLow each bar during pre-market window

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class PreviousDayLevels : Indicator
    {
        // ── Previous-day levels (from daily data series) ──────────────────────
        private double _prevHigh, _prevLow;

        // ── Pre-market levels (accumulated intra-day) ─────────────────────────
        private DateTime _currentDate;
        private double   _preHigh, _preLow;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "Previous Day Levels";
                Description        = "PDH/PDL (previous day H/L) + Pre-market H/L. Ported from SMC/ICT Suite Pro Market Levels section.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                // PDH/PDL
                ShowPDH     = true;
                ShowPDL     = true;
                ShowMidPoint = false;
                ShowLabels  = true;

                // Pre-market H/L
                ShowPreH         = true;
                ShowPreL         = true;
                PreMarketStart   = 40000;   // 04:00:00
                PreMarketEnd     = 93000;   // 09:30:00

                // Colors (matching Pine defaults: pdhColor=blue, pdlColor=red, preH=aqua, preL=orange)
                HighColor   = new SolidColorBrush(Color.FromRgb(0,   120, 255));  // blue
                LowColor    = new SolidColorBrush(Color.FromRgb(255,  50,  50));  // red
                MidColor    = new SolidColorBrush(Color.FromRgb(100, 100, 100));  // gray
                PreHColor   = new SolidColorBrush(Color.FromRgb(0,   200, 255));  // aqua
                PreLColor   = new SolidColorBrush(Color.FromRgb(255, 165,   0));  // orange
            }
            else if (State == State.Configure)
            {
                // Add daily data series to get previous day H/L
                AddDataSeries(BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                _prevHigh = 0; _prevLow = 0;
                _preHigh  = double.MinValue;
                _preLow   = double.MaxValue;
                _currentDate = DateTime.MinValue;
            }
        }

        protected override void OnBarUpdate()
        {
            // ── Daily series: update PDH/PDL from previous session ────────────
            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] >= 2)
                {
                    _prevHigh = Highs[1][1];
                    _prevLow  = Lows[1][1];
                }
                return;
            }

            // ── Primary series ────────────────────────────────────────────────
            if (CurrentBars[0] < 1 || CurrentBars[1] < 2) return;

            // ── Pre-market accumulation ───────────────────────────────────────
            if (Time[0].Date != _currentDate)
            {
                _currentDate = Time[0].Date;
                _preHigh     = double.MinValue;
                _preLow      = double.MaxValue;
            }

            int now = ToTime(Time[0]);
            if (now >= PreMarketStart && now <= PreMarketEnd)
            {
                _preHigh = Math.Max(_preHigh, High[0]);
                _preLow  = Math.Min(_preLow,  Low[0]);
            }

            // ── Draw PDH / PDL ────────────────────────────────────────────────
            if (_prevHigh > 0 && _prevLow > 0)
            {
                if (ShowPDH)
                {
                    Draw.HorizontalLine(this, "PDH", _prevHigh, HighColor, DashStyleHelper.Solid, 2);
                    if (ShowLabels)
                        Draw.Text(this, "PDH_lbl", true,
                            string.Format("PDH {0}", FormatPrice(_prevHigh)),
                            0, _prevHigh + TickSize * 3, 0,
                            HighColor, new SimpleFont("Arial", 8), TextAlignment.Right,
                            Brushes.Transparent, Brushes.Transparent, 0);
                }

                if (ShowPDL)
                {
                    Draw.HorizontalLine(this, "PDL", _prevLow, LowColor, DashStyleHelper.Solid, 2);
                    if (ShowLabels)
                        Draw.Text(this, "PDL_lbl", true,
                            string.Format("PDL {0}", FormatPrice(_prevLow)),
                            0, _prevLow - TickSize * 5, 0,
                            LowColor, new SimpleFont("Arial", 8), TextAlignment.Right,
                            Brushes.Transparent, Brushes.Transparent, 0);
                }

                if (ShowMidPoint)
                {
                    double mid = (_prevHigh + _prevLow) / 2.0;
                    Draw.HorizontalLine(this, "PDMid", mid, MidColor, DashStyleHelper.Dash, 1);
                    if (ShowLabels)
                        Draw.Text(this, "PDMid_lbl", true,
                            string.Format("PDM {0}", FormatPrice(mid)),
                            0, mid + TickSize * 2, 0,
                            MidColor, new SimpleFont("Arial", 8), TextAlignment.Right,
                            Brushes.Transparent, Brushes.Transparent, 0);
                }
            }

            // ── Draw Pre-market H/L ───────────────────────────────────────────
            if (_preHigh > double.MinValue && ShowPreH)
            {
                Draw.HorizontalLine(this, "PreH", _preHigh, PreHColor, DashStyleHelper.Dash, 1);
                if (ShowLabels)
                    Draw.Text(this, "PreH_lbl", true,
                        string.Format("PreH {0}", FormatPrice(_preHigh)),
                        0, _preHigh + TickSize * 3, 0,
                        PreHColor, new SimpleFont("Arial", 8), TextAlignment.Right,
                        Brushes.Transparent, Brushes.Transparent, 0);
            }

            if (_preLow < double.MaxValue && ShowPreL)
            {
                Draw.HorizontalLine(this, "PreL", _preLow, PreLColor, DashStyleHelper.Dash, 1);
                if (ShowLabels)
                    Draw.Text(this, "PreL_lbl", true,
                        string.Format("PreL {0}", FormatPrice(_preLow)),
                        0, _preLow - TickSize * 5, 0,
                        PreLColor, new SimpleFont("Arial", 8), TextAlignment.Right,
                        Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        private string FormatPrice(double price)
        {
            return price >= 1000 ? price.ToString("F2") : price.ToString("F4");
        }

        #region Properties

        // PDH / PDL
        [NinjaScriptProperty]
        [Display(Name = "Show PDH", Order = 0, GroupName = "Previous Day Levels")]
        public bool ShowPDH { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show PDL", Order = 1, GroupName = "Previous Day Levels")]
        public bool ShowPDL { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Mid Point", Order = 2, GroupName = "Previous Day Levels")]
        public bool ShowMidPoint { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 3, GroupName = "Previous Day Levels")]
        public bool ShowLabels { get; set; }

        [XmlIgnore]
        [Display(Name = "PDH Color", Order = 4, GroupName = "Previous Day Levels")]
        public Brush HighColor { get; set; }
        [Browsable(false)]
        public string HighColorSerializable
        { get { return Serialize.BrushToString(HighColor); } set { HighColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "PDL Color", Order = 5, GroupName = "Previous Day Levels")]
        public Brush LowColor { get; set; }
        [Browsable(false)]
        public string LowColorSerializable
        { get { return Serialize.BrushToString(LowColor); } set { LowColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Mid Color", Order = 6, GroupName = "Previous Day Levels")]
        public Brush MidColor { get; set; }
        [Browsable(false)]
        public string MidColorSerializable
        { get { return Serialize.BrushToString(MidColor); } set { MidColor = Serialize.StringToBrush(value); } }

        // Pre-market H/L
        [NinjaScriptProperty]
        [Display(Name = "Show Pre-Market High", Order = 0, GroupName = "Pre-Market Levels")]
        public bool ShowPreH { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Pre-Market Low", Order = 1, GroupName = "Pre-Market Levels")]
        public bool ShowPreL { get; set; }

        [NinjaScriptProperty]
        [Range(0, 235959)]
        [Display(Name = "Pre-Market Start (HHmmss)", Order = 2, GroupName = "Pre-Market Levels")]
        public int PreMarketStart { get; set; }

        [NinjaScriptProperty]
        [Range(0, 235959)]
        [Display(Name = "Pre-Market End (HHmmss)", Order = 3, GroupName = "Pre-Market Levels")]
        public int PreMarketEnd { get; set; }

        [XmlIgnore]
        [Display(Name = "Pre-H Color", Order = 4, GroupName = "Pre-Market Levels")]
        public Brush PreHColor { get; set; }
        [Browsable(false)]
        public string PreHColorSerializable
        { get { return Serialize.BrushToString(PreHColor); } set { PreHColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Pre-L Color", Order = 5, GroupName = "Pre-Market Levels")]
        public Brush PreLColor { get; set; }
        [Browsable(false)]
        public string PreLColorSerializable
        { get { return Serialize.BrushToString(PreLColor); } set { PreLColor = Serialize.StringToBrush(value); } }

        #endregion
    }
}
