// Support & Resistance Zone Detection
// Logic ported from Pine Script reference (LuxAlgo DSR parity)
// Donchian: direction flip → level; pivot: ta.pivothigh/ta.pivotlow asymmetric comparison
// Zones anchored at actual pivot bar, extend to current bar (extend.right equivalent)

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
            public int    StartBar;
            public string Tag, LblTag;
            public bool   InZone;
        }

        private readonly List<SRZone> _levels = new List<SRZone>();
        private int    _donchOs;
        private double _donchVal    = double.NaN;
        private int    _donchValBar = -1;   // bar index of the tracked extreme
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

                DetectionMethod  = SrDetectionMethod.Donchian;
                SwingSensitivity = 10;
                AtrPeriod        = 200;
                DisplayType      = SrDisplayType.Zones;
                ZoneDepthAtr     = 0.5;
                BreakoutBuffer   = 0.0;
                ZoneOpacity      = 65;
                OverlapHandling  = SrOverlapMode.HideOldest;
                MaxHistory       = 8;
                VisibleAbove     = 2;
                VisibleBelow     = 2;
                ShowLabels       = true;
                ShowBreakLines   = true;
                // LuxAlgo palette: #089981 teal for support, #F23645 coral for resistance
                // Frozen = thread-safe for OnBarUpdate (data thread) accessing WPF brush
                var sc = new SolidColorBrush(Color.FromArgb(255,   8, 153, 129)); sc.Freeze();
                var rc = new SolidColorBrush(Color.FromArgb(255, 242,  54,  69)); rc.Freeze();
                SupportColor = sc;
                ResistColor  = rc;
            }
            else if (State == State.DataLoaded)
            {
                _levels.Clear();
                _donchOs = 0; _donchVal = double.NaN; _donchValBar = -1;
                _bullCount = 0; _bearCount = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            try
            {
                int len     = Math.Max(2, (int)SwingSensitivity);
                int minBars = Math.Max(len * 2 + 4, AtrPeriod + 1);
                if (CurrentBar < minBars) return;

                double atr = ATR(AtrPeriod)[0];
                if (atr <= 0 || double.IsNaN(atr)) return;

                double zDepth   = atr * ZoneDepthAtr;
                double breakBuf = atr * BreakoutBuffer;

                // ── Detect new levels ────────────────────────────────────────────
                double detPH = double.NaN, detPL = double.NaN;
                int pivotOffset = 0;

                int donchExtreme = -1;   // actual bar of the Donchian extreme
                switch (DetectionMethod)
                {
                    case SrDetectionMethod.Pivots:
                        detPH = DetectPivotHigh(len);
                        detPL = DetectPivotLow(len);
                        pivotOffset = len;
                        break;

                    case SrDetectionMethod.Donchian:
                        DonchianDetect(len, out detPH, out detPL, out donchExtreme);
                        break;

                    case SrDetectionMethod.CSID:
                        _bullCount = Close[0] > Open[0] ? _bullCount + 1 : 0;
                        _bearCount = Close[0] < Open[0] ? _bearCount + 1 : 0;
                        if (_bullCount == len) detPH = MAX(High, len)[0];
                        if (_bearCount == len) detPL = MIN(Low,  len)[0];
                        break;
                }

                // For Donchian, startBar = actual extreme bar (not detection bar)
                int phStart = DetectionMethod == SrDetectionMethod.Donchian && donchExtreme >= 0
                    ? donchExtreme : CurrentBar - pivotOffset;
                int plStart = phStart;

                if (!double.IsNaN(detPH))
                    AddZone(detPH + breakBuf, detPH - zDepth, detPH, false, phStart);
                if (!double.IsNaN(detPL))
                    AddZone(detPL + zDepth, detPL - breakBuf, detPL, true, plStart);

                // ── Update existing zones ────────────────────────────────────────
                for (int i = _levels.Count - 1; i >= 0; i--)
                {
                    SRZone z = _levels[i];
                    if (z.Mitigated) continue;

                    // Pine: close < btm (support broken) / close > top (resistance broken)
                    bool broken = z.IsSupport ? Close[0] < z.Bot : Close[0] > z.Top;
                    if (broken)
                    {
                        z.Mitigated = true; _levels[i] = z;
                        RemoveDrawObject(z.Tag);
                        RemoveDrawObject(z.LblTag);
                        if (ShowBreakLines)
                        {
                            Brush bc = z.IsSupport ? (SupportColor ?? Brushes.Teal) : (ResistColor ?? Brushes.Red);
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

        // ── Pivot detection — asymmetric (matches ta.pivothigh / ta.pivotlow) ────
        // Right side (newer, i < len): strict — ties disqualify
        // Left side (older, i > len): lenient — ties allowed
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

        // ── Donchian — matches Pine Script reference logic ────────────────────────
        // Tracks individual bar High[0]/Low[0]; returns bar index of actual extreme
        private void DonchianDetect(int len, out double detPH, out double detPL, out int extremeBar)
        {
            detPH = double.NaN; detPL = double.NaN; extremeBar = -1;
            if (CurrentBar <= len) return;

            double donchH  = MAX(High, len)[0];
            double donchL  = MIN(Low,  len)[0];
            double pDonchH = MAX(High, len)[1];
            double pDonchL = MIN(Low,  len)[1];

            int newDir = donchH > pDonchH ? 1 : donchL < pDonchL ? -1 : _donchOs;

            if (newDir != _donchOs && newDir != 0)
            {
                // Direction flip → previous tracked extreme becomes a level
                if (newDir ==  1 && !double.IsNaN(_donchVal)) { detPL = _donchVal; extremeBar = _donchValBar; }
                if (newDir == -1 && !double.IsNaN(_donchVal)) { detPH = _donchVal; extremeBar = _donchValBar; }
                _donchVal    = newDir == 1 ? High[0] : Low[0];
                _donchValBar = CurrentBar;
                _donchOs     = newDir;
            }
            else
            {
                // Same direction → track new extreme using individual bar price
                if (_donchOs ==  1 && High[0] >= _donchVal) { _donchVal = High[0]; _donchValBar = CurrentBar; }
                if (_donchOs == -1 && Low[0]  <= _donchVal) { _donchVal = Low[0];  _donchValBar = CurrentBar; }
            }
        }

        // ── Add zone — startBar is the actual pivot/extreme bar index ─────────────
        private void AddZone(double top, double bot, double basePrice, bool isSupport, int startBar)
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

            string tag = (isSupport ? "SR_S_" : "SR_R_") + CurrentBar;
            var z = new SRZone
            {
                Top = top, Bot = bot, Base = basePrice,
                IsSupport = isSupport, Mitigated = false, Hidden = false,
                Strength = 1, Sweeps = 0,
                StartBar = Math.Max(0, startBar),
                Tag = tag, LblTag = tag + "_lbl"
            };
            _levels.Add(z);
            RedrawZone(z);
        }

        // ── Draw — fill brush used for both outline and area (avoids NT8 color fallback)
        private void RedrawZone(SRZone z)
        {
            if (z.Hidden || z.Mitigated) return;
            Brush fill = z.IsSupport ? (SupportColor ?? Brushes.Teal) : (ResistColor ?? Brushes.Red);
            int   ago  = Math.Max(0, Math.Min(CurrentBar - z.StartBar, 4000));

            if (DisplayType == SrDisplayType.Zones)
            {
                Draw.Rectangle(this, z.Tag, false, ago, z.Top, -500, z.Bot, Brushes.Transparent, fill, ZoneOpacity);
            }
            else
            {
                Draw.HorizontalLine(this, z.Tag, z.Base, fill, DashStyleHelper.Solid, 2);
            }

            if (ShowLabels)
            {
                string lbl  = z.IsSupport ? "SUPPORT" : "RESISTANCE";
                double yLbl = z.Top;
                Draw.Text(this, z.LblTag, true, lbl, ago, yLbl, 0,
                    Brushes.White, new SimpleFont("Arial", 9), System.Windows.TextAlignment.Left,
                    Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        // ── Visibility — Pine Script: isAbove = zoneMid >= close ─────────────────
        private void EnforceVisibility()
        {
            int above = 0, below = 0;
            for (int i = _levels.Count - 1; i >= 0; i--)
            {
                SRZone z = _levels[i];
                if (z.Mitigated) continue;

                double mid     = (z.Top + z.Bot) * 0.5;
                bool   isAbove = mid >= Close[0];
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
        [Display(Name = "Swing Sensitivity", Order = 2, GroupName = "Support & Resistance")]
        public double SwingSensitivity { get; set; }

        [NinjaScriptProperty]
        [Range(5, 500)]
        [Display(Name = "ATR Period", Order = 3, GroupName = "Support & Resistance")]
        public int AtrPeriod { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Display Style", Order = 4, GroupName = "Support & Resistance")]
        public SrDisplayType DisplayType { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "Zone Depth (× ATR)", Order = 5, GroupName = "Support & Resistance")]
        public double ZoneDepthAtr { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 2.0)]
        [Display(Name = "Breakout Buffer (× ATR)", Order = 6, GroupName = "Support & Resistance")]
        public double BreakoutBuffer { get; set; }

        [NinjaScriptProperty]
        [Range(1, 100)]
        [Display(Name = "Zone Fill Opacity %", Order = 7, GroupName = "Support & Resistance")]
        public int ZoneOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Overlap Handling", Order = 8, GroupName = "Support & Resistance")]
        public SrOverlapMode OverlapHandling { get; set; }

        [NinjaScriptProperty]
        [Range(1, 30)]
        [Display(Name = "Max S/R History", Order = 9, GroupName = "Support & Resistance")]
        public int MaxHistory { get; set; }

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Display(Name = "Visible Zones Above", Order = 10, GroupName = "Support & Resistance")]
        public int VisibleAbove { get; set; }

        [NinjaScriptProperty]
        [Range(0, 20)]
        [Display(Name = "Visible Zones Below", Order = 11, GroupName = "Support & Resistance")]
        public int VisibleBelow { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 12, GroupName = "Support & Resistance")]
        public bool ShowLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Break Lines", Order = 13, GroupName = "Support & Resistance")]
        public bool ShowBreakLines { get; set; }

        [XmlIgnore]
        [Display(Name = "Resistance Color", Order = 14, GroupName = "Support & Resistance")]
        public Brush ResistColor { get; set; }
        [Browsable(false)]
        public string ResistColorSerializable
        {
            get => Serialize.BrushToString(ResistColor);
            set => ResistColor = Serialize.StringToBrush(value);
        }

        [XmlIgnore]
        [Display(Name = "Support Color", Order = 15, GroupName = "Support & Resistance")]
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
