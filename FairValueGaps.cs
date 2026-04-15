// Converted from SMC / ICT Suite Pro Pine Script — FVG DELTA section
// Pine: bullFVG = currLow > last2High AND lastClose > last2High
//       bearFVG = currHigh < last2Low AND lastClose < last2Low
// Quality filters: Displacement %, ATR size, Volume, Delta bias

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

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public enum FvgQuality { StructureOnly, Balanced, HighConfluence }

    public class FairValueGaps : Indicator
    {
        private struct FvgZone
        {
            public string Tag, LblTag;
            public double Top, Bot;
            public int    Bias;       // 1 = bull, -1 = bear
            public bool   Mitigated;
        }

        private readonly List<FvgZone> _zones = new List<FvgZone>();
        private double _avgVol;

        // delta proxy accumulators (reset each bar)
        private double _barBullVol, _barBearVol;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name           = "Fair Value Gaps";
                Description    = "FVG Delta — gap detection with displacement, ATR size, volume and delta quality filters. Ported from SMC/ICT Suite Pro.";
                Calculate      = Calculate.OnBarClose;
                IsOverlay      = true;
                DrawOnPricePanel = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                IsSuspendedWhileInactive = true;
                ForwardBars        = 20;
                Quality            = FvgQuality.HighConfluence;
                MaxZones           = 3;
                DisplacementPct    = 80.0;
                AtrMult            = 1.5;
                VolMult            = 2.0;
                ShowMitigated      = false;
                ShowLabels         = true;
                UseVolumeFilter    = true;
                BullColor          = new SolidColorBrush(Color.FromArgb(76,  0, 255, 104));  // #00ff68 70% transp
                BearColor          = new SolidColorBrush(Color.FromArgb(76, 255,  0,   8));  // #ff0008 70% transp
                MitigatedColor     = new SolidColorBrush(Color.FromArgb(30, 150, 150, 150));
            }
            else if (State == State.DataLoaded)
            {
                _zones.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 3) return;

            _avgVol = SMA(Volume, 20)[0];
            double atrVal  = ATR(14)[0];

            // ── FVG detection (Pine: currLow > last2High AND lastClose > last2High) ──
            // NT8 bar index: [0]=current, [1]=prev, [2]=2 bars ago
            bool bullFvgBase = Low[0]  > High[2] && Close[1] > High[2];
            bool bearFvgBase = High[0] < Low[2]  && Close[1] < Low[2];

            // ── Mitigation check on existing zones ───────────────────────────
            for (int i = _zones.Count - 1; i >= 0; i--)
            {
                var z = _zones[i];
                if (z.Mitigated) continue;

                // Bull FVG mitigated when price enters from top: low <= zone top
                // Bear FVG mitigated when price enters from bottom: high >= zone bot
                bool mit = z.Bias == 1 ? Low[0] <= z.Top : High[0] >= z.Bot;
                if (mit)
                {
                    var upd = z; upd.Mitigated = true; _zones[i] = upd;
                    if (ShowMitigated)
                        Draw.Rectangle(this, z.Tag, false, 0, z.Top, -ForwardBars, z.Bot, MitigatedColor, MitigatedColor, 15);
                    else
                    {
                        RemoveDrawObject(z.Tag);
                        RemoveDrawObject(z.LblTag);
                    }
                }
            }

            // ── New FVG validation ────────────────────────────────────────────
            if (!bullFvgBase && !bearFvgBase) return;

            bool   isBull    = bullFvgBase;
            double gapTop    = isBull ? Low[0]   : Low[2];
            double gapBot    = isBull ? High[2]  : High[0];
            double gapHeight = Math.Abs(gapTop - gapBot);

            // Middle candle (candle[1]) body ratio — displacement filter
            double candleBody  = Math.Abs(Close[1] - Open[1]);
            double candleRange = Math.Max(High[1] - Low[1], TickSize);
            double bodyRatioPct = candleBody / candleRange * 100.0;

            bool displacementOK = Quality == FvgQuality.StructureOnly
                || bodyRatioPct >= DisplacementPct;

            bool sizeOK = Quality == FvgQuality.StructureOnly
                || gapHeight >= atrVal * AtrMult;

            bool volumeOK = !UseVolumeFilter
                || Quality != FvgQuality.HighConfluence
                || Volume[1] > _avgVol * VolMult;

            // Delta bias: body bias proxy (bull vol = close>open, bear vol = close<open)
            double bodyBias   = Close[1] > Open[1] ? 1.0 : Close[1] < Open[1] ? 0.0 : 0.5;
            double bullPct    = bodyBias * 100.0;
            double bearPct    = (1.0 - bodyBias) * 100.0;
            bool   deltaOK    = true;
            string deltaLabel = string.Format("Δ B{0:F0}% S{1:F0}%", bullPct, bearPct);

            if (Quality == FvgQuality.HighConfluence)
            {
                // Discard if delta opposes direction
                if (isBull  && bullPct < 50.0) deltaOK = false;
                if (!isBull && bearPct < 50.0) deltaOK = false;
            }

            if (!displacementOK || !sizeOK || !volumeOK || !deltaOK) return;

            // ── Remove oldest active zone if at max ───────────────────────────
            int activeCount = 0;
            int oldestIdx   = -1;
            for (int i = 0; i < _zones.Count; i++)
            {
                if (!_zones[i].Mitigated) { activeCount++; oldestIdx = i; }
            }
            if (activeCount >= MaxZones && oldestIdx >= 0)
            {
                RemoveDrawObject(_zones[oldestIdx].Tag);
                RemoveDrawObject(_zones[oldestIdx].LblTag);
                _zones.RemoveAt(oldestIdx);
            }

            // ── Draw new FVG ──────────────────────────────────────────────────
            string tag    = (isBull ? "FVG_B_" : "FVG_R_") + CurrentBar;
            string lblTag = tag + "_lbl";
            Brush  color  = isBull ? BullColor : BearColor;

            Draw.Rectangle(this, tag, false, 0, gapTop, -ForwardBars, gapBot, color, color, 0);

            if (ShowLabels)
            {
                string txt = Quality == FvgQuality.HighConfluence ? deltaLabel : "FVG";
                Draw.Text(this, lblTag, true, txt,
                    -ForwardBars / 2, (gapTop + gapBot) / 2.0, 0,
                    Brushes.White, new SimpleFont("Arial", 7), TextAlignment.Center,
                    Brushes.Transparent, Brushes.Transparent, 0);
            }

            _zones.Add(new FvgZone
            {
                Tag = tag, LblTag = lblTag,
                Top = gapTop, Bot = gapBot,
                Bias = isBull ? 1 : -1,
                Mitigated = false
            });
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "Forward Bars", Order = 0, GroupName = "FVG Delta")]
        public int ForwardBars { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "FVG Quality", Order = 1, GroupName = "FVG Delta")]
        public FvgQuality Quality { get; set; }

        [NinjaScriptProperty]
        [Range(1, 10)]
        [Display(Name = "Max FVGs Shown", Order = 2, GroupName = "FVG Delta")]
        public int MaxZones { get; set; }

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "Displacement % (body/range)", Order = 3, GroupName = "FVG Delta")]
        public double DisplacementPct { get; set; }

        [NinjaScriptProperty]
        [Range(0.1, 5.0)]
        [Display(Name = "ATR Multiplier (min gap size)", Order = 4, GroupName = "FVG Delta")]
        public double AtrMult { get; set; }

        [NinjaScriptProperty]
        [Range(0.5, 10.0)]
        [Display(Name = "Volume Multiplier", Order = 5, GroupName = "FVG Delta")]
        public double VolMult { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Use Volume Filter", Order = 6, GroupName = "FVG Delta")]
        public bool UseVolumeFilter { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Mitigated", Order = 7, GroupName = "FVG Delta")]
        public bool ShowMitigated { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 8, GroupName = "FVG Delta")]
        public bool ShowLabels { get; set; }

        [XmlIgnore]
        [Display(Name = "Bullish FVG Color", Order = 9, GroupName = "FVG Delta")]
        public Brush BullColor { get; set; }

        [Browsable(false)]
        public string BullColorSerializable
        {
            get { return Serialize.BrushToString(BullColor); }
            set { BullColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Bearish FVG Color", Order = 10, GroupName = "FVG Delta")]
        public Brush BearColor { get; set; }

        [Browsable(false)]
        public string BearColorSerializable
        {
            get { return Serialize.BrushToString(BearColor); }
            set { BearColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Mitigated Color", Order = 11, GroupName = "FVG Delta")]
        public Brush MitigatedColor { get; set; }

        [Browsable(false)]
        public string MitigatedColorSerializable
        {
            get { return Serialize.BrushToString(MitigatedColor); }
            set { MitigatedColor = Serialize.StringToBrush(value); }
        }

        #endregion
    }
}
