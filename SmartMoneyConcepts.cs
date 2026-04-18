#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum FvgQuality    { Any, Displaced, Strong }
    public enum ObRangeMode   { FullCandle, BodyOnly, Threshold75, Threshold50 }
    public enum ObOverlapMode { None, Merge, HideOldest, HideYoungest }

    [Gui.CategoryOrder("FVG Delta",      1)]
    [Gui.CategoryOrder("Order Blocks",   2)]
    [Gui.CategoryOrder("Breaker Blocks", 3)]
    public class SmartMoneyConcepts : Indicator
    {
        private struct Zone
        {
            public int    StartBar;
            public double Top, Bottom;
            public bool   Bull, Mitigated, Visible;
            public string DrawTag, LabelTag;
        }

        private readonly List<Zone> _fvgZones      = new List<Zone>();
        private readonly List<Zone> _obZones        = new List<Zone>();
        private readonly List<Zone> _breakerBlocks  = new List<Zone>();
        private double _lastFvgTop    = double.NaN;
        private double _lastFvgBottom = double.NaN;
        private double _lastObTop     = double.NaN;
        private double _lastObBottom  = double.NaN;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name        = "SmartMoneyConcepts";
                Description = "Fair Value Gaps (FVG) + Order Blocks with proximity-based visibility.";
                Calculate   = Calculate.OnBarClose;
                IsOverlay   = true;
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;

                FvgEnabled    = true;
                MinFvgTicks   = 2;
                QualityFilter = FvgQuality.Displaced;
                FvgExtendBars = 120;
                FvgOpacity    = 20;
                FvgBullColor  = Brushes.LimeGreen;
                FvgBearColor  = Brushes.IndianRed;

                ObEnabled       = true;
                PivotStrength   = 2;
                RangeMode       = ObRangeMode.Threshold75;
                OverlapMode     = ObOverlapMode.HideOldest;
                ObVisibleAbove  = 2;
                ObVisibleBelow  = 2;
                ObExtendBars    = 180;
                ObOpacity       = 15;
                ShowObLabels    = true;
                ObBullColor     = Brushes.DodgerBlue;
                ObBearColor     = Brushes.DarkOrange;

                ShowBreakerBlocks = true;
                BreakerOpacity    = 18;
                BreakerBullColor  = Brushes.Teal;
                BreakerBearColor  = Brushes.Sienna;
            }
            else if (State == State.DataLoaded)
            {
                _fvgZones.Clear();
                _obZones.Clear();
                _breakerBlocks.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 10) return;

            if (FvgEnabled)
            {
                ProcessFvg();
                CheckFvgMitigation();
            }

            if (ObEnabled)
            {
                ProcessOrderBlocks();
                CheckObInvalidation();
                EnforceObVisibility();
            }
        }

        // ── FVG ──────────────────────────────────────────────────────────────────

        private void ProcessFvg()
        {
            bool bullGap = Low[0] > High[2];
            bool bearGap = High[0] < Low[2];
            if (!bullGap && !bearGap) return;

            double top      = bullGap ? Low[0] : Low[2];
            double bot      = bullGap ? High[2] : High[0];
            double gapTicks = (top - bot) / TickSize;
            if (gapTicks < MinFvgTicks) return;

            double body = Math.Abs(Close[0] - Open[0]);
            double atr  = ATR(14)[0];
            if (QualityFilter == FvgQuality.Displaced && !(atr > 0 && body >= atr * 0.4)) return;
            if (QualityFilter == FvgQuality.Strong    && !(atr > 0 && body >= atr * 0.7)) return;

            string fvgTag = $"SMC_FVG_{(bullGap ? "B" : "S")}_{CurrentBar}";
            Zone zone = new Zone { StartBar = CurrentBar, Top = top, Bottom = bot, Bull = bullGap, DrawTag = fvgTag };

            if (OverlapMode == ObOverlapMode.Merge)
                MergeZone(_fvgZones, zone);
            else
                AddZoneCapped(_fvgZones, zone);

            _lastFvgTop    = zone.Top;
            _lastFvgBottom = zone.Bottom;

            Draw.Rectangle(this, fvgTag, false, 0, top, -FvgExtendBars, bot,
                Brushes.Transparent, zone.Bull ? FvgBullColor : FvgBearColor, FvgOpacity);
        }

        private void CheckFvgMitigation()
        {
            for (int i = 0; i < _fvgZones.Count; i++)
            {
                Zone z = _fvgZones[i];
                if (z.Mitigated || string.IsNullOrEmpty(z.DrawTag)) continue;
                if (!(Close[0] > z.Bottom && Close[0] < z.Top)) continue;

                z.Mitigated  = true;
                _fvgZones[i] = z;

                int barsBack = CurrentBar - z.StartBar;
                Draw.Rectangle(this, z.DrawTag, false, barsBack, z.Top, 0, z.Bottom,
                    Brushes.Transparent, z.Bull ? FvgBullColor : FvgBearColor,
                    Math.Max(1, FvgOpacity / 4));
            }
        }

        // ── Order Blocks ─────────────────────────────────────────────────────────

        private void ProcessOrderBlocks()
        {
            if (CurrentBar < PivotStrength + 2) return;

            bool bullImpulse = Close[0] > Open[0] && Close[0] > High[1] && Open[1] > Close[1];
            bool bearImpulse = Close[0] < Open[0] && Close[0] < Low[1]  && Open[1] < Close[1];
            if (!bullImpulse && !bearImpulse) return;

            const int src = 1;
            double bodyHi = Math.Max(Open[src], Close[src]);
            double bodyLo = Math.Min(Open[src], Close[src]);
            double fullR  = High[src] - Low[src];

            double hi, lo;
            switch (RangeMode)
            {
                case ObRangeMode.BodyOnly:
                    hi = bodyHi; lo = bodyLo; break;
                case ObRangeMode.Threshold75:
                    hi = bodyHi;
                    lo = High[src] - 0.75 * fullR;
                    break;
                case ObRangeMode.Threshold50:
                    hi = bodyHi;
                    lo = (High[src] + Low[src]) * 0.5;
                    break;
                default: // FullCandle
                    hi = High[src]; lo = Low[src]; break;
            }
            if (hi <= lo) return;

            string obTag  = $"SMC_OB_{(bullImpulse ? "B" : "S")}_{CurrentBar}";
            string lblTag = $"SMC_OB_LBL_{CurrentBar}";
            // StartBar = source candle (1 bar ago), so barsBack = CurrentBar - StartBar is always correct
            Zone zone = new Zone
            {
                StartBar = CurrentBar - 1,
                Top = hi, Bottom = lo,
                Bull = bullImpulse,
                DrawTag = obTag, LabelTag = lblTag
            };

            switch (OverlapMode)
            {
                case ObOverlapMode.Merge:
                    MergeZone(_obZones, zone); break;
                case ObOverlapMode.HideOldest:
                    AddHideOldest(_obZones, zone); break;
                case ObOverlapMode.HideYoungest:
                    if (!HasOverlap(_obZones, zone)) AddZoneCapped(_obZones, zone); break;
                default: // None
                    AddZoneCapped(_obZones, zone); break;
            }

            _lastObTop    = zone.Top;
            _lastObBottom = zone.Bottom;
        }

        // Remove OB when price closes inside or through the zone
        private void CheckObInvalidation()
        {
            for (int i = _obZones.Count - 1; i >= 0; i--)
            {
                Zone z      = _obZones[i];
                bool broken = z.Bull ? Close[0] < z.Top : Close[0] > z.Bottom;
                if (!broken) continue;

                if (z.Visible)
                {
                    RemoveDrawObject(z.DrawTag);
                    if (!string.IsNullOrEmpty(z.LabelTag)) RemoveDrawObject(z.LabelTag);
                }
                _obZones.RemoveAt(i);

                if (!ShowBreakerBlocks) continue;

                bool   bbBull    = !z.Bull;
                int    barsBack  = CurrentBar - z.StartBar;
                string bbTag     = $"SMC_BB_{(bbBull ? "B" : "S")}_{z.StartBar}";
                string bbLbl     = bbTag + "_lbl";

                if (_breakerBlocks.Count >= MaxZones)
                {
                    int oldest = 0;
                    for (int j = 1; j < _breakerBlocks.Count; j++)
                        if (_breakerBlocks[j].StartBar < _breakerBlocks[oldest].StartBar) oldest = j;
                    RemoveDrawObject(_breakerBlocks[oldest].DrawTag);
                    RemoveDrawObject(_breakerBlocks[oldest].LabelTag);
                    _breakerBlocks.RemoveAt(oldest);
                }

                _breakerBlocks.Add(new Zone
                {
                    StartBar = CurrentBar, Top = z.Top, Bottom = z.Bottom,
                    Bull = bbBull, DrawTag = bbTag, LabelTag = bbLbl
                });
                Draw.Rectangle(this, bbTag, false, barsBack, z.Top, -ObExtendBars, z.Bottom,
                    Brushes.Transparent, bbBull ? BreakerBullColor : BreakerBearColor, BreakerOpacity);
                Draw.Text(this, bbLbl, "BB", 0, (z.Top + z.Bottom) * 0.5, Brushes.White);
            }
        }

        // Show only the N closest OBs above price and N closest below price.
        // Zones containing current price are always visible.
        private void EnforceObVisibility()
        {
            double price = Close[0];

            // Collect indices by position relative to price
            var above = new List<int>();
            var below = new List<int>();
            var at    = new List<int>();

            for (int i = 0; i < _obZones.Count; i++)
            {
                Zone z = _obZones[i];
                if      (z.Bottom > price) above.Add(i);
                else if (z.Top    < price) below.Add(i);
                else                       at.Add(i);
            }

            // Sort above ascending by Bottom (smallest Bottom = closest to price)
            above.Sort((a, b) => _obZones[a].Bottom.CompareTo(_obZones[b].Bottom));
            // Sort below descending by Top (largest Top = closest to price)
            below.Sort((a, b) => _obZones[b].Top.CompareTo(_obZones[a].Top));

            var show = new HashSet<int>(at);
            for (int k = 0; k < Math.Min(ObVisibleAbove, above.Count); k++) show.Add(above[k]);
            for (int k = 0; k < Math.Min(ObVisibleBelow, below.Count); k++) show.Add(below[k]);

            for (int i = 0; i < _obZones.Count; i++)
            {
                Zone z         = _obZones[i];
                bool shouldShow = show.Contains(i);

                if (shouldShow && !z.Visible)
                {
                    Brush fill     = z.Bull ? ObBullColor : ObBearColor;
                    int   barsBack = Math.Max(1, CurrentBar - z.StartBar);
                    Draw.Rectangle(this, z.DrawTag, false, barsBack, z.Top, -ObExtendBars, z.Bottom,
                        Brushes.Transparent, fill, ObOpacity);
                    if (ShowObLabels)
                        Draw.Text(this, z.LabelTag, true, "OB", barsBack, z.Top, 0,
                            Brushes.White, new SimpleFont("Arial", 9), System.Windows.TextAlignment.Left,
                            Brushes.Transparent, Brushes.Transparent, 0);
                    z.Visible  = true;
                    _obZones[i] = z;
                }
                else if (!shouldShow && z.Visible)
                {
                    RemoveDrawObject(z.DrawTag);
                    if (!string.IsNullOrEmpty(z.LabelTag)) RemoveDrawObject(z.LabelTag);
                    z.Visible  = false;
                    _obZones[i] = z;
                }
            }
        }

        // ── Zone list helpers ─────────────────────────────────────────────────────

        private const int MaxZones = 50;

        private void AddZoneCapped(List<Zone> zones, Zone incoming)
        {
            if (zones.Count >= MaxZones) EvictOldest(zones);
            zones.Add(incoming);
        }

        private void AddHideOldest(List<Zone> zones, Zone incoming)
        {
            for (int i = zones.Count - 1; i >= 0; i--)
            {
                Zone z = zones[i];
                if (z.Bull != incoming.Bull) continue;
                if (!(incoming.Top >= z.Bottom && incoming.Bottom <= z.Top)) continue;
                // overlapping same-direction zone found — remove older one
                if (z.Visible)
                {
                    RemoveDrawObject(z.DrawTag);
                    if (!string.IsNullOrEmpty(z.LabelTag)) RemoveDrawObject(z.LabelTag);
                }
                zones.RemoveAt(i);
                break;
            }
            if (zones.Count >= MaxZones) EvictOldest(zones);
            zones.Add(incoming);
        }

        private bool HasOverlap(List<Zone> zones, Zone incoming)
        {
            foreach (var z in zones)
                if (z.Bull == incoming.Bull && incoming.Top >= z.Bottom && incoming.Bottom <= z.Top)
                    return true;
            return false;
        }

        private void EvictOldest(List<Zone> zones)
        {
            int oldest = 0;
            for (int i = 1; i < zones.Count; i++)
                if (zones[i].StartBar < zones[oldest].StartBar) oldest = i;
            if (zones[oldest].Visible)
            {
                RemoveDrawObject(zones[oldest].DrawTag);
                if (!string.IsNullOrEmpty(zones[oldest].LabelTag)) RemoveDrawObject(zones[oldest].LabelTag);
            }
            zones.RemoveAt(oldest);
        }

        private void MergeZone(List<Zone> zones, Zone incoming)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                Zone z = zones[i];
                if (z.Bull != incoming.Bull) continue;
                if (!(incoming.Top >= z.Bottom && incoming.Bottom <= z.Top)) continue;
                z.Top      = Math.Max(z.Top, incoming.Top);
                z.Bottom   = Math.Min(z.Bottom, incoming.Bottom);
                z.StartBar = incoming.StartBar;
                z.Visible  = false; // force redraw on next EnforceObVisibility pass
                zones[i]   = z;
                return;
            }
            if (zones.Count >= MaxZones) EvictOldest(zones);
            zones.Add(incoming);
        }

        // ── Properties ───────────────────────────────────────────────────────────

        [NinjaScriptProperty, Display(Name = "FVG Enabled",    GroupName = "FVG Delta", Order = 1)]
        public bool FvgEnabled { get; set; }

        [NinjaScriptProperty, Range(1, 100), Display(Name = "Min FVG Ticks", GroupName = "FVG Delta", Order = 2)]
        public int MinFvgTicks { get; set; }

        [NinjaScriptProperty, Display(Name = "Quality Filter", GroupName = "FVG Delta", Order = 3)]
        public FvgQuality QualityFilter { get; set; }

        [NinjaScriptProperty, Range(1, 1000), Display(Name = "FVG Extend Bars", GroupName = "FVG Delta", Order = 4)]
        public int FvgExtendBars { get; set; }

        [NinjaScriptProperty, Range(0, 100), Display(Name = "FVG Opacity", GroupName = "FVG Delta", Order = 5)]
        public int FvgOpacity { get; set; }

        [XmlIgnore, Display(Name = "FVG Bull Color", GroupName = "FVG Delta", Order = 6)]
        public Brush FvgBullColor { get; set; }
        [Browsable(false)]
        public string FvgBullColorSerializable { get => Serialize.BrushToString(FvgBullColor); set => FvgBullColor = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "FVG Bear Color", GroupName = "FVG Delta", Order = 7)]
        public Brush FvgBearColor { get; set; }
        [Browsable(false)]
        public string FvgBearColorSerializable { get => Serialize.BrushToString(FvgBearColor); set => FvgBearColor = Serialize.StringToBrush(value); }

        [NinjaScriptProperty, Display(Name = "OB Enabled",       GroupName = "Order Blocks", Order = 1)]
        public bool ObEnabled { get; set; }

        [NinjaScriptProperty, Range(1, 20), Display(Name = "Pivot Strength",   GroupName = "Order Blocks", Order = 2)]
        public int PivotStrength { get; set; }

        [NinjaScriptProperty, Display(Name = "Range Mode",       GroupName = "Order Blocks", Order = 3)]
        public ObRangeMode RangeMode { get; set; }

        [NinjaScriptProperty, Display(Name = "Overlap Mode",     GroupName = "Order Blocks", Order = 4)]
        public ObOverlapMode OverlapMode { get; set; }

        [NinjaScriptProperty, Range(0, 20), Display(Name = "Visible OB Above Price", GroupName = "Order Blocks", Order = 5)]
        public int ObVisibleAbove { get; set; }

        [NinjaScriptProperty, Range(0, 20), Display(Name = "Visible OB Below Price", GroupName = "Order Blocks", Order = 6)]
        public int ObVisibleBelow { get; set; }

        [NinjaScriptProperty, Range(1, 1000), Display(Name = "OB Extend Bars",  GroupName = "Order Blocks", Order = 7)]
        public int ObExtendBars { get; set; }

        [NinjaScriptProperty, Range(0, 100), Display(Name = "OB Opacity",       GroupName = "Order Blocks", Order = 8)]
        public int ObOpacity { get; set; }

        [NinjaScriptProperty, Display(Name = "Show OB Labels",   GroupName = "Order Blocks", Order = 9)]
        public bool ShowObLabels { get; set; }

        [XmlIgnore, Display(Name = "OB Bull Color", GroupName = "Order Blocks", Order = 10)]
        public Brush ObBullColor { get; set; }
        [Browsable(false)]
        public string ObBullColorSerializable { get => Serialize.BrushToString(ObBullColor); set => ObBullColor = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "OB Bear Color", GroupName = "Order Blocks", Order = 11)]
        public Brush ObBearColor { get; set; }
        [Browsable(false)]
        public string ObBearColorSerializable { get => Serialize.BrushToString(ObBearColor); set => ObBearColor = Serialize.StringToBrush(value); }

        [NinjaScriptProperty, Display(Name = "Show Breaker Blocks", GroupName = "Breaker Blocks", Order = 1)]
        public bool ShowBreakerBlocks { get; set; }

        [NinjaScriptProperty, Range(0, 100), Display(Name = "BB Opacity", GroupName = "Breaker Blocks", Order = 2)]
        public int BreakerOpacity { get; set; }

        [XmlIgnore, Display(Name = "BB Bull Color", GroupName = "Breaker Blocks", Order = 3)]
        public Brush BreakerBullColor { get; set; }
        [Browsable(false)]
        public string BreakerBullColorSerializable { get => Serialize.BrushToString(BreakerBullColor); set => BreakerBullColor = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "BB Bear Color", GroupName = "Breaker Blocks", Order = 4)]
        public Brush BreakerBearColor { get; set; }
        [Browsable(false)]
        public string BreakerBearColorSerializable { get => Serialize.BrushToString(BreakerBearColor); set => BreakerBearColor = Serialize.StringToBrush(value); }

        [Browsable(false)] public int    ActiveFvgZoneCount => _fvgZones.Count;
        [Browsable(false)] public int    ActiveObZoneCount  => _obZones.Count;
        [Browsable(false)] public double LastFvgTop         => _lastFvgTop;
        [Browsable(false)] public double LastFvgBottom      => _lastFvgBottom;
        [Browsable(false)] public double LastObTop          => _lastObTop;
        [Browsable(false)] public double LastObBottom       => _lastObBottom;
    }
}
