#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

// Big Trades: variable-size SharpDX circles (volume-proportional, bid/ask classified)
// Absorption:  variable-size SharpDX diamonds (volume-ratio proportional)
// Triple-A:   Fabio Valentini sequence — Absorption → Accumulation → Aggression

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum AbsorptionDirection { Both, BullOnly, BearOnly }

    [Gui.CategoryOrder("Big Trades",  1)]
    [Gui.CategoryOrder("Absorption",  2)]
    [Gui.CategoryOrder("Triple-A",    3)]
    [Gui.CategoryOrder("Colors",      4)]
    public class OrderFlowSignals : Indicator
    {
        // ── Render queues (shared between data thread and UI thread) ──────────
        private struct BubblePrint
        {
            public int    BarIdx;
            public double Price;
            public long   Volume;
            public bool   IsBuy;
        }
        private struct DiamondPrint
        {
            public int    BarIdx;
            public double Price;
            public double VolRatio;  // Volume / AvgVolume
            public bool   IsBull;
        }

        private readonly Queue<BubblePrint>  _bubbles  = new Queue<BubblePrint>();
        private readonly Queue<DiamondPrint> _diamonds = new Queue<DiamondPrint>();
        private readonly object _renderLock = new object();
        private const int MaxBubbles  = 500;
        private const int MaxDiamonds = 200;

        // ── Cached SharpDX brushes ────────────────────────────────────────────
        private SharpDX.Direct2D1.RenderTarget _cachedRt;
        private SharpDX.Direct2D1.Brush _dxBull, _dxBear, _dxAbsUp, _dxAbsDown, _dxBorder;

        // ── Triple-A state machine ────────────────────────────────────────────
        private enum TaaPhase { None, Absorption, Accumulation, Aggression }
        private TaaPhase _longPhase  = TaaPhase.None;
        private TaaPhase _shortPhase = TaaPhase.None;
        private int      _longPhaseBar, _shortPhaseBar;

        // ── Working state ─────────────────────────────────────────────────────
        private double _avgVol;
        private bool   _absUp, _absDown;
        private double _lastBid = double.NaN;
        private double _lastAsk = double.NaN;

        // Exposed for cross-indicator reading
        private bool   _lastAbsorption;
        private double _lastAbsorptionPrice;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name        = "OrderFlowSignals";
                Description = "Big Trades (circles) + Absorption (diamonds) + Triple-A signal machine.";
                Calculate   = Calculate.OnEachTick;
                IsOverlay   = true;
                MaximumBarsLookBack = MaximumBarsLookBack.Infinite;
                IsSuspendedWhileInactive = true;

                // Big Trades
                ShowBubbles        = true;
                BigPrintSize       = 50;
                BigTradeMultiplier = 3.0;
                BubbleMaxRadius    = 28;
                BubbleOpacity      = 85;

                // Absorption
                EnableAbsorption    = true;
                AbsorptionMultiplier = 2.5;
                AbsorptionAtrFactor = 0.30;
                AbsDir              = AbsorptionDirection.Both;
                DiamondMaxRadius    = 22;

                // Triple-A
                EnableTripleA   = true;
                TripleALookback  = 20;
                TaaPhaseTimeout  = 10;
                ShowTaaLabels    = true;

                // Colors
                ColorBigBull  = Brushes.LimeGreen;
                ColorBigBear  = Brushes.IndianRed;
                ColorAbsUp    = Brushes.DeepSkyBlue;
                ColorAbsDown  = Brushes.Magenta;
                ColorTaaLong  = Brushes.Lime;
                ColorTaaShort = Brushes.OrangeRed;

                AddPlot(new Stroke(Brushes.Lime,      2), PlotStyle.TriangleUp,   "PhaseLong");
                AddPlot(new Stroke(Brushes.OrangeRed, 2), PlotStyle.TriangleDown, "PhaseShort");
            }
            else if (State == State.DataLoaded)
            {
                lock (_renderLock)
                {
                    _bubbles.Clear();
                    _diamonds.Clear();
                }
                _longPhase  = TaaPhase.None;
                _shortPhase = TaaPhase.None;
                _lastAbsorption = false;
                _lastAbsorptionPrice = double.NaN;
            }
            else if (State == State.Terminated)
            {
                DisposeDxBrushes();
            }
        }

        // ── OnBarUpdate: absorption detection + 3A machine ───────────────────
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
            _lastAbsorption = false;

            if (EnableAbsorption) DetectAbsorption(atrSafe);

            Values[0][0] = double.NaN;
            Values[1][0] = double.NaN;

            if (EnableTripleA && IsFirstTickOfBar) RunTripleAMachine(atrSafe);
        }

        // ── Absorption: high-volume narrow-range candle ───────────────────────
        private void DetectAbsorption(double atrSafe)
        {
            double move   = High[0] - Low[0];
            bool highVol  = _avgVol > 0 && Volume[0] > _avgVol * AbsorptionMultiplier;
            bool narrow   = move < atrSafe * AbsorptionAtrFactor;
            if (!highVol || !narrow) return;

            bool bull = Close[0] >= Open[0];
            bool bear = !bull;

            _absUp   = bull && AbsDir != AbsorptionDirection.BearOnly;
            _absDown = bear && AbsDir != AbsorptionDirection.BullOnly;

            _lastAbsorption      = _absUp || _absDown;
            _lastAbsorptionPrice = (High[0] + Low[0]) * 0.5;

            if (!_lastAbsorption) return;

            double volRatio = _avgVol > 0 ? Volume[0] / _avgVol : 1.0;
            double price    = _absUp ? Low[0] - TickSize * 3 : High[0] + TickSize * 3;

            lock (_renderLock)
            {
                _diamonds.Enqueue(new DiamondPrint
                {
                    BarIdx   = CurrentBar,
                    Price    = price,
                    VolRatio = volRatio,
                    IsBull   = _absUp
                });
                if (_diamonds.Count > MaxDiamonds) _diamonds.Dequeue();
            }
        }

        // ── Triple-A state machine (Fabio Valentini) ──────────────────────────
        private void RunTripleAMachine(double atrSafe)
        {
            int timeout = Math.Max(3, TaaPhaseTimeout);

            // LONG machine: Absorption↑ → Accumulation → Aggression↑
            switch (_longPhase)
            {
                case TaaPhase.None:
                    if (_absUp) { _longPhase = TaaPhase.Absorption; _longPhaseBar = CurrentBar; }
                    break;
                case TaaPhase.Absorption:
                    if (CurrentBar - _longPhaseBar > timeout) { _longPhase = TaaPhase.None; break; }
                    bool insideLong = High[0] <= High[1] && Low[0] >= Low[1];
                    bool tightLong  = (High[0] - Low[0]) < atrSafe * 0.6;
                    if (insideLong || tightLong)
                    {
                        _longPhase = TaaPhase.Accumulation; _longPhaseBar = CurrentBar;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_ACC_L_{CurrentBar}", "② Accum", 0,
                                Low[0] - TickSize * 5, ColorTaaLong);
                    }
                    break;
                case TaaPhase.Accumulation:
                    if (CurrentBar - _longPhaseBar > timeout) { _longPhase = TaaPhase.None; break; }
                    if (Close[0] > MAX(High, TripleALookback)[1] && Volume[0] > _avgVol)
                    {
                        Values[0][0] = Low[0] - TickSize * 2;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_AGG_L_{CurrentBar}", "③ Aggr↑", 0,
                                Low[0] - TickSize * 7, ColorTaaLong);
                        _longPhase = TaaPhase.None;
                    }
                    break;
            }

            // SHORT machine: Absorption↓ → Accumulation → Aggression↓
            switch (_shortPhase)
            {
                case TaaPhase.None:
                    if (_absDown) { _shortPhase = TaaPhase.Absorption; _shortPhaseBar = CurrentBar; }
                    break;
                case TaaPhase.Absorption:
                    if (CurrentBar - _shortPhaseBar > timeout) { _shortPhase = TaaPhase.None; break; }
                    bool insideShort = High[0] <= High[1] && Low[0] >= Low[1];
                    bool tightShort  = (High[0] - Low[0]) < atrSafe * 0.6;
                    if (insideShort || tightShort)
                    {
                        _shortPhase = TaaPhase.Accumulation; _shortPhaseBar = CurrentBar;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_ACC_S_{CurrentBar}", "② Accum", 0,
                                High[0] + TickSize * 5, ColorTaaShort);
                    }
                    break;
                case TaaPhase.Accumulation:
                    if (CurrentBar - _shortPhaseBar > timeout) { _shortPhase = TaaPhase.None; break; }
                    if (Close[0] < MIN(Low, TripleALookback)[1] && Volume[0] > _avgVol)
                    {
                        Values[1][0] = High[0] + TickSize * 2;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_AGG_S_{CurrentBar}", "③ Aggr↓", 0,
                                High[0] + TickSize * 7, ColorTaaShort);
                        _shortPhase = TaaPhase.None;
                    }
                    break;
            }
        }

        // ── OnMarketData: real-time per-tick big print detection ──────────────
        protected override void OnMarketData(MarketDataEventArgs md)
        {
            if (md == null) return;
            try
            {
                if (md.MarketDataType == MarketDataType.Bid) { _lastBid = md.Price; return; }
                if (md.MarketDataType == MarketDataType.Ask) { _lastAsk = md.Price; return; }
                if (!ShowBubbles)                            return;
                if (md.MarketDataType != MarketDataType.Last) return;
                if (md.Volume < (long)Math.Max(1, BigPrintSize)) return;
                if (double.IsNaN(md.Price) || md.Price <= 0)    return;
                if (CurrentBar < 0)                              return;

                bool isBuy  = !double.IsNaN(_lastAsk) && md.Price >= _lastAsk;
                bool isSell = !double.IsNaN(_lastBid) && md.Price <= _lastBid;
                if (!isBuy && !isSell) isBuy = true;  // default to buy side if unclassified

                lock (_renderLock)
                {
                    _bubbles.Enqueue(new BubblePrint
                    {
                        BarIdx = CurrentBar,
                        Price  = md.Price,
                        Volume = md.Volume,
                        IsBuy  = isBuy
                    });
                    if (_bubbles.Count > MaxBubbles) _bubbles.Dequeue();
                }
            }
            catch { /* fires ~100x/sec — never throw */ }
        }

        // ── SharpDX rendering ─────────────────────────────────────────────────
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            if (ChartBars == null || RenderTarget == null || IsInHitTest) return;
            if (!ShowBubbles && !EnableAbsorption) return;

            try
            {
                EnsureDxBrushes(RenderTarget);

                BubblePrint[]  bubbles;
                DiamondPrint[] diamonds;
                lock (_renderLock)
                {
                    bubbles  = _bubbles.ToArray();
                    diamonds = _diamonds.ToArray();
                }

                float opacityF = Math.Max(0.1f, Math.Min(1f, BubbleOpacity / 100f));
                if (_dxBull   != null) _dxBull.Opacity   = opacityF;
                if (_dxBear   != null) _dxBear.Opacity   = opacityF;
                if (_dxAbsUp  != null) _dxAbsUp.Opacity  = opacityF;
                if (_dxAbsDown!= null) _dxAbsDown.Opacity= opacityF;
                if (_dxBorder != null) _dxBorder.Opacity = 0.35f;

                // Draw circles (Big Trades)
                if (ShowBubbles)
                {
                    foreach (var b in bubbles)
                    {
                        if (b.BarIdx < ChartBars.FromIndex || b.BarIdx > ChartBars.ToIndex) continue;
                        float r = GetBubbleRadius(b.Volume);
                        float x = chartControl.GetXByBarIndex(ChartBars, b.BarIdx);
                        float y = chartScale.GetYByValue(b.Price);

                        var brush   = b.IsBuy ? _dxBull : _dxBear;
                        var ellipse = new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), r, r);
                        if (brush != null) RenderTarget.FillEllipse(ellipse, brush);
                        if (_dxBorder != null) RenderTarget.DrawEllipse(ellipse, _dxBorder, 1.2f);
                    }
                }

                // Draw diamonds (Absorption)
                if (EnableAbsorption)
                {
                    foreach (var d in diamonds)
                    {
                        if (d.BarIdx < ChartBars.FromIndex || d.BarIdx > ChartBars.ToIndex) continue;
                        float r = GetDiamondRadius(d.VolRatio);
                        float x = chartControl.GetXByBarIndex(ChartBars, d.BarIdx);
                        float y = chartScale.GetYByValue(d.Price);

                        var brush = d.IsBull ? _dxAbsUp : _dxAbsDown;
                        if (brush == null) continue;

                        SharpDX.Direct2D1.PathGeometry geo  = null;
                        SharpDX.Direct2D1.GeometrySink sink = null;
                        try
                        {
                            geo  = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
                            sink = geo.Open();
                            sink.BeginFigure(new SharpDX.Vector2(x,     y - r), SharpDX.Direct2D1.FigureBegin.Filled);
                            sink.AddLine(new SharpDX.Vector2(x + r, y));
                            sink.AddLine(new SharpDX.Vector2(x,     y + r));
                            sink.AddLine(new SharpDX.Vector2(x - r, y));
                            sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                            sink.Close();

                            RenderTarget.FillGeometry(geo, brush);
                            if (_dxBorder != null) RenderTarget.DrawGeometry(geo, _dxBorder, 1.2f);
                        }
                        catch { }
                        finally
                        {
                            sink?.Dispose();
                            geo?.Dispose();
                        }
                    }
                }
            }
            catch { /* render never throws */ }
        }

        // ── Radius helpers ────────────────────────────────────────────────────
        // Log-scale: vol just at BigPrintSize → minR; exceptional vol → BubbleMaxRadius
        private float GetBubbleRadius(long vol)
        {
            const float minR = 5f;
            float maxR = Math.Max(minR + 1, (float)BubbleMaxRadius);
            if (vol <= 0 || BigPrintSize <= 0) return minR;
            double logRatio = Math.Log10(1.0 + (double)vol / BigPrintSize);
            double logMax   = Math.Log10(1.0 + 50.0);  // 50x threshold = max bubble
            float  r        = minR + (float)(Math.Min(logRatio / logMax, 1.0) * (maxR - minR));
            return Math.Max(minR, r);
        }

        // Log-scale: volRatio = vol/avgVol; AbsorptionMultiplier → minR; 5x → DiamondMaxRadius
        private float GetDiamondRadius(double volRatio)
        {
            const float minR = 5f;
            float maxR = Math.Max(minR + 1, (float)DiamondMaxRadius);
            double floor    = Math.Max(1, AbsorptionMultiplier);
            double logRatio = Math.Log10(1.0 + volRatio / floor);
            double logMax   = Math.Log10(1.0 + 5.0);
            float  r        = minR + (float)(Math.Min(logRatio / logMax, 1.0) * (maxR - minR));
            return Math.Max(minR, r);
        }

        // ── DX brush cache ────────────────────────────────────────────────────
        private void EnsureDxBrushes(SharpDX.Direct2D1.RenderTarget rt)
        {
            if (rt == _cachedRt && _dxBull != null) return;
            DisposeDxBrushes();
            _dxBull    = ColorBigBull.ToDxBrush(rt);
            _dxBear    = ColorBigBear.ToDxBrush(rt);
            _dxAbsUp   = ColorAbsUp.ToDxBrush(rt);
            _dxAbsDown = ColorAbsDown.ToDxBrush(rt);
            _dxBorder  = new SharpDX.Direct2D1.SolidColorBrush(rt,
                             new SharpDX.Color4(0f, 0f, 0f, 0.35f));
            _cachedRt  = rt;
        }

        private void DisposeDxBrushes()
        {
            _dxBull?.Dispose();    _dxBull    = null;
            _dxBear?.Dispose();    _dxBear    = null;
            _dxAbsUp?.Dispose();   _dxAbsUp   = null;
            _dxAbsDown?.Dispose(); _dxAbsDown = null;
            _dxBorder?.Dispose();  _dxBorder  = null;
            _cachedRt = null;
        }

        // ── Properties ────────────────────────────────────────────────────────
        #region Properties

        // — Big Trades —
        [NinjaScriptProperty]
        [Display(Name = "Show Bubbles", GroupName = "Big Trades", Order = 1)]
        public bool ShowBubbles { get; set; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Min Print Size (contracts)", GroupName = "Big Trades", Order = 2)]
        public int BigPrintSize { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 20.0)]
        [Display(Name = "Sensitivity (× avg vol)", GroupName = "Big Trades", Order = 3)]
        public double BigTradeMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(10, 80)]
        [Display(Name = "Max Bubble Size (px)", GroupName = "Big Trades", Order = 4)]
        public int BubbleMaxRadius { get; set; }

        [NinjaScriptProperty]
        [Range(10, 100)]
        [Display(Name = "Opacity %", GroupName = "Big Trades", Order = 5)]
        public int BubbleOpacity { get; set; }

        // — Absorption —
        [NinjaScriptProperty]
        [Display(Name = "Enable Absorption", GroupName = "Absorption", Order = 1)]
        public bool EnableAbsorption { get; set; }

        [NinjaScriptProperty]
        [Range(1.0, 15.0)]
        [Display(Name = "Sensitivity (× avg vol)", GroupName = "Absorption", Order = 2)]
        public double AbsorptionMultiplier { get; set; }

        [NinjaScriptProperty]
        [Range(0.05, 1.0)]
        [Display(Name = "Max Range (× ATR)", GroupName = "Absorption", Order = 3)]
        public double AbsorptionAtrFactor { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Direction", GroupName = "Absorption", Order = 4)]
        public AbsorptionDirection AbsDir { get; set; }

        [NinjaScriptProperty]
        [Range(10, 80)]
        [Display(Name = "Max Diamond Size (px)", GroupName = "Absorption", Order = 5)]
        public int DiamondMaxRadius { get; set; }

        // — Triple-A —
        [NinjaScriptProperty]
        [Display(Name = "Enable Triple-A", GroupName = "Triple-A", Order = 1)]
        public bool EnableTripleA { get; set; }

        [NinjaScriptProperty]
        [Range(5, 200)]
        [Display(Name = "Lookback Bars", GroupName = "Triple-A", Order = 2)]
        public int TripleALookback { get; set; }

        [NinjaScriptProperty]
        [Range(3, 50)]
        [Display(Name = "Phase Timeout (bars)", GroupName = "Triple-A", Order = 3)]
        public int TaaPhaseTimeout { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", GroupName = "Triple-A", Order = 4)]
        public bool ShowTaaLabels { get; set; }

        // — Colors —
        [XmlIgnore, Display(Name = "Big Buy",       GroupName = "Colors", Order = 1)]
        public Brush ColorBigBull { get; set; }
        [Browsable(false)]
        public string ColorBigBullSerializable { get => Serialize.BrushToString(ColorBigBull); set => ColorBigBull = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Big Sell",      GroupName = "Colors", Order = 2)]
        public Brush ColorBigBear { get; set; }
        [Browsable(false)]
        public string ColorBigBearSerializable { get => Serialize.BrushToString(ColorBigBear); set => ColorBigBear = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Absorption ↑",  GroupName = "Colors", Order = 3)]
        public Brush ColorAbsUp { get; set; }
        [Browsable(false)]
        public string ColorAbsUpSerializable { get => Serialize.BrushToString(ColorAbsUp); set => ColorAbsUp = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Absorption ↓",  GroupName = "Colors", Order = 4)]
        public Brush ColorAbsDown { get; set; }
        [Browsable(false)]
        public string ColorAbsDownSerializable { get => Serialize.BrushToString(ColorAbsDown); set => ColorAbsDown = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Triple-A Long",  GroupName = "Colors", Order = 5)]
        public Brush ColorTaaLong { get; set; }
        [Browsable(false)]
        public string ColorTaaLongSerializable { get => Serialize.BrushToString(ColorTaaLong); set => ColorTaaLong = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Triple-A Short", GroupName = "Colors", Order = 6)]
        public Brush ColorTaaShort { get; set; }
        [Browsable(false)]
        public string ColorTaaShortSerializable { get => Serialize.BrushToString(ColorTaaShort); set => ColorTaaShort = Serialize.StringToBrush(value); }

        // — Cross-indicator (hidden) —
        [Browsable(false)] public Series<double> PhaseLong           => Values[0];
        [Browsable(false)] public Series<double> PhaseShort          => Values[1];
        [Browsable(false)] public bool   LastAbsorption              => _lastAbsorption;
        [Browsable(false)] public double LastAbsorptionPrice         => _lastAbsorptionPrice;

        #endregion
    }
}
