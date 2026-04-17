// Support & Resistance Zone Detection
// Detection methods: Pivots / Donchian / CSID
// Zones: ATR-depth rectangles anchored at detection bar, extending right
// Mitigation: close beyond zone → dashed break line
// Overlap: None / Merge / Hide

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using System.Windows;
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
            public bool   Hidden;        // hidden by visibility enforcement
            public int    Strength;      // retest count
            public int    Sweeps;
            public int    StartBar;
            public string Tag, LblTag;
            public bool   InZone;        // price was inside zone last bar
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
                SwingSensitivity = 5;
                DisplayType      = SrDisplayType.Zones;
                ZoneDepthAtr     = 0.5;
                BreakoutBuffer   = 0.1;
                ZoneOpacity      = 20;
                OverlapHandling  = SrOverlapMode.HideOldest;
                MaxHistory       = 8;
                VisibleAbove     = 4;
                VisibleBelow     = 4;
                ShowLabels       = true;
                ShowBreakLines   = true;
                ResistColor      = new SolidColorBrush(Color.FromArgb(200, 255,  80,  80));
                SupportColor     = new SolidColorBrush(Color.FromArgb(200,  80, 200,  80));
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
            if (CurrentBar < (int)SwingSensitivity * 2 + 4) return;

            double atr      = ATR(14)[0];
            if (atr <= 0) return;
            double zDepth   = atr * ZoneDepthAtr;
            double breakBuf = atr * BreakoutBuffer;
            int    len      = Math.Max(2, (int)SwingSensitivity);

            // ── Detect new pivot highs / lows ─────────────────────────────────
            double detPH = double.NaN, detPL = double.NaN;

            switch (DetectionMethod)
            {
                case SrDetectionMethod.Pivots:
                    detPH = DetectPivotHigh(len);
                    detPL = DetectPivotLow(len);
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
                AddZone(detPH + breakBuf, detPH - zDepth, detPH, false);
            if (!double.IsNaN(detPL))
                AddZone(detPL + zDepth, detPL - breakBuf, detPL, true);

            // ── Update existing zones (mitigation, retests, sweeps) ───────────
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SRZone z = _levels[i];
                if (z.Mitigated) continue;

                // Mitigation: close breaks cleanly through zone
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

                // Liquidity sweep: wick through then close back
                bool swept = z.IsSupport
                    ? Low[0]  < z.Bot  && Close[0] > z.Bot
                    : High[0] > z.Top  && Close[0] < z.Top;
                if (swept) { z.Sweeps++; _levels[i] = z; }

                // Retest strength: count each fresh visit into zone
                bool inside = High[0] >= z.Bot && Low[0] <= z.Top;
                if (inside && !z.InZone)
                {
                    z.Strength++; z.InZone = true; _levels[i] = z;
                }
                else if (!inside && z.InZone)
                {
                    z.InZone = false; _levels[i] = z;
                }

                if (!z.Hidden) RedrawZone(_levels[i]);
            }

            EnforceVisibility();
        }

        // ── Pivot detection helpers ───────────────────────────────────────────
        private double DetectPivotHigh(int len)
        {
            // Need len bars on each side confirmed → fires len bars after the pivot
            if (CurrentBar < len * 2 + 1) return double.NaN;
            double h = High[len];
            for (int i = 0; i <= len * 2; i++)
            {
                if (i == len) continue;
                if (High[i] >= h) return double.NaN;
            }
            return h;
        }

        private double DetectPivotLow(int len)
        {
            if (CurrentBar < len * 2 + 1) return double.NaN;
            double l = Low[len];
            for (int i = 0; i <= len * 2; i++)
            {
                if (i == len) continue;
                if (Low[i] <= l) return double.NaN;
            }
            return l;
        }

        private void DonchianDetect(int len, out double detPH, out double detPL)
        {
            detPH = double.NaN; detPL = double.NaN;
            double dHigh = MAX(High, len)[0];
            double dLow  = MIN(Low,  len)[0];

            // Direction: 1 = up (new highest high), -1 = down (new lowest low)
            int prev = CurrentBar > len ? (MAX(High, len)[1] < dHigh ? 1 :
                                           MIN(Low,  len)[1] > dLow  ? -1 : _donchOs)
                                        : _donchOs;

            if (prev != _donchOs && prev != 0)
            {
                // Direction flip: previous extreme becomes a level
                if (prev ==  1 && !double.IsNaN(_donchVal)) detPL = _donchVal;
                if (prev == -1 && !double.IsNaN(_donchVal)) detPH = _donchVal;
                _donchVal = prev == 1 ? dHigh : dLow;
            }
            else
            {
                // Extend extreme in current direction
                if (_donchOs ==  1 && dHigh > _donchVal) _donchVal = dHigh;
                if (_donchOs == -1 && dLow  < _donchVal) _donchVal = dLow;
            }
            _donchOs = prev == 0 ? _donchOs : prev;
        }

        // ── Add / merge zone ──────────────────────────────────────────────────
        private void AddZone(double top, double bot, double basePrice, bool isSupport)
        {
            // Overlap handling
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SRZone ex = _levels[i];
                if (ex.Mitigated || ex.IsSupport != isSupport) continue;
                bool overlaps = Math.Max(bot, ex.Bot) < Math.Min(top, ex.Top);
                if (!overlaps) continue;

                switch (OverlapHandling)
                {
                    case SrOverlapMode.HideYoungest:
                        return;  // discard incoming

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

            // Enforce max history: remove the OLDEST active zone of same type
            int activeCount = 0;
            int oldestBar = int.MaxValue, oldestIdx = -1;
            for (int i = 0; i < _levels.Count; i++)
            {
                if (_levels[i].Mitigated || _levels[i].IsSupport != isSupport) continue;
                activeCount++;
                if (_levels[i].StartBar < oldestBar)
                {
                    oldestBar = _levels[i].StartBar;
                    oldestIdx = i;
                }
            }
            if (activeCount >= MaxHistory && oldestIdx >= 0)
            {
                RemoveDrawObject(_levels[oldestIdx].Tag);
                RemoveDrawObject(_levels[oldestIdx].LblTag);
                _levels.RemoveAt(oldestIdx);
            }

            string tag    = (isSupport ? "SR_S_" : "SR_R_") + CurrentBar;
            string lblTag = tag + "_lbl";
            var z = new SRZone
            {
                Top = top, Bot = bot, Base = basePrice,
                IsSupport = isSupport, Mitigated = false, Hidden = false,
                Strength = 1, Sweeps = 0, StartBar = CurrentBar,
                Tag = tag, LblTag = lblTag
            };
            _levels.Add(z);
            RedrawZone(z);
        }

        // ── Draw zone rectangle / line ─────────────────────────────────────────
        private void RedrawZone(SRZone z)
        {
            if (z.Hidden || z.Mitigated) return;
            Brush c   = z.IsSupport ? SupportColor : ResistColor;
            int   ago = CurrentBar - z.StartBar;  // bars since detection (anchors left edge)

            if (DisplayType == SrDisplayType.Zones)
            {
                // Extend rectangle: left edge at detection bar, right edge 300 bars into future
                Draw.Rectangle(this, z.Tag, false, ago, z.Top, -300, z.Bot, c, c, ZoneOpacity);
            }
            else
            {
                Draw.HorizontalLine(this, z.Tag, z.Base, c, DashStyleHelper.Solid, 2);
            }

            if (ShowLabels)
            {
                string lbl = string.Format("{0}  S:{1}  Sw:{2}",
                    z.IsSupport ? "SUP" : "RES", z.Strength, z.Sweeps);
                double yLbl = z.IsSupport ? z.Bot - TickSize * 4 : z.Top + TickSize * 4;
                Draw.Text(this, z.LblTag, true, lbl, 0, yLbl, 0,
                    c, new SimpleFont("Arial", 8), TextAlignment.Left,
                    Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        // ── Visibility control: hide extras, reveal if now in range ───────────
        private void EnforceVisibility()
        {
            // Sort active non-mitigated zones relative to current price
            int above = 0, below = 0;
            // Iterate from newest to oldest (list appends at end → reverse)
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SRZone z = _levels[i];
                if (z.Mitigated) continue;

                bool isAbove = z.Bot > Close[0];
                bool isBelow = z.Top < Close[0];
                bool shouldHide = false;

                if (isAbove)  { above++; if (above  > VisibleAbove) shouldHide = true; }
                if (isBelow)  { below++; if (below  > VisibleBelow) shouldHide = true; }

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

        // ── Properties ────────────────────────────────────────────────────────
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
        [Range(1, 60)]
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
