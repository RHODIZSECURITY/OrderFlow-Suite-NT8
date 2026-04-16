// Converted from SMC / ICT Suite Pro Pine Script — SUPPORT & RESISTANCE section
// Detection methods: Pivots / Donchian / CSID
// Zone depth: ATR multiplier
// Display: Levels or Zones
// Overlap handling: None / Merge / Hide Overlapping (Oldest / Youngest Precedence)
// Visible count above/below price

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
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum SrDetectionMethod { Pivots, Donchian, CSID }
    public enum SrDisplayType     { Levels, Zones }
    public enum SrOverlapMode     { None, MergeOverlapping, HideOldest, HideYoungest }

    public class SupportResistance : Indicator
    {
        private struct SRZone
        {
            public double Top, Bot, Base;
            public bool   IsSupport;
            public bool   Mitigated;
            public int    Strength;
            public int    Sweeps;
            public int    StartBar;
            public string Tag, LblTag;
        }

        private readonly List<SRZone> _levels = new List<SRZone>();

        // Donchian state
        private int    _donchOs;
        private double _donchVal = double.NaN;
        private int    _bullCount, _bearCount;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name           = "Support Resistance";
                Description    = "S/R Zones — Pivots / Donchian / CSID detection with zone depth, overlap handling and visibility control. Ported from SMC/ICT Suite Pro.";
                Calculate      = Calculate.OnBarClose;
                IsOverlay      = true;
                MaxLookBack = MaximumBarsLookBack.Infinite;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;

                DetectionMethod  = SrDetectionMethod.Donchian;
                SwingSensitivity = 10;
                DisplayType      = SrDisplayType.Zones;
                ZoneDepthAtr     = 0.5;
                BreakoutBuffer   = 0.1;
                OverlapHandling  = SrOverlapMode.HideOldest;
                MaxHistory       = 6;
                VisibleAbove     = 3;
                VisibleBelow     = 3;
                ShowLabels       = true;
                ShowBreakLines   = true;
                ResistColor      = new SolidColorBrush(Color.FromArgb(153, 255,   0,   0));  // red 40% transp
                SupportColor     = new SolidColorBrush(Color.FromArgb(153,  43, 255,   0));  // green 40% transp
            }
            else if (State == State.DataLoaded)
            {
                _levels.Clear();
                _donchOs = 0; _donchVal = double.NaN;
                _bullCount = 0; _bearCount = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < (int)SwingSensitivity * 2 + 2) return;

            double atr     = ATR(14)[0];
            double zDepth  = atr * ZoneDepthAtr;
            double breakBuf = atr * BreakoutBuffer;
            int    len     = (int)SwingSensitivity;

            double detPH = double.NaN, detPL = double.NaN;

            switch (DetectionMethod)
            {
                case SrDetectionMethod.Pivots:
                    // Classic pivot high/low
                    bool isPH = true;
                    double pivH = High[len];
                    for (int i = 0; i < len * 2 + 1; i++) { if (i == len) continue; if (High[i] >= pivH) { isPH = false; break; } }
                    if (isPH) detPH = pivH;

                    bool isPL = true;
                    double pivL = Low[len];
                    for (int i = 0; i < len * 2 + 1; i++) { if (i == len) continue; if (Low[i] <= pivL) { isPL = false; break; } }
                    if (isPL) detPL = pivL;
                    break;

                case SrDetectionMethod.Donchian:
                    // Track direction changes in highest/lowest
                    double dHigh = MAX(High, len)[0];
                    double dLow  = MIN(Low,  len)[0];
                    int newOs = dHigh > (CurrentBar > len ? MAX(High, len)[1] : dHigh) ? 1
                              : dLow  < (CurrentBar > len ? MIN(Low,  len)[1] : dLow)  ? -1
                              : _donchOs;

                    if (newOs != _donchOs && newOs != 0)
                    {
                        if (newOs == 1 && !double.IsNaN(_donchVal)) detPL = _donchVal;
                        if (newOs == -1 && !double.IsNaN(_donchVal)) detPH = _donchVal;
                        _donchVal = newOs == 1 ? dHigh : dLow;
                    }
                    else
                    {
                        if (_donchOs == 1  && dHigh >= (_donchVal > double.MinValue ? _donchVal : double.MinValue)) _donchVal = dHigh;
                        if (_donchOs == -1 && dLow  <= (_donchVal < double.MaxValue ? _donchVal : double.MaxValue)) _donchVal = dLow;
                    }
                    _donchOs = newOs;
                    break;

                case SrDetectionMethod.CSID:
                    // Consecutive same-direction candles
                    _bullCount = Close[0] > Open[0] ? _bullCount + 1 : 0;
                    _bearCount = Close[0] < Open[0] ? _bearCount + 1 : 0;
                    if (_bullCount >= len) detPH = MAX(High, len)[0];
                    if (_bearCount >= len) detPL = MIN(Low,  len)[0];
                    break;
            }

            // ── Add detected levels ───────────────────────────────────────────
            if (!double.IsNaN(detPH))
            {
                double rTop = detPH + breakBuf;
                double rBot = detPH - zDepth;
                HandleStructure(rTop, rBot, detPH, false);
            }
            if (!double.IsNaN(detPL))
            {
                double sTop = detPL + zDepth;
                double sBot = detPL - breakBuf;
                HandleStructure(sTop, sBot, detPL, true);
            }

            // ── Mitigation & sweep tracking ───────────────────────────────────
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                var lvl = _levels[i];
                if (lvl.Mitigated) continue;

                // Mitigation: close breaks through zone
                bool broken = lvl.IsSupport
                    ? Close[0] < lvl.Bot
                    : Close[0] > lvl.Top;
                if (broken)
                {
                    var upd = lvl; upd.Mitigated = true; _levels[i] = upd;
                    if (ShowBreakLines)
                    {
                        Brush bc = lvl.IsSupport ? SupportColor : ResistColor;
                        Draw.HorizontalLine(this, lvl.Tag + "_brk", lvl.Base, bc, DashStyleHelper.Dash, 1);
                    }
                    else { RemoveDrawObject(lvl.Tag); RemoveDrawObject(lvl.LblTag); }
                    continue;
                }

                // Sweep: wick through zone then close back
                bool swept = lvl.IsSupport
                    ? Low[0] < lvl.Bot  && Close[0] > lvl.Bot
                    : High[0] > lvl.Top && Close[0] < lvl.Top;
                if (swept)
                {
                    var upd = lvl; upd.Sweeps++; _levels[i] = upd;
                }

                // Refresh draw
                RedrawLevel(_levels[i]);
            }

            // ── Visibility control: only show N zones above/below price ────────
            EnforceVisibility();
        }

        private void HandleStructure(double top, double bot, double basePrice, bool isSupport)
        {
            // Trim if over max history
            int count = 0;
            int oldestIdx = -1;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (!_levels[i].Mitigated && _levels[i].IsSupport == isSupport)
                { count++; oldestIdx = i; }
            }
            if (count >= MaxHistory && oldestIdx >= 0)
            {
                RemoveDrawObject(_levels[oldestIdx].Tag);
                RemoveDrawObject(_levels[oldestIdx].LblTag);
                _levels.RemoveAt(oldestIdx);
            }

            // Overlap handling
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                var l = _levels[i];
                if (l.Mitigated || l.IsSupport != isSupport) continue;
                bool overlaps = Math.Max(bot, l.Bot) < Math.Min(top, l.Top);
                if (!overlaps) continue;

                switch (OverlapHandling)
                {
                    case SrOverlapMode.HideOldest:
                        RemoveDrawObject(l.Tag); RemoveDrawObject(l.LblTag);
                        _levels.RemoveAt(i);
                        break;
                    case SrOverlapMode.HideYoungest:
                        return;
                    case SrOverlapMode.MergeOverlapping:
                        var merged = l;
                        merged.Top = Math.Max(l.Top, top);
                        merged.Bot = Math.Min(l.Bot, bot);
                        _levels[i] = merged;
                        RedrawLevel(merged);
                        return;
                }
            }

            string tag    = (isSupport ? "SR_S_" : "SR_R_") + CurrentBar;
            string lblTag = tag + "_lbl";
            _levels.Insert(0, new SRZone
            {
                Top = top, Bot = bot, Base = basePrice, IsSupport = isSupport,
                Strength = 1, Sweeps = 0, StartBar = CurrentBar,
                Tag = tag, LblTag = lblTag
            });
            RedrawLevel(_levels[0]);
        }

        private void RedrawLevel(SRZone lvl)
        {
            Brush c = lvl.IsSupport ? SupportColor : ResistColor;
            if (DisplayType == SrDisplayType.Zones)
                Draw.Rectangle(this, lvl.Tag, false, 0, lvl.Top, -30, lvl.Bot, c, c, 0);
            else
                Draw.HorizontalLine(this, lvl.Tag, lvl.Base, c, DashStyleHelper.Solid, 1);

            if (ShowLabels)
            {
                string lbl = string.Format("{0} S:{1} Sw:{2}",
                    lvl.IsSupport ? "SUP" : "RES", lvl.Strength, lvl.Sweeps);
                Draw.Text(this, lvl.LblTag, true, lbl,
                    0, lvl.IsSupport ? lvl.Bot - TickSize * 3 : lvl.Top + TickSize * 3, 0,
                    c, new SimpleFont("Arial", 7), TextAlignment.Right,
                    Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        private void EnforceVisibility()
        {
            int aboveCount = 0, belowCount = 0;
            for (int i = 0; i < _levels.Count; i++)
            {
                var lvl = _levels[i];
                if (lvl.Mitigated) continue;
                bool above = lvl.Bot > Close[0];
                bool below = lvl.Top < Close[0];
                if (above) { aboveCount++; if (aboveCount > VisibleAbove) { RemoveDrawObject(lvl.Tag); RemoveDrawObject(lvl.LblTag); } }
                if (below) { belowCount++; if (belowCount > VisibleBelow) { RemoveDrawObject(lvl.Tag); RemoveDrawObject(lvl.LblTag); } }
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Detection Method", Order = 1, GroupName = "Support & Resistance")]
        public SrDetectionMethod DetectionMethod { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Swing Sensitivity", Order = 2, GroupName = "Support & Resistance")]
        public double SwingSensitivity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Style", Order = 3, GroupName = "Support & Resistance")]
        public SrDisplayType DisplayType { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 5.0)]
        [Display(Name = "Zone Depth (ATR mult)", Order = 4, GroupName = "Support & Resistance")]
        public double ZoneDepthAtr { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 2.0)]
        [Display(Name = "Breakout Buffer (ATR mult)", Order = 5, GroupName = "Support & Resistance")]
        public double BreakoutBuffer { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Overlap Handling", Order = 6, GroupName = "Support & Resistance")]
        public SrOverlapMode OverlapHandling { get; set; }

        [NinjaScriptProperty]
        [Range(1, 30)]
        [Display(Name = "S/R History", Order = 7, GroupName = "Support & Resistance")]
        public int MaxHistory { get; set; }

        [NinjaScriptProperty]
        [Range(0, 30)]
        [Display(Name = "Visible Above Price", Order = 8, GroupName = "Support & Resistance")]
        public int VisibleAbove { get; set; }

        [NinjaScriptProperty]
        [Range(0, 30)]
        [Display(Name = "Visible Below Price", Order = 9, GroupName = "Support & Resistance")]
        public int VisibleBelow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 10, GroupName = "Support & Resistance")]
        public bool ShowLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Break Lines", Order = 11, GroupName = "Support & Resistance")]
        public bool ShowBreakLines { get; set; }

        [XmlIgnore]
        [Display(Name = "Resistance Color", Order = 12, GroupName = "Support & Resistance")]
        public Brush ResistColor { get; set; }

        [Browsable(false)]
        public string ResistColorSerializable
        {
            get { return Serialize.BrushToString(ResistColor); }
            set { ResistColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Support Color", Order = 13, GroupName = "Support & Resistance")]
        public Brush SupportColor { get; set; }

        [Browsable(false)]
        public string SupportColorSerializable
        {
            get { return Serialize.BrushToString(SupportColor); }
            set { SupportColor = Serialize.StringToBrush(value); }
        }

        #endregion
    }
}
