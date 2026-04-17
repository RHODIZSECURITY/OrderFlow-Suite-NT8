#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    [Gui.CategoryOrder("Previous Day Levels", 1)]
    [Gui.CategoryOrder("NY Pre-Market Levels", 2)]
    [Gui.CategoryOrder("Session VWAP", 3)]
    [Gui.CategoryOrder("ORB Pro", 4)]
    [Gui.CategoryOrder("Session Gap", 5)]
    [Gui.CategoryOrder("Kill Zones", 6)]
    public class LevelsSuite : Indicator
    {
        private double _prevHigh;
        private double _prevLow;

        private int _pmStart;
        private int _pmEnd;
        private bool _pmReady;
        private double _pmHigh;
        private double _pmLow;
        private double _pmVwapNumer;
        private double _pmVwapDenom;
        private double _pmHighFinal;
        private double _pmLowFinal;
        private double _pmVwapFinal;

        private DateTime _sessionDate;
        private double _sessVwapNumer;
        private double _sessVwapDenom;
        private double _sessSumVT2;  // Σ(vol * typical²) for VWAP variance

        private bool _orbReady;
        private bool _orbBrokenUp;
        private bool _orbBrokenDown;
        private double _orbHigh;
        private double _orbLow;
        private DateTime _orbEndTime;

        private readonly Queue<string> _orbSignalTags = new Queue<string>();
        private const int MaxOrbSignals = 200;

        private bool _gapUp;
        private bool _gapDown;
        private bool _gapFilled;
        private bool _gapEvaluated;
        private double _gapLine;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name = "LevelsSuite";
                Description = "Market levels: Previous Day, Pre-Market levels/VWAP, session VWAP, ORB Pro and session gaps.";
                Calculate = Calculate.OnEachTick;
                IsOverlay = true;
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel = true;

                _showPDH = true;
                _showPDL = true;
                _showPDMid = false;
                _showPDLabels = true;
                _pdhColor = Brushes.DodgerBlue;
                _pdlColor = Brushes.IndianRed;
                _pdMidColor = Brushes.Gray;

                _pmStart = 40000;
                _pmEnd = 93000;
                _showPMHigh = true;
                _showPMVwap = true;
                _showPMLabels = true;
                _pmHighColor = Brushes.Aqua;
                _pmLowColor = Brushes.Orange;
                _pmVwapColor = Brushes.MediumPurple;

                _showSessionVWAP = true;
                _sessionVwapColor = Brushes.Gold;
                _showVwapBands = true;
                _vwapSd1Color = new SolidColorBrush(Color.FromArgb(160, 255, 215, 0));
                _vwapSd2Color = new SolidColorBrush(Color.FromArgb(110, 218, 165, 32));

                _orbDuration = 5;
                _orbCutoffHour = 11;
                _enableBreak = true;
                _enableTrap = true;
                _enableReversal = true;
                _showFill = true;
                _showLabels = true;
                _orbHighColor = Brushes.LimeGreen;
                _orbLowColor = Brushes.Red;

                _gapMinAtr = 0.5;
                _showGapLine = true;
                _showGapLabel = true;
                _gapUpColor = Brushes.LimeGreen;
                _gapDownColor = Brushes.Crimson;
                _gapFilledColor = Brushes.Gray;

                _showAsiaKz   = true;
                _showLondonKz = true;
                _showNyAmKz   = true;
                _showKzLabels = true;
                _asiaKzColor   = new SolidColorBrush(Color.FromArgb(200, 100, 181, 246));  // light blue
                _londonKzColor = new SolidColorBrush(Color.FromArgb(200, 129, 199, 132));  // light green
                _nyAmKzColor   = new SolidColorBrush(Color.FromArgb(200, 255, 183, 77));   // amber
                _asiaKzDate = _londonKzDate = _nyAmKzDate = DateTime.MinValue;
                _asiaKzHigh = _londonKzHigh = _nyAmKzHigh = double.MinValue;
                _asiaKzLow  = _londonKzLow  = _nyAmKzLow  = double.MaxValue;

                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.HLine, "OvernightHigh");
                AddPlot(new Stroke(Brushes.Orange, 2), PlotStyle.HLine, "OvernightLow");
                AddPlot(new Stroke(Brushes.MediumPurple, 2), PlotStyle.Line, "OvernightVWAP");
                AddPlot(new Stroke(Brushes.Gold, 2), PlotStyle.Line, "SessionVWAPLine");
                AddPlot(new Stroke(Brushes.Gold, 1), PlotStyle.Line, "VwapSD1Up");
                AddPlot(new Stroke(Brushes.Gold, 1), PlotStyle.Line, "VwapSD1Down");
                AddPlot(new Stroke(Brushes.DarkGoldenrod, 1), PlotStyle.Line, "VwapSD2Up");
                AddPlot(new Stroke(Brushes.DarkGoldenrod, 1), PlotStyle.Line, "VwapSD2Down");
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Day, 1);
            }
            else if (State == State.DataLoaded)
            {
                _sessionDate = Core.Globals.MinDate;
                _pmVwapFinal = double.NaN;
                _sessSumVT2  = 0;
                _orbSignalTags.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == 1)
            {
                if (CurrentBars[1] >= 2)
                {
                    _prevHigh = Highs[1][1];
                    _prevLow = Lows[1][1];
                }
                return;
            }

            if (CurrentBars[0] < 1 || CurrentBars[1] < 2)
                return;

            DateTime t = Time[0];
            if (_sessionDate.Date != t.Date)
                ResetSessionState(t);

            int hhmmss = ToTime(t);
            bool inPm = IsInPM(hhmmss);
            bool nyCash = IsNYCash(t);
            double typical = (High[0] + Low[0] + Close[0]) / 3.0;

            UpdatePreMarket(inPm, typical);
            UpdateSessionVwapAndOrb(t, nyCash, typical);
            UpdateGap(t, nyCash);
            UpdateKillZones(t);
            DrawLevels();

            Values[0][0] = (_showPMHigh && _pmReady) ? _pmHighFinal : double.NaN;
            Values[1][0] = (_showPMHigh && _pmReady) ? _pmLowFinal : double.NaN;
            Values[2][0] = (_showPMVwap && !double.IsNaN(_pmVwapFinal)) ? _pmVwapFinal : double.NaN;
            Values[3][0] = (_showSessionVWAP && _sessVwapDenom > 0) ? _sessVwapNumer / _sessVwapDenom : double.NaN;

            if (_showSessionVWAP && _showVwapBands && _sessVwapDenom > 0)
            {
                double vwap     = _sessVwapNumer / _sessVwapDenom;
                double variance = _sessSumVT2 / _sessVwapDenom - vwap * vwap;
                double sd       = Math.Sqrt(Math.Max(0, variance));
                Values[4][0] = vwap + sd;
                Values[5][0] = vwap - sd;
                Values[6][0] = vwap + 2 * sd;
                Values[7][0] = vwap - 2 * sd;
            }
            else
                Values[4][0] = Values[5][0] = Values[6][0] = Values[7][0] = double.NaN;
        }

        private void UpdatePreMarket(bool inPm, double typical)
        {
            if (inPm)
            {
                _pmReady = true;
                _pmHigh = Math.Max(_pmHigh, High[0]);
                _pmLow = Math.Min(_pmLow, Low[0]);
                _pmVwapNumer += typical * Volume[0];
                _pmVwapDenom += Volume[0];

                _pmHighFinal = _pmHigh;
                _pmLowFinal = _pmLow;
                _pmVwapFinal = _pmVwapDenom > 0 ? _pmVwapNumer / _pmVwapDenom : double.NaN;
            }
        }

        private void UpdateSessionVwapAndOrb(DateTime t, bool nyCash, double typical)
        {
            if (!nyCash) return;

            _sessVwapNumer += typical * Volume[0];
            _sessVwapDenom += Volume[0];
            _sessSumVT2    += typical * typical * Volume[0];

            if (t <= _orbEndTime)
            {
                _orbHigh = Math.Max(_orbHigh, High[0]);
                _orbLow = Math.Min(_orbLow, Low[0]);
                return;
            }

            if (!_orbReady)
                _orbReady = true;

            if (_orbReady && t.Hour < _orbCutoffHour)
            {
                if (_enableBreak)
                {
                    bool breakUp = !_orbBrokenUp && Close[0] > _orbHigh;
                    bool breakDown = !_orbBrokenDown && Close[0] < _orbLow;
                    if (breakUp)
                    {
                        _orbBrokenUp = true;
                        if (_showLabels)
                            DrawOrbSignalText($"LS_ORB_BU_{CurrentBar}", "① Break↑", 0, High[0] + 2 * TickSize, _orbHighColor);
                    }
                    if (breakDown)
                    {
                        _orbBrokenDown = true;
                        if (_showLabels)
                            DrawOrbSignalText($"LS_ORB_BD_{CurrentBar}", "① Break↓", 0, Low[0] - 2 * TickSize, _orbLowColor);
                    }
                }

                if (_enableTrap && CurrentBars[0] >= 2)
                {
                    bool brokeAbove = High[1] > _orbHigh && Close[1] > _orbHigh;
                    bool brokeBelow = Low[1]  < _orbLow  && Close[1] < _orbLow;
                    if (brokeAbove && Close[0] < _orbHigh && !_orbBrokenDown)
                    {
                        _orbBrokenDown = true;
                        if (_showLabels)
                            DrawOrbSignalText($"LS_ORB_TS_{CurrentBar}", "② Trap↓", 0, High[0] + 2 * TickSize, _orbLowColor);
                    }
                    if (brokeBelow && Close[0] > _orbLow && !_orbBrokenUp)
                    {
                        _orbBrokenUp = true;
                        if (_showLabels)
                            DrawOrbSignalText($"LS_ORB_TL_{CurrentBar}", "② Trap↑", 0, Low[0] - 2 * TickSize, _orbHighColor);
                    }
                }

                if (_enableReversal)
                {
                    double mid = (High[0] + Low[0]) * 0.5;
                    if (High[0] > _orbHigh && Close[0] < mid && Close[0] < _orbHigh)
                        DrawOrbSignalDiamond($"LS_ORB_RS_{CurrentBar}", 0, High[0] + TickSize * 4, _orbLowColor);
                    if (Low[0] < _orbLow && Close[0] > mid && Close[0] > _orbLow)
                        DrawOrbSignalDiamond($"LS_ORB_RL_{CurrentBar}", 0, Low[0] - TickSize * 4, _orbHighColor);
                }
            }
        }

        private void UpdateGap(DateTime t, bool nyCash)
        {
            if (!nyCash || _gapEvaluated || CurrentBar < 2)
                return;

            // Evaluate only once around NY open for TV-like session gap behavior.
            if (t.TimeOfDay < new TimeSpan(9, 30, 0) || t.TimeOfDay > new TimeSpan(9, 31, 30))
                return;

            double atr = ATR(14)[0];
            if (atr <= 0 || _prevHigh <= 0 || _prevLow <= 0)
                return;

            _gapUp = Open[0] > _prevHigh && (Open[0] - _prevHigh) / atr >= _gapMinAtr;
            _gapDown = Open[0] < _prevLow && (_prevLow - Open[0]) / atr >= _gapMinAtr;
            _gapLine = _gapUp ? _prevHigh : _gapDown ? _prevLow : double.NaN;
            _gapEvaluated = true;

            if (_showGapLabel && !double.IsNaN(_gapLine))
                Draw.Text(this, $"LS_GAP_LBL_{_sessionDate:yyyyMMdd}", _gapUp ? "GAP UP" : "GAP DOWN", 0, _gapLine, _gapUp ? _gapUpColor : _gapDownColor);
        }

        private void UpdateKillZones(DateTime t)
        {
            TimeSpan ts = t.TimeOfDay;

            // Asia KZ: 20:00–23:59 ET — resets each evening
            if (_showAsiaKz)
            {
                if (ts.Hours >= 20 && t.Date != _asiaKzDate.Date)
                {
                    _asiaKzDate = t; _asiaKzHigh = double.MinValue; _asiaKzLow = double.MaxValue;
                }
                if (ts.Hours >= 20)
                {
                    _asiaKzHigh = Math.Max(_asiaKzHigh, High[0]);
                    _asiaKzLow  = Math.Min(_asiaKzLow,  Low[0]);
                }
                if (_asiaKzDate != DateTime.MinValue && _asiaKzHigh > double.MinValue)
                {
                    string d = _asiaKzDate.ToString("yyyyMMdd");
                    Draw.HorizontalLine(this, $"LS_ASIA_H_{d}", _asiaKzHigh, _asiaKzColor, DashStyleHelper.Dot, 1);
                    Draw.HorizontalLine(this, $"LS_ASIA_L_{d}", _asiaKzLow,  _asiaKzColor, DashStyleHelper.Dot, 1);
                    if (_showKzLabels)
                        Draw.Text(this, $"LS_ASIA_LBL_{d}", "Asia", 0, _asiaKzHigh + TickSize * 3, _asiaKzColor);
                }
            }

            // London KZ: 02:00–05:00 ET
            if (_showLondonKz)
            {
                bool inLon = ts.Hours >= 2 && ts.Hours < 5;
                if (inLon && t.Date != _londonKzDate.Date)
                {
                    _londonKzDate = t; _londonKzHigh = double.MinValue; _londonKzLow = double.MaxValue;
                }
                if (inLon)
                {
                    _londonKzHigh = Math.Max(_londonKzHigh, High[0]);
                    _londonKzLow  = Math.Min(_londonKzLow,  Low[0]);
                }
                if (_londonKzDate != DateTime.MinValue && _londonKzHigh > double.MinValue)
                {
                    string d = _londonKzDate.ToString("yyyyMMdd");
                    Draw.HorizontalLine(this, $"LS_LON_H_{d}", _londonKzHigh, _londonKzColor, DashStyleHelper.Dash, 1);
                    Draw.HorizontalLine(this, $"LS_LON_L_{d}", _londonKzLow,  _londonKzColor, DashStyleHelper.Dash, 1);
                    if (_showKzLabels)
                        Draw.Text(this, $"LS_LON_LBL_{d}", "London", 0, _londonKzHigh + TickSize * 3, _londonKzColor);
                }
            }

            // NY AM KZ: 07:00–10:00 ET
            if (_showNyAmKz)
            {
                bool inNy = ts.Hours >= 7 && ts.Hours < 10;
                if (inNy && t.Date != _nyAmKzDate.Date)
                {
                    _nyAmKzDate = t; _nyAmKzHigh = double.MinValue; _nyAmKzLow = double.MaxValue;
                }
                if (inNy)
                {
                    _nyAmKzHigh = Math.Max(_nyAmKzHigh, High[0]);
                    _nyAmKzLow  = Math.Min(_nyAmKzLow,  Low[0]);
                }
                if (_nyAmKzDate != DateTime.MinValue && _nyAmKzHigh > double.MinValue)
                {
                    string d = _nyAmKzDate.ToString("yyyyMMdd");
                    Draw.HorizontalLine(this, $"LS_NY_H_{d}", _nyAmKzHigh, _nyAmKzColor, DashStyleHelper.Solid, 1);
                    Draw.HorizontalLine(this, $"LS_NY_L_{d}", _nyAmKzLow,  _nyAmKzColor, DashStyleHelper.Solid, 1);
                    if (_showKzLabels)
                        Draw.Text(this, $"LS_NY_LBL_{d}", "NY AM", 0, _nyAmKzHigh + TickSize * 3, _nyAmKzColor);
                }
            }
        }

        private void ResetSessionState(DateTime t)
        {
            _sessionDate = t.Date;

            _pmReady = false;
            _pmHigh = double.MinValue;
            _pmLow = double.MaxValue;
            _pmVwapNumer = 0;
            _pmVwapDenom = 0;
            _pmHighFinal = double.NaN;
            _pmLowFinal = double.NaN;
            _pmVwapFinal = double.NaN;

            _sessVwapNumer = 0;
            _sessVwapDenom = 0;
            _sessSumVT2    = 0;

            DateTime nyOpen = new DateTime(t.Year, t.Month, t.Day, 9, 30, 0);
            _orbEndTime = nyOpen.AddMinutes(_orbDuration);
            _orbReady = false;
            _orbBrokenUp = false;
            _orbBrokenDown = false;
            _orbHigh = double.MinValue;
            _orbLow = double.MaxValue;

            _gapUp = false;
            _gapDown = false;
            _gapFilled = false;
            _gapEvaluated = false;
            _gapLine = double.NaN;
        }

        private void DrawLevels()
        {
            if (_showPDH)
                Draw.HorizontalLine(this, "LS_PDH", _prevHigh, _pdhColor);
            if (_showPDL)
                Draw.HorizontalLine(this, "LS_PDL", _prevLow, _pdlColor);
            if (_showPDMid)
                Draw.HorizontalLine(this, "LS_PDM", (_prevHigh + _prevLow) * 0.5, _pdMidColor);

            if (_showPMHigh && _pmReady)
            {
                Draw.HorizontalLine(this, "LS_PMH", _pmHighFinal, _pmHighColor);
                Draw.HorizontalLine(this, "LS_PML", _pmLowFinal, _pmLowColor);
                if (_showPMLabels)
                {
                    Draw.Text(this, "LS_PMH_LBL", "PRE HIGH", 0, _pmHighFinal, _pmHighColor);
                    Draw.Text(this, "LS_PML_LBL", "PRE LOW", 0, _pmLowFinal, _pmLowColor);
                }
            }

            if (_showPMVwap && !double.IsNaN(_pmVwapFinal))
                Draw.HorizontalLine(this, "LS_PMVWAP", _pmVwapFinal, _pmVwapColor);

            if (_orbReady)
            {
                Draw.HorizontalLine(this, "LS_ORBH", _orbHigh, _orbHighColor);
                Draw.HorizontalLine(this, "LS_ORBL", _orbLow, _orbLowColor);
                if (_showFill)
                    Draw.Region(this, "LS_ORB_FILL", 0, 0, _orbHigh, _orbLow, Brushes.Transparent, Brushes.DodgerBlue, 8);
            }

            if (!double.IsNaN(_gapLine))
            {
                _gapFilled = Low[0] <= _gapLine && High[0] >= _gapLine;
                if (_showGapLine)
                    Draw.HorizontalLine(this, "LS_GAP", _gapLine, _gapFilled ? _gapFilledColor : (_gapUp ? _gapUpColor : _gapDownColor));
            }
        }

        private void DrawOrbSignalText(string tag, string text, int barsAgo, double price, Brush color)
        {
            Draw.Text(this, tag, text, barsAgo, price, color);
            _orbSignalTags.Enqueue(tag);
            if (_orbSignalTags.Count > MaxOrbSignals) RemoveDrawObject(_orbSignalTags.Dequeue());
        }

        private void DrawOrbSignalDiamond(string tag, int barsAgo, double price, Brush color)
        {
            Draw.Diamond(this, tag, false, barsAgo, price, color);
            _orbSignalTags.Enqueue(tag);
            if (_orbSignalTags.Count > MaxOrbSignals) RemoveDrawObject(_orbSignalTags.Dequeue());
        }

        private bool IsNYCash(DateTime t)
        {
            TimeSpan ts = t.TimeOfDay;
            return ts >= new TimeSpan(9, 30, 0) && ts < new TimeSpan(16, 0, 0);
        }

        private bool IsInPM(int hhmmss) => hhmmss >= _pmStart && hhmmss <= _pmEnd;

        private bool _showPDH, _showPDL, _showPDMid, _showPDLabels;
        private Brush _pdhColor, _pdlColor, _pdMidColor;
        private bool _showPMHigh, _showPMVwap, _showPMLabels;
        private Brush _pmHighColor, _pmLowColor, _pmVwapColor;
        private bool _showSessionVWAP;
        private Brush _sessionVwapColor;
        private bool _showVwapBands;
        private Brush _vwapSd1Color, _vwapSd2Color;
        private int _orbDuration, _orbCutoffHour;
        private bool _enableBreak, _enableTrap, _enableReversal, _showFill, _showLabels;
        private Brush _orbHighColor, _orbLowColor;
        private double _gapMinAtr;
        private bool _showGapLine, _showGapLabel;
        private Brush _gapUpColor, _gapDownColor, _gapFilledColor;

        // Kill Zones
        private bool  _showAsiaKz, _showLondonKz, _showNyAmKz, _showKzLabels;
        private Brush _asiaKzColor, _londonKzColor, _nyAmKzColor;
        private double   _asiaKzHigh, _asiaKzLow;
        private double   _londonKzHigh, _londonKzLow;
        private double   _nyAmKzHigh, _nyAmKzLow;
        private DateTime _asiaKzDate, _londonKzDate, _nyAmKzDate;

        [NinjaScriptProperty, Display(Name = "ShowPDH", GroupName = "Previous Day Levels", Order = 1)] public bool ShowPDH { get => _showPDH; set => _showPDH = value; }
        [NinjaScriptProperty, Display(Name = "ShowPDL", GroupName = "Previous Day Levels", Order = 2)] public bool ShowPDL { get => _showPDL; set => _showPDL = value; }
        [NinjaScriptProperty, Display(Name = "ShowPDMid", GroupName = "Previous Day Levels", Order = 3)] public bool ShowPDMid { get => _showPDMid; set => _showPDMid = value; }
        [NinjaScriptProperty, Display(Name = "ShowPDLabels", GroupName = "Previous Day Levels", Order = 4)] public bool ShowPDLabels { get => _showPDLabels; set => _showPDLabels = value; }

        [XmlIgnore, Display(Name = "PDH Color", GroupName = "Previous Day Levels", Order = 10)] public Brush PdhColor { get => _pdhColor; set => _pdhColor = value; }
        [Browsable(false)] public string PdhColorSerializable { get => Serialize.BrushToString(_pdhColor); set => _pdhColor = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "PDL Color", GroupName = "Previous Day Levels", Order = 11)] public Brush PdlColor { get => _pdlColor; set => _pdlColor = value; }
        [Browsable(false)] public string PdlColorSerializable { get => Serialize.BrushToString(_pdlColor); set => _pdlColor = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Mid Color", GroupName = "Previous Day Levels", Order = 12)] public Brush PdMidColor { get => _pdMidColor; set => _pdMidColor = value; }
        [Browsable(false)] public string PdMidColorSerializable { get => Serialize.BrushToString(_pdMidColor); set => _pdMidColor = Serialize.StringToBrush(value); }

        [NinjaScriptProperty, Range(0, 235959), Display(Name = "PmStart", GroupName = "NY Pre-Market Levels", Order = 1)] public int PmStart { get => _pmStart; set => _pmStart = value; }
        [NinjaScriptProperty, Range(0, 235959), Display(Name = "PmEnd", GroupName = "NY Pre-Market Levels", Order = 2)] public int PmEnd { get => _pmEnd; set => _pmEnd = value; }
        [NinjaScriptProperty, Display(Name = "ShowPMHigh", GroupName = "NY Pre-Market Levels", Order = 3)] public bool ShowPMHigh { get => _showPMHigh; set => _showPMHigh = value; }
        [NinjaScriptProperty, Display(Name = "ShowPMVwap", GroupName = "NY Pre-Market Levels", Order = 4)] public bool ShowPMVwap { get => _showPMVwap; set => _showPMVwap = value; }
        [NinjaScriptProperty, Display(Name = "ShowPMLabels", GroupName = "NY Pre-Market Levels", Order = 5)] public bool ShowPMLabels { get => _showPMLabels; set => _showPMLabels = value; }

        [NinjaScriptProperty, Display(Name = "ShowSessionVWAP", GroupName = "Session VWAP", Order = 1)] public bool ShowSessionVWAP { get => _showSessionVWAP; set => _showSessionVWAP = value; }
        [NinjaScriptProperty, Display(Name = "Show VWAP Bands (±1σ/±2σ)", GroupName = "Session VWAP", Order = 2)] public bool ShowVwapBands { get => _showVwapBands; set => _showVwapBands = value; }
        [XmlIgnore, Display(Name = "VWAP SD1 Color", GroupName = "Session VWAP", Order = 3)] public Brush VwapSd1Color { get => _vwapSd1Color; set => _vwapSd1Color = value; }
        [Browsable(false)] public string VwapSd1ColorSerializable { get => Serialize.BrushToString(_vwapSd1Color); set => _vwapSd1Color = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "VWAP SD2 Color", GroupName = "Session VWAP", Order = 4)] public Brush VwapSd2Color { get => _vwapSd2Color; set => _vwapSd2Color = value; }
        [Browsable(false)] public string VwapSd2ColorSerializable { get => Serialize.BrushToString(_vwapSd2Color); set => _vwapSd2Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty, Range(1, 120), Display(Name = "OrbDuration", GroupName = "ORB Pro", Order = 1)] public int OrbDuration { get => _orbDuration; set => _orbDuration = value; }
        [NinjaScriptProperty, Range(9, 16), Display(Name = "OrbCutoffHour", GroupName = "ORB Pro", Order = 2)] public int OrbCutoffHour { get => _orbCutoffHour; set => _orbCutoffHour = value; }
        [NinjaScriptProperty, Display(Name = "EnableBreak", GroupName = "ORB Pro", Order = 3)] public bool EnableBreak { get => _enableBreak; set => _enableBreak = value; }
        [NinjaScriptProperty, Display(Name = "EnableTrap", GroupName = "ORB Pro", Order = 4)] public bool EnableTrap { get => _enableTrap; set => _enableTrap = value; }
        [NinjaScriptProperty, Display(Name = "EnableReversal", GroupName = "ORB Pro", Order = 5)] public bool EnableReversal { get => _enableReversal; set => _enableReversal = value; }
        [NinjaScriptProperty, Display(Name = "ShowFill", GroupName = "ORB Pro", Order = 6)] public bool ShowFill { get => _showFill; set => _showFill = value; }
        [NinjaScriptProperty, Display(Name = "ShowLabels", GroupName = "ORB Pro", Order = 7)] public bool ShowLabels { get => _showLabels; set => _showLabels = value; }

        [NinjaScriptProperty, Range(0.0, 10.0), Display(Name = "GapMinAtr", GroupName = "Session Gap", Order = 1)] public double GapMinAtr { get => _gapMinAtr; set => _gapMinAtr = value; }
        [NinjaScriptProperty, Display(Name = "ShowGapLine", GroupName = "Session Gap", Order = 2)] public bool ShowGapLine { get => _showGapLine; set => _showGapLine = value; }
        [NinjaScriptProperty, Display(Name = "ShowGapLabel", GroupName = "Session Gap", Order = 3)] public bool ShowGapLabel { get => _showGapLabel; set => _showGapLabel = value; }

        [NinjaScriptProperty, Display(Name = "Show Asia KZ (20:00-00:00 ET)", GroupName = "Kill Zones", Order = 1)] public bool ShowAsiaKz { get => _showAsiaKz; set => _showAsiaKz = value; }
        [NinjaScriptProperty, Display(Name = "Show London KZ (02:00-05:00 ET)", GroupName = "Kill Zones", Order = 2)] public bool ShowLondonKz { get => _showLondonKz; set => _showLondonKz = value; }
        [NinjaScriptProperty, Display(Name = "Show NY AM KZ (07:00-10:00 ET)", GroupName = "Kill Zones", Order = 3)] public bool ShowNyAmKz { get => _showNyAmKz; set => _showNyAmKz = value; }
        [NinjaScriptProperty, Display(Name = "Show KZ Labels", GroupName = "Kill Zones", Order = 4)] public bool ShowKzLabels { get => _showKzLabels; set => _showKzLabels = value; }

        [XmlIgnore, Display(Name = "Asia KZ Color", GroupName = "Kill Zones", Order = 5)] public Brush AsiaKzColor { get => _asiaKzColor; set => _asiaKzColor = value; }
        [Browsable(false)] public string AsiaKzColorSerializable { get => Serialize.BrushToString(_asiaKzColor); set => _asiaKzColor = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "London KZ Color", GroupName = "Kill Zones", Order = 6)] public Brush LondonKzColor { get => _londonKzColor; set => _londonKzColor = value; }
        [Browsable(false)] public string LondonKzColorSerializable { get => Serialize.BrushToString(_londonKzColor); set => _londonKzColor = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "NY AM KZ Color", GroupName = "Kill Zones", Order = 7)] public Brush NyAmKzColor { get => _nyAmKzColor; set => _nyAmKzColor = value; }
        [Browsable(false)] public string NyAmKzColorSerializable { get => Serialize.BrushToString(_nyAmKzColor); set => _nyAmKzColor = Serialize.StringToBrush(value); }

        [Browsable(false), XmlIgnore] public Series<double> OvernightHigh  => Values[0];
        [Browsable(false), XmlIgnore] public Series<double> OvernightLow   => Values[1];
        [Browsable(false), XmlIgnore] public Series<double> OvernightVWAP  => Values[2];
        [Browsable(false), XmlIgnore] public Series<double> SessionVWAPLine => Values[3];
        [Browsable(false), XmlIgnore] public Series<double> VwapSD1Up      => Values[4];
        [Browsable(false), XmlIgnore] public Series<double> VwapSD1Down    => Values[5];
        [Browsable(false), XmlIgnore] public Series<double> VwapSD2Up      => Values[6];
        [Browsable(false), XmlIgnore] public Series<double> VwapSD2Down    => Values[7];
        [Browsable(false)] public double PrevHigh => _prevHigh;
        [Browsable(false)] public double PrevLow => _prevLow;
        [Browsable(false)] public bool OrbReady => _orbReady;
        [Browsable(false)] public double OrbHigh => _orbHigh;
        [Browsable(false)] public double OrbLow => _orbLow;
        [Browsable(false)] public bool GapUp => _gapUp;
        [Browsable(false)] public bool GapDown => _gapDown;
        [Browsable(false)] public bool GapFilled => _gapFilled;
    }
}
