// Converted from OrderFlow Scalper Pro Pine Script — ABSORPTION & BIG TRADES section
// Pine logic: absorption = highVolume AND smallMove (priceMove < absorbPriceThresh * ATR)
// Doji fix: close == open classified by wick bias (close position within high-low range)
// Big buy/sell: volume > avgVol * bigTradeMult AND directional close
// Imbalances: lowerHalf > upperHalf * ratio (bull) | upperHalf > lowerHalf * ratio (bear)
// Stacked imbalances: consecutive imbalance count >= stackedMinCount

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class BigTrades : Indicator
    {
        // ── Tick-level volume accumulators (current bar) ──────────────────────
        private long _tradeCounter;
        private long _barBuyVol, _barSellVol, _barNeutVol;
        private long _prevBarBuyVol, _prevBarSellVol, _prevBarNeutVol;

        // ── Rolling 20-bar average volume ─────────────────────────────────────
        private long _avgVol20;
        private readonly System.Collections.Generic.Queue<long> _volQueue
            = new System.Collections.Generic.Queue<long>();

        // ── Imbalance streak counters ─────────────────────────────────────────
        private int _bullImbalanceCount;
        private int _bearImbalanceCount;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                         = "Big Trades";
                Description                  = "Tick bubbles + Absorption (doji-safe) + Big Buy/Sell + Imbalance signals. Ported from OrderFlow Scalper Pro.";
                Calculate                    = Calculate.OnEachTick;
                IsOverlay                    = true;
                DrawOnPricePanel             = true;
                IsSuspendedWhileInactive     = false;

                // Bubble settings
                MinTradeSize        = 50;
                MinBubbleSizeTicks  = 2;
                MaxBubbleSizeTicks  = 12;
                VolumeScale         = 25;
                BubbleWidthSeconds  = 2;
                FillOpacity         = 55;
                ShowTradeSizeText   = false;

                // Absorption settings
                ShowAbsorption      = true;
                AbsorbVolMult       = 1.95;
                AbsorbMovePct       = 0.30;     // fraction of ATR: Pine absorbPriceThresh

                // Big buy/sell settings
                ShowBigTrades       = true;
                BigTradeMult        = 2.5;

                // Imbalance settings
                ShowImbalances      = true;
                ImbalanceRatio      = 3.0;      // Pine imbalanceRatio default
                StackedMinCount     = 2;        // Pine stackedMinCount default

                // Colors
                BuyTradeColor       = new SolidColorBrush(Color.FromRgb(0, 255, 0));      // lime
                SellTradeColor      = new SolidColorBrush(Color.FromRgb(255,  69,  0));   // orangered
                NeutralTradeColor   = new SolidColorBrush(Color.FromRgb(255, 215,  0));   // gold
                AbsorbBuyColor      = new SolidColorBrush(Color.FromRgb(0, 255, 0));
                AbsorbSellColor     = new SolidColorBrush(Color.FromRgb(255,   0,  0));
                BigBuyColor         = new SolidColorBrush(Color.FromRgb(0, 200, 255));    // cyan
                BigSellColor        = new SolidColorBrush(Color.FromRgb(255,  50, 180));  // magenta
                ImbalanceBullColor  = new SolidColorBrush(Color.FromRgb(43, 255, 0));
                ImbalanceBearColor  = new SolidColorBrush(Color.FromRgb(255, 50, 50));
                StackedBullColor    = new SolidColorBrush(Color.FromRgb(255, 255, 0));    // yellow
                StackedBearColor    = new SolidColorBrush(Color.FromRgb(255, 128, 0));    // orange
            }
            else if (State == State.DataLoaded)
            {
                _tradeCounter = 0;
                _barBuyVol = _barSellVol = _barNeutVol = 0;
                _prevBarBuyVol = _prevBarSellVol = _prevBarNeutVol = 0;
                _avgVol20 = 0;
                _bullImbalanceCount = _bearImbalanceCount = 0;
                _volQueue.Clear();
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < 2) return;

            if (IsFirstTickOfBar)
            {
                // ── Capture previous bar totals before resetting ───────────────
                long barTot = _barBuyVol + _barSellVol + _barNeutVol;
                _prevBarBuyVol  = _barBuyVol;
                _prevBarSellVol = _barSellVol;
                _prevBarNeutVol = _barNeutVol;
                _barBuyVol = _barSellVol = _barNeutVol = 0;

                if (barTot > 0)
                {
                    _volQueue.Enqueue(barTot);
                    while (_volQueue.Count > 20) _volQueue.Dequeue();
                    long sum = 0;
                    foreach (long v in _volQueue) sum += v;
                    _avgVol20 = sum / _volQueue.Count;
                }

                if (_avgVol20 <= 0) return;

                long   prevTotal  = _prevBarBuyVol + _prevBarSellVol + _prevBarNeutVol;
                double prevClose  = Close[1];
                double prevOpen   = Open[1];
                double prevHigh   = High[1];
                double prevLow    = Low[1];
                double atr        = ATR(14)[1];
                double atrSafe    = Math.Max(atr, TickSize);

                // ── Absorption: high volume + small price move (Pine logic) ────
                double priceMove  = Math.Abs(prevClose - prevOpen) / atrSafe;
                bool   highVol    = prevTotal >= _avgVol20 * AbsorbVolMult;
                bool   smallMove  = priceMove < AbsorbMovePct;
                bool   absorption = highVol && smallMove;

                if (ShowAbsorption && absorption)
                {
                    // Doji classification by wick bias (Pine FIX for doji absorption)
                    bool isAbsUp;
                    if (prevClose > prevOpen)
                    {
                        isAbsUp = true;
                    }
                    else if (prevClose < prevOpen)
                    {
                        isAbsUp = false;
                    }
                    else
                    {
                        double midPoint = (prevHigh + prevLow) / 2.0;
                        if (prevClose > midPoint)
                            isAbsUp = true;
                        else if (prevClose < midPoint)
                            isAbsUp = false;
                        else
                            // Perfect doji — fall back to prior bar direction
                            isAbsUp = Close[2] > Open[2];
                    }

                    Brush absBrush = isAbsUp ? AbsorbBuyColor : AbsorbSellColor;
                    double aDot    = isAbsUp
                        ? prevLow  - TickSize * 3
                        : prevHigh + TickSize * 3;
                    Draw.Diamond(this, "Abs_" + (CurrentBar - 1), false, 1, aDot, absBrush);
                }

                // ── Big buy / big sell (Pine: volume > avgVol * bigTradeMult) ──
                if (ShowBigTrades)
                {
                    bool bigBuy  = prevTotal >= _avgVol20 * BigTradeMult && prevClose > prevOpen;
                    bool bigSell = prevTotal >= _avgVol20 * BigTradeMult && prevClose < prevOpen;

                    if (bigBuy)
                        Draw.ArrowUp(this, "BigBuy_" + (CurrentBar - 1), false, 1,
                            prevLow - TickSize * 6, BigBuyColor);
                    if (bigSell)
                        Draw.ArrowDown(this, "BigSell_" + (CurrentBar - 1), false, 1,
                            prevHigh + TickSize * 6, BigSellColor);
                }

                // ── Imbalances (Pine: upperHalf/lowerHalf ratio + volume filter) ─
                if (ShowImbalances)
                {
                    double candleRange = Math.Max(prevHigh - prevLow, TickSize);
                    double upperHalf   = (prevHigh - prevClose) / candleRange;
                    double lowerHalf   = (prevClose - prevLow)  / candleRange;

                    bool bullImbalance = lowerHalf > upperHalf * ImbalanceRatio
                                      && prevTotal > _avgVol20 * 1.5
                                      && prevClose > prevOpen;
                    bool bearImbalance = upperHalf > lowerHalf * ImbalanceRatio
                                      && prevTotal > _avgVol20 * 1.5
                                      && prevClose < prevOpen;

                    // Update streak counters (Pine: reset opposite on each bar)
                    if (bullImbalance)
                    { _bullImbalanceCount++; _bearImbalanceCount = 0; }
                    else if (bearImbalance)
                    { _bearImbalanceCount++; _bullImbalanceCount = 0; }
                    else
                    { _bullImbalanceCount = 0; _bearImbalanceCount = 0; }

                    bool stackedBull = _bullImbalanceCount >= StackedMinCount;
                    bool stackedBear = _bearImbalanceCount >= StackedMinCount;

                    if (stackedBull)
                        Draw.ArrowUp(this, "StkBull_" + (CurrentBar - 1), false, 1,
                            prevLow - TickSize * 9, StackedBullColor);
                    else if (bullImbalance)
                        Draw.TriangleUp(this, "ImbBull_" + (CurrentBar - 1), false, 1,
                            prevLow - TickSize * 5, ImbalanceBullColor);

                    if (stackedBear)
                        Draw.ArrowDown(this, "StkBear_" + (CurrentBar - 1), false, 1,
                            prevHigh + TickSize * 9, StackedBearColor);
                    else if (bearImbalance)
                        Draw.TriangleDown(this, "ImbBear_" + (CurrentBar - 1), false, 1,
                            prevHigh + TickSize * 5, ImbalanceBearColor);
                }
            }
        }

        protected override void OnMarketData(MarketDataEventArgs marketArgs)
        {
            if (marketArgs == null || marketArgs.MarketDataType != MarketDataType.Last) return;
            if (marketArgs.Volume < MinTradeSize) return;
            if (CurrentBar < 0) return;

            _tradeCounter++;
            bool isBuy  = marketArgs.Ask > 0 && marketArgs.Price >= marketArgs.Ask;
            bool isSell = marketArgs.Bid > 0 && marketArgs.Price <= marketArgs.Bid;

            Brush markerBrush = NeutralTradeColor;
            string sideTag    = "N";

            if (isBuy)
            {
                markerBrush = BuyTradeColor;
                sideTag     = "B";
                _barBuyVol += (long)marketArgs.Volume;
            }
            else if (isSell)
            {
                markerBrush = SellTradeColor;
                sideTag     = "S";
                _barSellVol += (long)marketArgs.Volume;
            }
            else
            {
                _barNeutVol += (long)marketArgs.Volume;
                return;  // only draw classified trades as bubbles
            }

            string tag         = string.Format("BigTrade_{0}_{1}_{2}", CurrentBar, _tradeCounter, sideTag);
            double bubbleTicks = GetBubbleSizeTicks(marketArgs.Volume);
            double y           = marketArgs.Price;
            double y1          = y - bubbleTicks * TickSize;
            double y2          = y + bubbleTicks * TickSize;
            double halfSec     = Math.Max(0.5, BubbleWidthSeconds / 2.0);
            DateTime t0        = marketArgs.Time.AddSeconds(-halfSec);
            DateTime t1        = marketArgs.Time.AddSeconds(halfSec);

            Draw.Ellipse(this, tag, false, t0, y1, t1, y2, markerBrush, markerBrush, FillOpacity);

            if (ShowTradeSizeText)
                Draw.Text(this, tag + "_txt", false,
                    string.Format("{0}", marketArgs.Volume), 0, y, 0,
                    Brushes.White, new SimpleFont("Arial", 9), TextAlignment.Center,
                    Brushes.Transparent, Brushes.Transparent, 0);
        }

        private double GetBubbleSizeTicks(long volume)
        {
            double extra = volume / (double)Math.Max(1, VolumeScale);
            double ticks = MinBubbleSizeTicks + extra;
            return Math.Max(MinBubbleSizeTicks, Math.Min(MaxBubbleSizeTicks, ticks));
        }

        #region Properties

        // ── Bubble ────────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Range(1, long.MaxValue)]
        [Display(Name = "Min Trade Size", Order = 0, GroupName = "Big Trades — Bubbles")]
        public long MinTradeSize { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Min Bubble (ticks)", Order = 1, GroupName = "Big Trades — Bubbles")]
        public int MinBubbleSizeTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 80)]
        [Display(Name = "Max Bubble (ticks)", Order = 2, GroupName = "Big Trades — Bubbles")]
        public int MaxBubbleSizeTicks { get; set; }

        [NinjaScriptProperty]
        [Range(1, 1000)]
        [Display(Name = "Volume Scale", Order = 3, GroupName = "Big Trades — Bubbles")]
        public int VolumeScale { get; set; }

        [NinjaScriptProperty]
        [Range(1, 20)]
        [Display(Name = "Bubble Width (sec)", Order = 4, GroupName = "Big Trades — Bubbles")]
        public int BubbleWidthSeconds { get; set; }

        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Bubble Opacity %", Order = 5, GroupName = "Big Trades — Bubbles")]
        public int FillOpacity { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Size Text", Order = 6, GroupName = "Big Trades — Bubbles")]
        public bool ShowTradeSizeText { get; set; }

        // ── Absorption ────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show Absorption", Order = 0, GroupName = "Absorption")]
        public bool ShowAbsorption { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Absorption Volume Mult", Order = 1, GroupName = "Absorption")]
        public double AbsorbVolMult { get; set; }

        [NinjaScriptProperty]
        [Range(0.05, 2.0)]
        [Display(Name = "Absorption Move (ATR fraction)", Order = 2, GroupName = "Absorption")]
        public double AbsorbMovePct { get; set; }

        // ── Big buy/sell ──────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show Big Buy/Sell", Order = 0, GroupName = "Big Trades — Signals")]
        public bool ShowBigTrades { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 20.0)]
        [Display(Name = "Big Trade Volume Mult", Order = 1, GroupName = "Big Trades — Signals")]
        public double BigTradeMult { get; set; }

        // ── Imbalances ────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show Imbalances", Order = 0, GroupName = "Imbalances")]
        public bool ShowImbalances { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 10.0)]
        [Display(Name = "Imbalance Ratio", Order = 1, GroupName = "Imbalances")]
        public double ImbalanceRatio { get; set; }

        [NinjaScriptProperty]
        [Range(2, 10)]
        [Display(Name = "Stacked Min Count", Order = 2, GroupName = "Imbalances")]
        public int StackedMinCount { get; set; }

        // ── Colors ────────────────────────────────────────────────────────────
        [XmlIgnore]
        [Display(Name = "Buy Bubble Color", Order = 0, GroupName = "Colors")]
        public Brush BuyTradeColor { get; set; }
        [Browsable(false)]
        public string BuyTradeColorSerializable
        { get { return Serialize.BrushToString(BuyTradeColor); } set { BuyTradeColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Sell Bubble Color", Order = 1, GroupName = "Colors")]
        public Brush SellTradeColor { get; set; }
        [Browsable(false)]
        public string SellTradeColorSerializable
        { get { return Serialize.BrushToString(SellTradeColor); } set { SellTradeColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Neutral Bubble Color", Order = 2, GroupName = "Colors")]
        public Brush NeutralTradeColor { get; set; }
        [Browsable(false)]
        public string NeutralTradeColorSerializable
        { get { return Serialize.BrushToString(NeutralTradeColor); } set { NeutralTradeColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Absorption Buy Color", Order = 3, GroupName = "Colors")]
        public Brush AbsorbBuyColor { get; set; }
        [Browsable(false)]
        public string AbsorbBuyColorSerializable
        { get { return Serialize.BrushToString(AbsorbBuyColor); } set { AbsorbBuyColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Absorption Sell Color", Order = 4, GroupName = "Colors")]
        public Brush AbsorbSellColor { get; set; }
        [Browsable(false)]
        public string AbsorbSellColorSerializable
        { get { return Serialize.BrushToString(AbsorbSellColor); } set { AbsorbSellColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Big Buy Color", Order = 5, GroupName = "Colors")]
        public Brush BigBuyColor { get; set; }
        [Browsable(false)]
        public string BigBuyColorSerializable
        { get { return Serialize.BrushToString(BigBuyColor); } set { BigBuyColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Big Sell Color", Order = 6, GroupName = "Colors")]
        public Brush BigSellColor { get; set; }
        [Browsable(false)]
        public string BigSellColorSerializable
        { get { return Serialize.BrushToString(BigSellColor); } set { BigSellColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Bull Imbalance Color", Order = 7, GroupName = "Colors")]
        public Brush ImbalanceBullColor { get; set; }
        [Browsable(false)]
        public string ImbalanceBullColorSerializable
        { get { return Serialize.BrushToString(ImbalanceBullColor); } set { ImbalanceBullColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Bear Imbalance Color", Order = 8, GroupName = "Colors")]
        public Brush ImbalanceBearColor { get; set; }
        [Browsable(false)]
        public string ImbalanceBearColorSerializable
        { get { return Serialize.BrushToString(ImbalanceBearColor); } set { ImbalanceBearColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Stacked Bull Color", Order = 9, GroupName = "Colors")]
        public Brush StackedBullColor { get; set; }
        [Browsable(false)]
        public string StackedBullColorSerializable
        { get { return Serialize.BrushToString(StackedBullColor); } set { StackedBullColor = Serialize.StringToBrush(value); } }

        [XmlIgnore]
        [Display(Name = "Stacked Bear Color", Order = 10, GroupName = "Colors")]
        public Brush StackedBearColor { get; set; }
        [Browsable(false)]
        public string StackedBearColorSerializable
        { get { return Serialize.BrushToString(StackedBearColor); } set { StackedBearColor = Serialize.StringToBrush(value); } }

        #endregion
    }
}
