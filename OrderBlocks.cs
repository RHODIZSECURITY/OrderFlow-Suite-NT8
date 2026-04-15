// Converted from SMC / ICT Suite Pro Pine Script — ORDER BLOCKS section
// Sensitivity 1-5 maps to pivot lengths: 3 / 5 / 8 / 13 / 21
// OB = last candle before a structural pivot impulse (highest high for bearish, lowest low for bullish)
// Box Range modes: Full Candle / Body Only / Open to Wick / 50% / 75% Threshold
// Overlap: None / Merge / Hide Oldest / Hide Youngest

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

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
    public enum ObRangeMode   { FullCandle, BodyOnly, OpenToWick, Threshold50, Threshold75 }
    public enum ObOverlapMode { None, Merge, HideOldest, HideYoungest }

    public class OrderBlocks : Indicator
    {
        private struct OBZone
        {
            public double Top, Bot;
            public int    Bias;       //  1 = bullish OB (demand),  -1 = bearish OB (supply)
            public bool   Mitigated;
            public string Tag, LblTag;
        }

        private readonly List<OBZone> _zones     = new List<OBZone>();
        private readonly List<double> _allHighs  = new List<double>();
        private readonly List<double> _allLows   = new List<double>();
        private readonly List<double> _allOpens  = new List<double>();
        private readonly List<double> _allCloses = new List<double>();
        private const int ArrayCap = 500;

        // sensitivity → pivot length
        private int PivLen => Sensitivity == 1 ? 3
                            : Sensitivity == 2 ? 5
                            : Sensitivity == 3 ? 8
                            : Sensitivity == 4 ? 13 : 21;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name           = "Order Blocks";
                Description    = "SMC Order Blocks — pivot-based demand/supply zones. Ported from SMC/ICT Suite Pro.";
                Calculate      = Calculate.OnBarClose;
                IsOverlay      = true;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                Sensitivity      = 2;           // pivot len = 5
                ShowInternalOBs  = true;
                MaxVisible       = 3;
                RangeMode        = ObRangeMode.FullCandle;
                OverlapMode      = ObOverlapMode.HideOldest;
                ShowLabels       = true;
                ShowMitigated    = false;

                BullColor = new SolidColorBrush(Color.FromArgb(30,  43, 255,   0));  // #2bff00 88% transp
                BearColor = new SolidColorBrush(Color.FromArgb(30, 255,   0,   0));  // #ff0000 88% transp
            }
            else if (State == State.DataLoaded)
            {
                _zones.Clear();
                _allHighs.Clear(); _allLows.Clear();
                _allOpens.Clear(); _allCloses.Clear();
            }
        }

        // ── Zone calculation helpers (Pine: obZoneFromMode) ───────────────────
        private void CalcZone(double cH, double cL, double cO, double cC, int bias,
                               out double zTop, out double zBot)
        {
            double bodyTop = Math.Max(cO, cC);
            double bodyBot = Math.Min(cO, cC);
            zTop = cH; zBot = cL;

            switch (RangeMode)
            {
                case ObRangeMode.BodyOnly:
                    zTop = bodyTop; zBot = bodyBot;
                    break;
                case ObRangeMode.OpenToWick:
                    if (bias == 1) { zTop = bodyTop; zBot = cL; }
                    else           { zTop = cH;       zBot = bodyBot; }
                    break;
                case ObRangeMode.Threshold50:
                    if (bias == 1) { double mid50 = (bodyTop + cL) / 2.0; zTop = bodyTop; zBot = mid50; }
                    else           { double mid50 = (cH + bodyBot) / 2.0; zTop = mid50;   zBot = bodyBot; }
                    break;
                case ObRangeMode.Threshold75:
                    if (bias == 1) { double t75 = cL + (bodyTop - cL) * 0.25; zTop = bodyTop; zBot = t75; }
                    else           { double t75 = bodyBot + (cH - bodyBot) * 0.75; zTop = t75; zBot = bodyBot; }
                    break;
            }

            // Ensure minimum visible width
            double minW = TickSize * 2;
            if (zTop - zBot < minW) { double mid = (zTop + zBot) / 2.0; zTop = mid + minW / 2; zBot = mid - minW / 2; }
        }

        // ── Find best candle in pre-pivot range (Pine: obFindZoneFromArrays) ──
        private bool FindOBCandle(int pivotConfirmedBar, int pivLen, int bias,
                                   out double zTop, out double zBot)
        {
            zTop = double.NaN; zBot = double.NaN;

            // The pivot was confirmed pivLen bars ago (we're at CurrentBar)
            // Pre-pivot range: from (CurrentBar - pivLen*2) to (CurrentBar - pivLen - 1)
            int endRelative   = pivLen;           // bars ago
            int startRelative = pivLen * 2;       // bars ago

            if (startRelative >= CurrentBar) return false;

            int bestRel = -1;
            double bestVal = bias == -1 ? double.MinValue : double.MaxValue;

            for (int rel = endRelative; rel <= startRelative; rel++)
            {
                if (rel >= CurrentBar) break;
                double val = bias == -1 ? High[rel] : Low[rel];
                if ((bias == -1 && val > bestVal) || (bias == 1 && val < bestVal))
                {
                    bestVal = val;
                    bestRel = rel;
                }
            }

            if (bestRel < 0) return false;

            CalcZone(High[bestRel], Low[bestRel], Open[bestRel], Close[bestRel], bias,
                     out zTop, out zBot);
            return true;
        }

        // ── Overlap handling ──────────────────────────────────────────────────
        private bool HandleOverlap(double top, double bot, int bias)
        {
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var z = _zones[i];
                if (z.Mitigated || z.Bias != bias) continue;
                bool overlaps = Math.Max(bot, z.Bot) < Math.Min(top, z.Top);
                if (!overlaps) continue;

                switch (OverlapMode)
                {
                    case ObOverlapMode.HideOldest:
                        RemoveDrawObject(z.Tag); RemoveDrawObject(z.LblTag);
                        _zones.RemoveAt(i);
                        return true;
                    case ObOverlapMode.HideYoungest:
                        return false;
                    case ObOverlapMode.Merge:
                        var merged = z;
                        merged.Top = Math.Max(z.Top, top);
                        merged.Bot = Math.Min(z.Bot, bot);
                        _zones[i] = merged;
                        DrawZone(merged);
                        return false;
                }
            }
            return true;
        }

        private void DrawZone(OBZone z)
        {
            Brush c = z.Bias == 1 ? BullColor : BearColor;
            Draw.Rectangle(this, z.Tag, false, 0, z.Top, -30, z.Bot, c, c, 0);
            if (ShowLabels)
                Draw.Text(this, z.LblTag, true, z.Bias == 1 ? "OB+" : "OB-",
                    -15, (z.Top + z.Bot) / 2.0, 0, Brushes.White,
                    new SimpleFont("Arial", 7), TextAlignment.Center,
                    Brushes.Transparent, Brushes.Transparent, 0);
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < PivLen * 2 + 2) return;

            // ── Detect pivot high (bearish OB — supply zone) ──────────────────
            bool isPH = true;
            double pivH = High[PivLen];
            for (int i = 0; i < PivLen * 2 + 1; i++)
            {
                if (i == PivLen) continue;
                if (High[i] >= pivH) { isPH = false; break; }
            }

            if (isPH)
            {
                double zTop, zBot;
                if (FindOBCandle(CurrentBar - PivLen, PivLen, -1, out zTop, out zBot))
                    AddZone(zTop, zBot, -1);
            }

            // ── Detect pivot low (bullish OB — demand zone) ───────────────────
            bool isPL = true;
            double pivL = Low[PivLen];
            for (int i = 0; i < PivLen * 2 + 1; i++)
            {
                if (i == PivLen) continue;
                if (Low[i] <= pivL) { isPL = false; break; }
            }

            if (isPL)
            {
                double zTop, zBot;
                if (FindOBCandle(CurrentBar - PivLen, PivLen, 1, out zTop, out zBot))
                    AddZone(zTop, zBot, 1);
            }

            // ── Internal OBs (pivot len = 3) ──────────────────────────────────
            if (ShowInternalOBs && PivLen > 3)
            {
                const int intLen = 3;
                if (CurrentBar >= intLen * 2 + 2)
                {
                    bool isIntPH = true;
                    double intPivH = High[intLen];
                    for (int i = 0; i < intLen * 2 + 1; i++)
                    {
                        if (i == intLen) continue;
                        if (High[i] >= intPivH) { isIntPH = false; break; }
                    }
                    if (isIntPH)
                    {
                        double zTop, zBot;
                        if (FindOBCandle(CurrentBar - intLen, intLen, -1, out zTop, out zBot))
                            AddZone(zTop, zBot, -1);
                    }

                    bool isIntPL = true;
                    double intPivL = Low[intLen];
                    for (int i = 0; i < intLen * 2 + 1; i++)
                    {
                        if (i == intLen) continue;
                        if (Low[i] <= intPivL) { isIntPL = false; break; }
                    }
                    if (isIntPL)
                    {
                        double zTop, zBot;
                        if (FindOBCandle(CurrentBar - intLen, intLen, 1, out zTop, out zBot))
                            AddZone(zTop, zBot, 1);
                    }
                }
            }

            // ── Mitigation check ──────────────────────────────────────────────
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var z = _zones[i];
                if (z.Mitigated) continue;
                // Bullish OB mitigated when price closes below bot (demand failed)
                // Bearish OB mitigated when price closes above top (supply failed)
                bool mit = z.Bias == 1 ? Close[0] < z.Bot : Close[0] > z.Top;
                if (mit)
                {
                    var upd = z; upd.Mitigated = true; _zones[i] = upd;
                    if (!ShowMitigated) { RemoveDrawObject(z.Tag); RemoveDrawObject(z.LblTag); }
                }
            }
        }

        private void AddZone(double top, double bot, int bias)
        {
            // Count active same-bias zones; trim oldest if over max
            int active = 0;
            int oldestIdx = -1;
            for (int i = 0; i < _zones.Count; i++)
            {
                if (!_zones[i].Mitigated && _zones[i].Bias == bias)
                { active++; oldestIdx = i; }
            }
            if (active >= MaxVisible && oldestIdx >= 0 && OverlapMode == ObOverlapMode.HideOldest)
            {
                RemoveDrawObject(_zones[oldestIdx].Tag);
                RemoveDrawObject(_zones[oldestIdx].LblTag);
                _zones.RemoveAt(oldestIdx);
            }

            if (!HandleOverlap(top, bot, bias)) return;

            string tag    = (bias == 1 ? "OBB_" : "OBS_") + CurrentBar;
            string lblTag = tag + "_lbl";
            var zone = new OBZone { Top = top, Bot = bot, Bias = bias, Tag = tag, LblTag = lblTag };
            _zones.Add(zone);
            DrawZone(zone);
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, 5)]
        [Display(Name = "Swing Sensitivity (1=fast … 5=slow)", Order = 1, GroupName = "Order Blocks")]
        public int Sensitivity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Internal OBs", Order = 2, GroupName = "Order Blocks")]
        public bool ShowInternalOBs { get; set; }

        [NinjaScriptProperty]
        [Range(1, 30)]
        [Display(Name = "Max Visible OBs per side", Order = 3, GroupName = "Order Blocks")]
        public int MaxVisible { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Box Range Mode", Order = 4, GroupName = "Order Blocks")]
        public ObRangeMode RangeMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Overlap Mode", Order = 5, GroupName = "Order Blocks")]
        public ObOverlapMode OverlapMode { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 6, GroupName = "Order Blocks")]
        public bool ShowLabels { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Mitigated", Order = 7, GroupName = "Order Blocks")]
        public bool ShowMitigated { get; set; }

        [XmlIgnore]
        [Display(Name = "Bullish OB Color", Order = 8, GroupName = "Order Blocks")]
        public Brush BullColor { get; set; }

        [Browsable(false)]
        public string BullColorSerializable
        {
            get { return Serialize.BrushToString(BullColor); }
            set { BullColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Bearish OB Color", Order = 9, GroupName = "Order Blocks")]
        public Brush BearColor { get; set; }

        [Browsable(false)]
        public string BearColorSerializable
        {
            get { return Serialize.BrushToString(BearColor); }
            set { BearColor = Serialize.StringToBrush(value); }
        }

        #endregion
    }
}
