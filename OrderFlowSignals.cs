#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.NinjaScript;
#endregion

// Ported from OrderFlowScalperPro.pine (lines 590-752)
// Triple-A Phase Machine: Absorption → Accumulation → Aggression
// LVN Engine: seed detection + freshness + retest proximity
// Stacked Imbalances: consecutive bull/bear count

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum AbsorptionDirection { Both, BullOnly, BearOnly }

    [Gui.CategoryOrder("Big Trades — Bubbles",  1)]
    [Gui.CategoryOrder("Absorption",            2)]
    [Gui.CategoryOrder("Big Trades — Signals",  3)]
    [Gui.CategoryOrder("Imbalances",            4)]
    [Gui.CategoryOrder("TripleA",               5)]
    [Gui.CategoryOrder("TripleA Visuals",       6)]
    [Gui.CategoryOrder("LVN",                   7)]
    [Gui.CategoryOrder("Colors",                8)]
    public class OrderFlowSignals : Indicator
    {
        private enum TaaPhase { None, Absorption, Accumulation, Aggression }

        private TaaPhase _longPhase   = TaaPhase.None;
        private TaaPhase _shortPhase  = TaaPhase.None;
        private int      _longPhaseBar  = 0;
        private int      _shortPhaseBar = 0;

        private double _avgVol;
        private bool   _absUp, _absDown;
        private int    _bullImbalanceCount;
        private int    _bearImbalanceCount;

        private double _lvnPrice = double.NaN;
        private int    _lvnBar   = -1;
        private bool   _atLvnZone;

        private readonly Queue<string> _bubbleTags = new Queue<string>();
        private readonly Queue<string> _imbalTags  = new Queue<string>();
        private const int MaxBubbles    = 600;
        private const int MaxImbalances = 400;

        private bool   _lastAbsorption;
        private bool   _lastBullImbalance;
        private bool   _lastBearImbalance;
        private double _lastAbsorptionPrice;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name        = "OrderFlowSignals";
                Description = "Big Trades + Triple-A (Absorption→Accumulation→Aggression) + LVN Engine. Ported from OrderFlow Scalper Pro.";
                Calculate   = Calculate.OnEachTick;
                IsOverlay   = true;
                MaxLookBack = MaximumBarsLookBack.Infinite;

                BigTradeMultiplier  = 3.0;
                ShowBubbles         = true;
                BigPrintSize        = 50;

                EnableAbsorption    = true;
                AbsorptionAtrFactor = 0.25;
                AbsDir              = AbsorptionDirection.Both;

                EnableImbalance     = true;
                ImbalanceRatio      = 1.5;
                StackedMinCount     = 2;

                TripleALookback     = 20;
                TaaPhaseTimeout     = 10;
                ShowTaaLabels       = true;

                EnableLvn           = true;
                LvnLookbackBars     = 60;
                LvnRetestAtrMult    = 0.35;

                ColorBigBull     = Brushes.LimeGreen;
                ColorBigBear     = Brushes.IndianRed;
                ColorAbsUp       = Brushes.DeepSkyBlue;
                ColorAbsDown     = Brushes.Magenta;
                ColorStackedBull = Brushes.Cyan;
                ColorStackedBear = Brushes.OrangeRed;
                ColorTaaLong     = Brushes.Lime;
                ColorTaaShort    = Brushes.Red;
                ColorLvn         = Brushes.Yellow;

                AddPlot(new Stroke(Brushes.Lime, 2), PlotStyle.TriangleUp,   "PhaseLong");
                AddPlot(new Stroke(Brushes.Red,  2), PlotStyle.TriangleDown, "PhaseShort");
            }
            else if (State == State.DataLoaded)
            {
                _bubbleTags.Clear();
                _imbalTags.Clear();
                _longPhase  = TaaPhase.None;
                _shortPhase = TaaPhase.None;
                _lvnPrice   = double.NaN;
                _lvnBar     = -1;
                _bullImbalanceCount = 0;
                _bearImbalanceCount = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            int warmup = Math.Max(20, TripleALookback);
            if (CurrentBar < warmup)
            {
                Values[0][0] = double.NaN;
                Values[1][0] = double.NaN;
                return;
            }

            _avgVol = SMA(Volume, 20)[0];
            double atr     = ATR(14)[0];
            double atrSafe = Math.Max(atr, TickSize);

            _absUp = false; _absDown = false;
            _lastAbsorption = false; _lastAbsorptionPrice = double.NaN;

            if (EnableAbsorption) DetectAbsorption(atrSafe);
            if (EnableImbalance)  DetectImbalances();
            if (EnableLvn)        DetectLvn(atrSafe);

            bool bigBull = Close[0] >= Open[0] && Volume[0] > _avgVol * BigTradeMultiplier;
            bool bigBear = Close[0] <  Open[0] && Volume[0] > _avgVol * BigTradeMultiplier;
            if (ShowBubbles)
            {
                if (bigBull) DrawBubble($"OFS_BB_{CurrentBar}", Low[0]  - TickSize * 2, ColorBigBull);
                if (bigBear) DrawBubble($"OFS_BS_{CurrentBar}", High[0] + TickSize * 2, ColorBigBear);
            }

            Values[0][0] = double.NaN;
            Values[1][0] = double.NaN;

            if (IsFirstTickOfBar) RunTripleAMachine();
        }

        private void DetectAbsorption(double atrSafe)
        {
            double move   = High[0] - Low[0];
            bool highVol  = Volume[0] > _avgVol * BigTradeMultiplier;
            bool absorbed = highVol && move < atrSafe * AbsorptionAtrFactor;
            if (!absorbed) return;

            bool bull = Close[0] >= Open[0] || Low[0] == Low[1];
            bool bear = Close[0] <  Open[0] || High[0] == High[1];

            _absUp   = bull && AbsDir != AbsorptionDirection.BearOnly;
            _absDown = bear && AbsDir != AbsorptionDirection.BullOnly;
            _lastAbsorption      = true;
            _lastAbsorptionPrice = (High[0] + Low[0]) * 0.5;

            if (_absUp)   Draw.Text(this, $"OFS_ABS_U_{CurrentBar}", "ABS↑", 0, Low[0]  - TickSize * 3, ColorAbsUp);
            if (_absDown) Draw.Text(this, $"OFS_ABS_D_{CurrentBar}", "ABS↓", 0, High[0] + TickSize * 3, ColorAbsDown);
        }

        private void DetectImbalances()
        {
            double bodyTop = Math.Max(Open[0], Close[0]);
            double bodyBot = Math.Min(Open[0], Close[0]);
            double upper   = High[0] - bodyTop;
            double lower   = bodyBot - Low[0];

            bool bullImb = lower > upper * ImbalanceRatio;
            bool bearImb = upper > lower * ImbalanceRatio;

            _bullImbalanceCount = bullImb ? _bullImbalanceCount + 1 : 0;
            _bearImbalanceCount = bearImb ? _bearImbalanceCount + 1 : 0;

            _lastBullImbalance = bullImb;
            _lastBearImbalance = bearImb;

            if (bullImb)
            {
                Brush c   = _bullImbalanceCount >= StackedMinCount ? ColorStackedBull : ColorBigBull;
                string tag = $"OFS_IMB_B_{CurrentBar}";
                Draw.Dot(this, tag, false, 0, Low[0], c);
                AddImbalTag(tag);
                if (_bullImbalanceCount >= StackedMinCount)
                    Draw.Text(this, $"OFS_SIMB_B_{CurrentBar}", "Stack↑", 0, Low[0] - TickSize * 4, ColorStackedBull);
            }
            if (bearImb)
            {
                Brush c   = _bearImbalanceCount >= StackedMinCount ? ColorStackedBear : ColorBigBear;
                string tag = $"OFS_IMB_S_{CurrentBar}";
                Draw.Dot(this, tag, false, 0, High[0], c);
                AddImbalTag(tag);
                if (_bearImbalanceCount >= StackedMinCount)
                    Draw.Text(this, $"OFS_SIMB_S_{CurrentBar}", "Stack↓", 0, High[0] + TickSize * 4, ColorStackedBear);
            }
        }

        private void DetectLvn(double atrSafe)
        {
            if (CurrentBar < 5) return;
            double avgRange = ATR(5)[0];
            double range    = High[0] - Low[0];

            bool lvnSeed = Volume[0] < _avgVol * 0.5
                        && range > avgRange * 0.4
                        && range < avgRange * 1.2;

            if (lvnSeed) { _lvnPrice = (High[0] + Low[0]) * 0.5; _lvnBar = CurrentBar; }

            bool lvnFresh = !double.IsNaN(_lvnPrice)
                         && (CurrentBar - _lvnBar) <= LvnLookbackBars;

            _atLvnZone = lvnFresh && Math.Abs(Close[0] - _lvnPrice) <= atrSafe * LvnRetestAtrMult;

            if (_atLvnZone && !lvnSeed)
            {
                Draw.HorizontalLine(this, "OFS_LVN", _lvnPrice, ColorLvn, DashStyleHelper.Dot, 1);
                if (_absUp || _lastBullImbalance)
                    Draw.Text(this, $"OFS_LVN_REACT_{CurrentBar}", "LVN↑", 0, Low[0] - TickSize * 5, ColorLvn);
                else if (_absDown || _lastBearImbalance)
                    Draw.Text(this, $"OFS_LVN_REACT_{CurrentBar}", "LVN↓", 0, High[0] + TickSize * 5, ColorLvn);
            }
        }

        private void RunTripleAMachine()
        {
            double atr14   = ATR(14)[0];
            int    timeout = Math.Max(3, TaaPhaseTimeout);

            // LONG machine
            switch (_longPhase)
            {
                case TaaPhase.None:
                    if (_absUp) { _longPhase = TaaPhase.Absorption; _longPhaseBar = CurrentBar; }
                    break;
                case TaaPhase.Absorption:
                    if (CurrentBar - _longPhaseBar > timeout) { _longPhase = TaaPhase.None; break; }
                    if ((High[0] <= High[1] && Low[0] >= Low[1]) || (High[0] - Low[0]) < atr14 * 0.6)
                    {
                        _longPhase = TaaPhase.Accumulation; _longPhaseBar = CurrentBar;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_TAA_ACC_L_{CurrentBar}", "② Accum", 0, Low[0] - TickSize * 5, ColorTaaLong);
                    }
                    break;
                case TaaPhase.Accumulation:
                    if (CurrentBar - _longPhaseBar > timeout) { _longPhase = TaaPhase.None; break; }
                    if (Close[0] > MAX(High, TripleALookback)[1] && Volume[0] > _avgVol)
                    {
                        Values[0][0] = Low[0] - TickSize * 2;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_TAA_AGG_L_{CurrentBar}", "③ Aggr↑", 0, Low[0] - TickSize * 7, ColorTaaLong);
                        _longPhase = TaaPhase.None;
                    }
                    break;
            }

            // SHORT machine
            switch (_shortPhase)
            {
                case TaaPhase.None:
                    if (_absDown) { _shortPhase = TaaPhase.Absorption; _shortPhaseBar = CurrentBar; }
                    break;
                case TaaPhase.Absorption:
                    if (CurrentBar - _shortPhaseBar > timeout) { _shortPhase = TaaPhase.None; break; }
                    if ((High[0] <= High[1] && Low[0] >= Low[1]) || (High[0] - Low[0]) < atr14 * 0.6)
                    {
                        _shortPhase = TaaPhase.Accumulation; _shortPhaseBar = CurrentBar;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_TAA_ACC_S_{CurrentBar}", "② Accum", 0, High[0] + TickSize * 5, ColorTaaShort);
                    }
                    break;
                case TaaPhase.Accumulation:
                    if (CurrentBar - _shortPhaseBar > timeout) { _shortPhase = TaaPhase.None; break; }
                    if (Close[0] < MIN(Low, TripleALookback)[1] && Volume[0] > _avgVol)
                    {
                        Values[1][0] = High[0] + TickSize * 2;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_TAA_AGG_S_{CurrentBar}", "③ Aggr↓", 0, High[0] + TickSize * 7, ColorTaaShort);
                        _shortPhase = TaaPhase.None;
                    }
                    break;
            }
        }

        private void DrawBubble(string tag, double price, Brush color)
        {
            Draw.Dot(this, tag, false, 0, price, color);
            _bubbleTags.Enqueue(tag);
            if (_bubbleTags.Count > MaxBubbles) RemoveDrawObject(_bubbleTags.Dequeue());
        }

        private void AddImbalTag(string tag)
        {
            _imbalTags.Enqueue(tag);
            if (_imbalTags.Count > MaxImbalances) RemoveDrawObject(_imbalTags.Dequeue());
        }

        protected override void OnMarketData(MarketDataEventArgs md)
        {
            if (!ShowBubbles || md.MarketDataType != MarketDataType.Last || md.Volume < (long)Math.Max(1, BigPrintSize)) return;
            string tag = $"OFS_TICK_{CurrentBar}_{md.Time.Ticks % 1000000}";
            Draw.Dot(this, tag, false, 0, md.Price, ColorBigBull);
            _bubbleTags.Enqueue(tag);
            if (_bubbleTags.Count > MaxBubbles) RemoveDrawObject(_bubbleTags.Dequeue());
        }

        #region Properties
        [NinjaScriptProperty, Display(Name = "Show Bubbles",        GroupName = "Big Trades — Bubbles",  Order = 1)] public bool   ShowBubbles         { get; set; }
        [NinjaScriptProperty, Range(1, int.MaxValue), Display(Name = "Big Print Min Size", GroupName = "Big Trades — Bubbles",  Order = 2)] public int    BigPrintSize        { get; set; }
        [NinjaScriptProperty, Display(Name = "Enable Absorption",   GroupName = "Absorption",            Order = 1)] public bool   EnableAbsorption     { get; set; }
        [NinjaScriptProperty, Range(0.05, 3.0), Display(Name = "Absorption ATR Factor", GroupName = "Absorption", Order = 2)]   public double AbsorptionAtrFactor  { get; set; }
        [NinjaScriptProperty, Display(Name = "Direction",           GroupName = "Absorption",            Order = 3)] public AbsorptionDirection AbsDir { get; set; }
        [NinjaScriptProperty, Range(1.0, 20.0), Display(Name = "Big Trade Multiplier", GroupName = "Big Trades — Signals", Order = 1)] public double BigTradeMultiplier  { get; set; }
        [NinjaScriptProperty, Display(Name = "Enable Imbalances",   GroupName = "Imbalances",            Order = 1)] public bool   EnableImbalance      { get; set; }
        [NinjaScriptProperty, Range(1.0, 5.0), Display(Name = "Imbalance Ratio",       GroupName = "Imbalances", Order = 2)]   public double ImbalanceRatio       { get; set; }
        [NinjaScriptProperty, Range(2, 10), Display(Name = "Stacked Min Count",         GroupName = "Imbalances", Order = 3)]   public int    StackedMinCount      { get; set; }
        [NinjaScriptProperty, Range(5, 200), Display(Name = "Lookback Bars",            GroupName = "TripleA",    Order = 1)]   public int    TripleALookback      { get; set; }
        [NinjaScriptProperty, Range(3, 50), Display(Name = "Phase Timeout (bars)",      GroupName = "TripleA",    Order = 2)]   public int    TaaPhaseTimeout      { get; set; }
        [NinjaScriptProperty, Display(Name = "Show Labels",         GroupName = "TripleA Visuals",       Order = 1)] public bool   ShowTaaLabels        { get; set; }
        [NinjaScriptProperty, Display(Name = "Enable LVN Engine",   GroupName = "LVN",                   Order = 1)] public bool   EnableLvn            { get; set; }
        [NinjaScriptProperty, Range(10, 500), Display(Name = "LVN Lookback Bars",       GroupName = "LVN", Order = 2)]          public int    LvnLookbackBars      { get; set; }
        [NinjaScriptProperty, Range(0.1, 2.0), Display(Name = "LVN Retest ATR Mult",   GroupName = "LVN", Order = 3)]          public double LvnRetestAtrMult     { get; set; }

        [XmlIgnore, Display(Name = "Big Bull",        GroupName = "Colors", Order = 1)] public Brush ColorBigBull     { get; set; }
        [Browsable(false)] public string ColorBigBullSerializable     { get => Serialize.BrushToString(ColorBigBull);     set => ColorBigBull     = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Big Bear",        GroupName = "Colors", Order = 2)] public Brush ColorBigBear     { get; set; }
        [Browsable(false)] public string ColorBigBearSerializable     { get => Serialize.BrushToString(ColorBigBear);     set => ColorBigBear     = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Absorption ↑",   GroupName = "Colors", Order = 3)] public Brush ColorAbsUp       { get; set; }
        [Browsable(false)] public string ColorAbsUpSerializable       { get => Serialize.BrushToString(ColorAbsUp);       set => ColorAbsUp       = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Absorption ↓",   GroupName = "Colors", Order = 4)] public Brush ColorAbsDown     { get; set; }
        [Browsable(false)] public string ColorAbsDownSerializable     { get => Serialize.BrushToString(ColorAbsDown);     set => ColorAbsDown     = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Stacked Bull",   GroupName = "Colors", Order = 5)] public Brush ColorStackedBull { get; set; }
        [Browsable(false)] public string ColorStackedBullSerializable { get => Serialize.BrushToString(ColorStackedBull); set => ColorStackedBull = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Stacked Bear",   GroupName = "Colors", Order = 6)] public Brush ColorStackedBear { get; set; }
        [Browsable(false)] public string ColorStackedBearSerializable { get => Serialize.BrushToString(ColorStackedBear); set => ColorStackedBear = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Triple-A Long",  GroupName = "Colors", Order = 7)] public Brush ColorTaaLong     { get; set; }
        [Browsable(false)] public string ColorTaaLongSerializable     { get => Serialize.BrushToString(ColorTaaLong);     set => ColorTaaLong     = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "Triple-A Short", GroupName = "Colors", Order = 8)] public Brush ColorTaaShort    { get; set; }
        [Browsable(false)] public string ColorTaaShortSerializable    { get => Serialize.BrushToString(ColorTaaShort);    set => ColorTaaShort    = Serialize.StringToBrush(value); }
        [XmlIgnore, Display(Name = "LVN",            GroupName = "Colors", Order = 9)] public Brush ColorLvn         { get; set; }
        [Browsable(false)] public string ColorLvnSerializable         { get => Serialize.BrushToString(ColorLvn);         set => ColorLvn         = Serialize.StringToBrush(value); }

        [Browsable(false)] public Series<double> PhaseLong            => Values[0];
        [Browsable(false)] public Series<double> PhaseShort           => Values[1];
        [Browsable(false)] public bool   LastAbsorption               => _lastAbsorption;
        [Browsable(false)] public double LastAbsorptionPrice          => _lastAbsorptionPrice;
        [Browsable(false)] public bool   LastBullImbalance            => _lastBullImbalance;
        [Browsable(false)] public bool   LastBearImbalance            => _lastBearImbalance;
        [Browsable(false)] public bool   StackedBullImbalance         => _bullImbalanceCount >= StackedMinCount;
        [Browsable(false)] public bool   StackedBearImbalance         => _bearImbalanceCount >= StackedMinCount;
        [Browsable(false)] public bool   AtLvnZone                    => _atLvnZone;
        [Browsable(false)] public double LvnPrice                     => _lvnPrice;
        #endregion
    }
}
