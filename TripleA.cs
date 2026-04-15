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
// Section: TRIPLE-A BASE (lines 664-752)
// Logic: 3-phase sequence — Absorption (A1) → Accumulation (A2) → Aggression (A3)
// Phase timeouts are ATR-adaptive (8–14 bars).

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class TripleA : Indicator
    {
        // ── Phase state ───────────────────────────────────────────────────────
        private int _phaseLong,  _phaseLongBars;
        private int _phaseShort, _phaseShortBars;

        // ── Lookback helpers ──────────────────────────────────────────────────
        private double _avgVol20;
        private double _avgRange50;

        // ── Settings ──────────────────────────────────────────────────────────
        private double _absorbVolMult;   // Pine: absorbVolMult = 1.95
        private double _absorbPriceThresh; // Pine: absorbPriceThresh = 0.3 (as fraction of ATR)
        private double _bigTradeMult;    // Pine: bigTradeMult = 2.1
        private bool   _showBoxes, _showAbsLine, _showLabel;

        private Brush _colBoxAbsorption, _colBoxAccum, _colBoxAggression, _colAbsLine;
        private Brush _colLabelLong, _colLabelShort;

        // Box tracking for cleanup
        private DateTime _currentDate;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "TripleA";
                Description        = "3A Phases: Absorption (A1) → Accumulation (A2) → Aggression (A3). ATR-adaptive timeout. Ported from OrderFlow Scalper Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                DisplayInDataBox   = false;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                _absorbVolMult     = 1.95;
                _absorbPriceThresh = 0.3;
                _bigTradeMult      = 2.1;

                _showBoxes  = true;
                _showAbsLine = true;
                _showLabel  = true;

                _colBoxAbsorption  = new SolidColorBrush(Color.FromArgb(60,  255, 165, 0));   // Orange 75% transparent
                _colBoxAccum       = new SolidColorBrush(Color.FromArgb(50,  255, 255, 0));   // Yellow 80%
                _colBoxAggression  = new SolidColorBrush(Color.FromArgb(75,  118, 255, 3));   // Green  70%
                _colAbsLine        = new SolidColorBrush(Color.FromArgb(200, 255, 165, 0));   // Orange solid
                _colLabelLong      = new SolidColorBrush(Color.FromArgb(255, 118, 255, 3));   // Bright green
                _colLabelShort     = new SolidColorBrush(Color.FromArgb(255, 255, 23,  68));  // Red

                // Plot: phase output (0=none, 1=A1, 2=A2, 3=A3) for long and short
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Bar, "PhaseLong");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Bar, "PhaseShort");
            }
            else if (State == State.DataLoaded)
            {
                _phaseLong  = 0; _phaseLongBars  = 0;
                _phaseShort = 0; _phaseShortBars = 0;
                _currentDate = DateTime.MinValue;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 50) return;

            // ── Session reset ─────────────────────────────────────────────────
            DateTime barDate = Time[0].Date;
            if (barDate != _currentDate)
            {
                _currentDate    = barDate;
                _phaseLong      = 0; _phaseLongBars  = 0;
                _phaseShort     = 0; _phaseShortBars = 0;
            }

            // ── Core calculations ─────────────────────────────────────────────
            double atr14   = ATR(14)[0];
            double atrSafe = Math.Max(atr14, TickSize);

            _avgVol20   = SMA(Volume, 20)[0];
            double rangeNow  = MAX(High, 10)[0] - MIN(Low, 10)[0];
            _avgRange50 = SMA(MAX(High, 10) - MIN(Low, 10), 50)[0];

            double priceMove = Math.Abs(Close[0] - Open[0]) / atrSafe;
            bool highVolume  = Volume[0] > _avgVol20 * _absorbVolMult;
            bool smallMove   = priceMove < _absorbPriceThresh;
            bool absorption  = highVolume && smallMove;

            // Absorption direction (including doji classification)
            bool absorptionUp, absorptionDown;
            if (absorption)
            {
                double mid = (High[0] + Low[0]) / 2.0;
                if (Close[0] > Open[0])
                    absorptionUp = true;
                else if (Close[0] < Open[0])
                    absorptionUp = false;
                else // doji
                    absorptionUp = Close[0] > mid || (Math.Abs(Close[0] - mid) < TickSize && Close[1] > Open[1]);
                absorptionDown = absorption && !absorptionUp;
            }
            else
            {
                absorptionUp = false; absorptionDown = false;
            }

            bool bigBuy  = Volume[0] > _avgVol20 * _bigTradeMult && Close[0] > Open[0];
            bool bigSell = Volume[0] > _avgVol20 * _bigTradeMult && Close[0] < Open[0];

            bool contraction = rangeNow < _avgRange50 * 0.6;

            // Body dominance for Phase 3
            double bodyDom3A = Math.Max(High[0] - Low[0], TickSize) > 0
                ? Math.Abs(Close[0] - Open[0]) / Math.Max(High[0] - Low[0], TickSize)
                : 0;

            // Buyers/sellers control proxy (close position)
            bool buyersControl  = (Close[0] - Low[0])  > (High[0] - Close[0]) * 1.5;
            bool sellersControl = (High[0] - Close[0]) > (Close[0] - Low[0])  * 1.5;

            bool tripleAAggrBull = Close[0] > Open[0]
                && Volume[0] > _avgVol20 * 1.3
                && bodyDom3A >= 0.40
                && (bigBuy || buyersControl);

            bool tripleAAggrBear = Close[0] < Open[0]
                && Volume[0] > _avgVol20 * 1.3
                && bodyDom3A >= 0.40
                && (bigSell || sellersControl);

            // ATR-adaptive timeout
            double atrSma50 = SMA(ATR(14), 50)[0];
            int timeout = atrSafe > atrSma50 * 1.2 ? 14 : atrSafe < atrSma50 * 0.8 ? 8 : 10;

            // ── Long phase state machine ──────────────────────────────────────
            int prevPhaseLong = _phaseLong;

            if (absorptionUp && _phaseLong < 1)
            { _phaseLong = 1; _phaseLongBars = 0; }
            else if (_phaseLong == 1 && contraction)
            { _phaseLong = 2; _phaseLongBars = 0; }
            else if (_phaseLong == 2 && tripleAAggrBull)
            { _phaseLong = 3; _phaseLongBars = 0; }
            else if (_phaseLong == 3)
            { _phaseLong = 0; }
            else if (_phaseLong > 0)
            {
                _phaseLongBars++;
                if (_phaseLongBars > timeout) { _phaseLong = 0; _phaseLongBars = 0; }
            }

            // ── Short phase state machine ─────────────────────────────────────
            int prevPhaseShort = _phaseShort;

            if (absorptionDown && _phaseShort < 1)
            { _phaseShort = 1; _phaseShortBars = 0; }
            else if (_phaseShort == 1 && contraction)
            { _phaseShort = 2; _phaseShortBars = 0; }
            else if (_phaseShort == 2 && tripleAAggrBear)
            { _phaseShort = 3; _phaseShortBars = 0; }
            else if (_phaseShort == 3)
            { _phaseShort = 0; }
            else if (_phaseShort > 0)
            {
                _phaseShortBars++;
                if (_phaseShortBars > timeout) { _phaseShort = 0; _phaseShortBars = 0; }
            }

            Values[0][0] = _phaseLong;
            Values[1][0] = _phaseShort;

            // ── Visual rendering ──────────────────────────────────────────────
            string barTag = "3A_" + CurrentBar;

            // A1 Absorption box (phase just entered = 1)
            if (_showBoxes)
            {
                if (_phaseLong == 1 && prevPhaseLong < 1)
                    Draw.Rectangle(this, "A1L_" + CurrentBar, false, 0, High[0], -1, Low[0], _colBoxAbsorption, _colBoxAbsorption, 1);
                if (_phaseLong == 2 && prevPhaseLong == 1)
                    Draw.Rectangle(this, "A2L_" + CurrentBar, false, 0, High[0], -1, Low[0], _colBoxAccum, _colBoxAccum, 1);
                if (_phaseLong == 3 && prevPhaseLong == 2)
                    Draw.Rectangle(this, "A3L_" + CurrentBar, false, 0, High[0], -1, Low[0], _colBoxAggression, _colBoxAggression, 1);

                if (_phaseShort == 1 && prevPhaseShort < 1)
                    Draw.Rectangle(this, "A1S_" + CurrentBar, false, 0, High[0], -1, Low[0], _colBoxAbsorption, _colBoxAbsorption, 1);
                if (_phaseShort == 2 && prevPhaseShort == 1)
                    Draw.Rectangle(this, "A2S_" + CurrentBar, false, 0, High[0], -1, Low[0], _colBoxAccum, _colBoxAccum, 1);
                if (_phaseShort == 3 && prevPhaseShort == 2)
                    Draw.Rectangle(this, "A3S_" + CurrentBar, false, 0, High[0], -1, Low[0], _colBoxAggression, _colBoxAggression, 1);
            }

            // Absorption level line
            if (_showAbsLine && absorptionUp)
                Draw.HorizontalLine(this, "AbsLineL_" + CurrentBar, Close[0], _colAbsLine, DashStyleHelper.Dot, 1);
            if (_showAbsLine && absorptionDown)
                Draw.HorizontalLine(this, "AbsLineS_" + CurrentBar, Close[0], _colAbsLine, DashStyleHelper.Dot, 1);

            // Completion label
            if (_showLabel)
            {
                if (_phaseLong == 3 && prevPhaseLong == 2)
                    Draw.Text(this, "3AL_" + CurrentBar, true, "3A Long",
                        0, Low[0] - TickSize * 5, 0, _colLabelLong,
                        new SimpleFont("Arial", 9), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);

                if (_phaseShort == 3 && prevPhaseShort == 2)
                    Draw.Text(this, "3AS_" + CurrentBar, true, "3A Short",
                        0, High[0] + TickSize * 5, 0, _colLabelShort,
                        new SimpleFont("Arial", 9), TextAlignment.Center,
                        Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name = "Absorption Vol Mult", Order = 1, GroupName = "TripleA")]
        public double AbsorbVolMult { get => _absorbVolMult; set => _absorbVolMult = value; }

        [NinjaScriptProperty]
        [Range(0.1, 1.0)]
        [Display(Name = "Absorption Price Thresh (ATR%)", Order = 2, GroupName = "TripleA")]
        public double AbsorbPriceThresh { get => _absorbPriceThresh; set => _absorbPriceThresh = value; }

        [NinjaScriptProperty]
        [Range(1.0, 5.0)]
        [Display(Name = "Big Trade Vol Mult", Order = 3, GroupName = "TripleA")]
        public double BigTradeMult { get => _bigTradeMult; set => _bigTradeMult = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Phase Boxes", Order = 4, GroupName = "TripleA Visuals")]
        public bool ShowBoxes { get => _showBoxes; set => _showBoxes = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show Absorption Level Line", Order = 5, GroupName = "TripleA Visuals")]
        public bool ShowAbsLine { get => _showAbsLine; set => _showAbsLine = value; }

        [NinjaScriptProperty]
        [Display(Name = "Show 3A Completion Label", Order = 6, GroupName = "TripleA Visuals")]
        public bool ShowLabel { get => _showLabel; set => _showLabel = value; }

        [XmlIgnore]
        [Display(Name = "Phase 1 Box Color (Absorption)", Order = 7, GroupName = "TripleA Visuals")]
        public Brush ColBoxAbsorption { get => _colBoxAbsorption; set => _colBoxAbsorption = value; }

        [Browsable(false)]
        public string ColBoxAbsorptionSerializable
        {
            get { return Serialize.BrushToString(_colBoxAbsorption); }
            set { _colBoxAbsorption = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Phase 2 Box Color (Accumulation)", Order = 8, GroupName = "TripleA Visuals")]
        public Brush ColBoxAccum { get => _colBoxAccum; set => _colBoxAccum = value; }

        [Browsable(false)]
        public string ColBoxAccumSerializable
        {
            get { return Serialize.BrushToString(_colBoxAccum); }
            set { _colBoxAccum = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Phase 3 Box Color (Aggression)", Order = 9, GroupName = "TripleA Visuals")]
        public Brush ColBoxAggression { get => _colBoxAggression; set => _colBoxAggression = value; }

        [Browsable(false)]
        public string ColBoxAggressionSerializable
        {
            get { return Serialize.BrushToString(_colBoxAggression); }
            set { _colBoxAggression = Serialize.StringToBrush(value); }
        }

        // Expose phase for strategies
        [Browsable(false)] public int PhaseLong  => _phaseLong;
        [Browsable(false)] public int PhaseShort => _phaseShort;

        [Browsable(false)] [XmlIgnore]
        public Series<double> PhaseLongSeries  => Values[0];

        [Browsable(false)] [XmlIgnore]
        public Series<double> PhaseShortSeries => Values[1];

        #endregion
    }
}
