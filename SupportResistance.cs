// Support & Resistance Zone Detection
// Logic ported from Pine Script reference (ta.pivothigh / ta.pivotlow equivalent)
// Pivots: asymmetric comparison — right (newer) strict, left (older) lenient (ties OK)
// Zone left edge anchored at actual pivot bar (CurrentBar - len), right edge extends to current bar

#region Using declarations
using System;
using System.Collections.Generic;
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
            public bool   Hidden;
            public int    Strength;
            public int    Sweeps;
            public int    StartBar;   // actual pivot bar index
            public string Tag, LblTag;
            public bool   InZone;
        }

        private readonly List<SRZone> _levels = new List<SRZone>();
        private int    _donchOs;
        private double _donchVal = double.NaN;
        private int    _bullCount, _bearCount;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name           = "Support Resistance";
                Description    = "S&R Zones — Pivots / Donchian / CSID · ATR depth · Mitigation · Overlap handling.";
                Calculate      = Calculate.OnBarClose;
                IsOverlay      = true;
                MaximumBarsLookBack      = MaximumBarsLookBack.Infinite;
                DrawOnPricePanel         = true;
                IsSuspendedWhileInactive = true;

                DetectionMethod  = SrDetectionMethod.Pivots;
                SwingSensitivity = 3;
                DisplayType      = SrDisplayType.Zones;
                ZoneDepthAtr     = 1.0;
                BreakoutBuffer   = 0.25;
                ZoneOpacity      = 65;
                OverlapHandling  = SrOverlapMode.HideOldest;
                MaxHistory       = 8;
                VisibleAbove     = 2;
                VisibleBelow     = 2;
                ShowLabels       = true;
                ShowBreakLines   = true;
                ResistColor      = new SolidColorBrush(Color.FromRgb(180, 30,  30));
                SupportColor     = new SolidColorBrush(Color.FromRgb( 30, 160, 50));
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
            try
            {
                int len = Math.Max(2, (int)SwingSensitivity);
                if (CurrentBar < len * 2 + 4) return;

                double atr = ATR(14)[0];
                if (atr <= 0 || double.IsNaN(atr)) return;

                double zDepth   = atr * ZoneDepthAtr;
                double breakBuf = atr * BreakoutBuffer;

                // ── Detect new levels ────────────────────────────────────────────
                double detPH = double.NaN, detPL = double.NaN;
                int pivotOffset = 0;

                switch (DetectionMethod)
                {
                    case SrDetectionMethod.Pivots:
                        detPH = DetectPivotHigh(len);
                        detPL = DetectPivotLow(len);
                        pivotOffset = len;
                        break;

                    case SrDetectionMethod.Donchian:
                        DonchianDetect(len, out detPH, out detPL);
                        break;

                    case SrDetectionMethod.CSID:
                        _bullCount = Close[0] > Open[0] ? _bullCount + 1 : 0;
                        _bearCount = Close[0] < Open[0] ? _bearCount + 1 : 0;
                        if (_bullCount == len) detPH = MAX(High, len)[0];
                        if (_bearCount == len) detPL = MIN(Low,  len)[0];
                        break;
                }

                if (!double.IsNaN(detPH))
                    AddZone(detPH + breakBuf, detPH - zDepth, detPH, false, pivotOffset);
                if (!double.IsNaN(detPL))
                    AddZone(detPL + zDepth, detPL - breakBuf, detPL, true, pivotOffset);

                // ── Update existing zones ────────────────────────────────────────
                for (int i = _levels.Count - 1; i >= 0; i--)
                {
                    SRZone z = _levels[i];
                    if (z.Mitigated) continue;

                    // Mitigation — Pine: close < btm (support) / close > top (resistance)
                    bool broken = z.IsSupport ? Close[0] < z.Bot : Close[0] > z.Top;
                    if (broken)
                    {
                        z.Mitigated = true; _levels[i] = z;
                        RemoveDrawObject(z.Tag);
                        RemoveDrawObject(z.LblTag);
                        if (ShowBreakLines)
                        {
                            Brush bc = z.IsSupport ? SupportColor : ResistColor;
                            Draw.HorizontalLine(this, z.Tag + "_brk", z.Base, bc, DashStyleHelper.Dash, 1);
                        }
                        continue;
                    }

                    // Sweep — Pine: low < btm AND min(close,open) > btm
                    bool swept = z.IsSupport
                        ? Low[0]  < z.Bot && Math.Min(Close[0], Open[0]) > z.Bot
                        : High[0] > z.Top && Math.Max(Close[0], Open[0]) < z.Top;
                    if (swept) { z.Sweeps++; _levels[i] = z; }

                    // Retest — Pine: high >= btm AND low <= top
                    bool inside = High[0] >= z.Bot && Low[0] <= z.Top;
                    if (inside && !z.InZone) { z.Strength++; z.InZone = true;  _levels[i] = z; }
                    else if (!inside && z.InZone) { z.InZone = false; _levels[i] = z; }

                    if (!z.Hidden) RedrawZone(_levels[i]);
                }

                EnforceVisibility();
            }
            catch { }
        }

        // ── Pivot detection — matches ta.pivothigh / ta.pivotlow (Pine Script) ──
        // Right side (newer bars, i < len): strict  — High[i] >= h disqualifies
        // Left side (older bars, i > len): lenient  — High[i] >  h disqualifies (ties OK)
        private double DetectPivotHigh(int len)
        {
            if (CurrentBar < len * 2 + 1) return double.NaN;
            double h = High[len];
            for (int i = 0; i < len; i++)
                if (High[i] >= h) return double.NaN;
            for (int i = len + 1; i <= len * 2; i++)
                if (High[i] > h) return double.NaN;
            return h;
        }

        private double DetectPivotLow(int len)
        {
            if (CurrentBar < len * 2 + 1) return double.NaN;
            double l = Low[len];
            for (int i = 0; i < len; i++)
                if (Low[i] <= l) return double.NaN;
            for (int i = len + 1; i <= len * 2; i++)
                if (Low[i] < l) return double.NaN;
            return l;
        }

        private void DonchianDetect(int len, out double detPH, out double detPL)
        {
            detPH = double.NaN; detPL = double.NaN;
            if (CurrentBar <= len) return;
            double dHigh = MAX(High, len)[0];
            double dLow  = MIN(Low,  len)[0];

            int newDir = MAX(High, len)[1] < dHigh ? 1 :
                         MIN(Low,  len)[1] > dLow  ? -1 : _donchOs;

            if (newDir != _donchOs && newDir != 0)
            {
                if (newDir ==  1 && !double.IsNaN(_donchVal)) detPL = _donchVal;
                if (newDir == -1 && !double.IsNaN(_donchVal)) detPH = _donchVal;
                _donchVal = newDir == 1 ? dHigh : dLow;
                _donchOs  = newDir;
            }
            else
            {
                if (_donchOs ==  1 && dHigh >= _donchVal) _donchVal = dHigh;
                if (_donchOs == -1 && dLow  <= _donchVal) _donchVal = dLow;
            }
        }

        // ── Add zone ─────────────────────────────────────────────────────────────
        private void AddZone(double top, double bot, double basePrice, bool isSupport, int pivotOffset)
        {
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SRZone ex = _levels[i];
                if (ex.Mitigated || ex.IsSupport != isSupport) continue;
                bool overlaps = Math.Max(bot, ex.Bot) < Math.Min(top, ex.Top);
                if (!overlaps) continue;

                switch (OverlapHandling)
                {
                    case SrOverlapMode.HideYoungest:
                        return;

                    case SrOverlapMode.HideOldest:
                        RemoveDrawObject(ex.Tag); RemoveDrawObject(ex.LblTag);
                        _levels.RemoveAt(i);
                        break;

                    case SrOverlapMode.MergeOverlapping:
                        SRZone m = ex;
                        m.Top = Math.Max(ex.Top, top);
                        m.Bot = Math.Min(ex.Bot, bot);
                        _levels[i] = m;
                        RedrawZone(m);
                        return;
                }
            }

            // Evict oldest active zone of same type when at capacity
            int activeCount = 0, oldestBar = int.MaxValue, oldestIdx = -1;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Mitigated || _levels[i].IsSupport != isSupport) continue;
                activeCount++;
                if (_levels[i].StartBar < oldestBar) { oldestBar = _levels[i].StartBar; oldestIdx = i; }
            }
            if (activeCount >= MaxHistory && oldestIdx >= 0)
            {
                RemoveDrawObject(_levels[oldestIdx].Tag);
                RemoveDrawObject(_levels[oldestIdx].LblTag);
                _levels.RemoveAt(oldestIdx);
            }

            string tag  = (isSupport ? "SR_S_" : "SR_R_") + CurrentBar;
            var z = new SRZone
            {
                Top = top, Bot = bot, Base = basePrice,
                IsSupport = isSupport, Mitigated = false, Hidden = false,
                Strength = 1, Sweeps = 0,
                StartBar = CurrentBar - pivotOffset,   // actual pivot bar
                Tag = tag, LblTag = tag + "_lbl"
            };
            _levels.Add(z);
            RedrawZone(z);
        }

        // ── Draw rectangle from pivot bar to current bar ──────────────────────────
        private void RedrawZone(SRZone z)
        {
            if (z.Hidden || z.Mitigated) return;
            Brush fill    = z.IsSupport ? (SupportColor ?? Brushes.Green) : (ResistColor ?? Brushes.Red);
            int   ago     = Math.Max(0, Math.Min(CurrentBar - z.StartBar, 4000));

            if (DisplayType == SrDisplayType.Zones)
            {
                // Pine Script equivalent: border_color=na, bgcolor=fill — no visible border
                // barsAgo=ago (pivot bar, left edge), barsAgo2=0 (current bar, right edge, extends each close)
                Draw.Rectangle(this, z.Tag, false, ago, z.Top, 0, z.Bot,
                    Brushes.Transparent, fill, ZoneOpacity);
            }
            else
            {
                Draw.HorizontalLine(this, z.Tag, z.Base, fill, DashStyleHelper.Solid, 2);
            }

            if (ShowLabels)
            {
                string lbl  = z.IsSupport ? "SUPPORT" : "RESISTANCE";
                double yLbl = (z.Top + z.Bot) * 0.5;   // center of zone, matching Pine valign=center
                Draw.Text(this, z.LblTag, true, lbl, ago, yLbl, 0,
                    Brushes.White, new SimpleFont("Arial", 9), System.Windows.TextAlignment.Left,
                    Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        // ── Visibility control — Pine Script: isAbove = zoneMid >= close ─────────
        private void EnforceVisibility()
        {
            int above = 0, below = 0;
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SRZone z = _levels[i];
                if (z.Mitigated) continue;

                double mid      = (z.Top + z.Bot) * 0.5;
                bool   isAbove  = mid >= Close[0];
                bool   shouldHide;

                if (isAbove) { above++; shouldHide = above > VisibleAbove; }
                else         { below++; shouldHide = below > VisibleBelow; }

                if (shouldHide && !z.Hidden)
                {
                    z.Hidden = true; _levels[i] = z;
                    RemoveDrawObject(z.Tag); RemoveDrawObject(z.LblTag);
                }
                else if (!shouldHide && z.Hidden)
                {
                    z.Hidden = false; _levels[i] = z;
                    RedrawZone(_levels[i]);
                }
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Display(Name = "Detection Method", Order = 1, GroupName = "Support & Resistance")]
        public SrDetectionMethod DetectionMethod { get; set; }

        [NinjaScriptProperty]
        [Range(2, 50)]
        [Display(Name = "Swing Sensitivity (bars)", Order = 2, GroupName = "Support & Resistance")]
        public double SwingSensitivity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Style", Order = 3, GroupName = "Support & Resistance")]
        public SrDisplayType DisplayType { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Zone Depth (× ATR)", Order = 4, GroupName = "Support & Resistance")]
        public double ZoneDepthAtr { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 2.0)]
        [Display(Name = "Breakout Buffer (× ATR)", Order = 5, GroupName = "Support & Resistance")]
        public double BreakoutBuffer { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Zone Fill Opacity %", Order = 6, GroupName = "Support & Resistance")]
        public int ZoneOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Overlap Handling", Order = 7, GroupName = "Support & Resistance")]
        public SrOverlapMode OverlapHandling { get; set; }

        [NinjaScriptProperty]
        [Range(1, 30)]
        [Display(Name = "Max S/R History", Order = 8, GroupName = "Support & Resistance")]
        public int MaxHistory { get; set; }

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Display(Name = "Visible Zones Above", Order = 9, GroupName = "Support & Resistance")]
        public int VisibleAbove { get; set; }

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Display(Name = "Visible Zones Below", Order = 10, GroupName = "Support & Resistance")]
        public int VisibleBelow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 11, GroupName = "Support & Resistance")]
        public bool ShowLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Break Lines", Order = 12, GroupName = "Support & Resistance")]
        public bool ShowBreakLines { get; set; }

        [XmlIgnore]
        [Display(Name = "Resistance Color", Order = 13, GroupName = "Support & Resistance")]
        public Brush ResistColor { get; set; }
        [Browsable(false)]
        public string ResistColorSerializable
        {
            get => Serialize.BrushToString(ResistColor);
            set => ResistColor = Serialize.StringToBrush(value);
        }

        [XmlIgnore]
        [Display(Name = "Support Color", Order = 14, GroupName = "Support & Resistance")]
        public Brush SupportColor { get; set; }
        [Browsable(false)]
        public string SupportColorSerializable
        {
            get => Serialize.BrushToString(SupportColor);
            set => SupportColor = Serialize.StringToBrush(value);
        }

        #endregion
    }
}
