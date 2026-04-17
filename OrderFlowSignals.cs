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

// Big Trades  : SharpDX variable-size circles, bid/ask classified, optional delta label
// Absorption  : SharpDX variable-size diamonds, volume-ratio proportional
// Triple-A    : Fabio Valentini — Absorption → Accumulation → Aggression
// Signal shape: Triangle / Arrow / Diamond / Dot (user-selectable)

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum AbsorptionDirection { Both, BullOnly, BearOnly }
    public enum SignalMarkerShape   { Triangle, Arrow, Diamond, Dot }

    [Gui.CategoryOrder("Big Trades", 1)]
    [Gui.CategoryOrder("Absorption", 2)]
    [Gui.CategoryOrder("Triple-A",   3)]
    [Gui.CategoryOrder("Colors",     4)]
    public class OrderFlowSignals : Indicator
    {
        // ── Render queues (data-thread → UI-thread) ───────────────────────────
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
            public double VolRatio;
            public bool   IsBull;
        }

        private readonly Queue<BubblePrint>  _bubbles  = new Queue<BubblePrint>();
        private readonly Queue<DiamondPrint> _diamonds = new Queue<DiamondPrint>();
        private readonly object _renderLock = new object();
        private const int MaxBubbles  = 500;
        private const int MaxDiamonds = 200;

        // ── SharpDX resource cache ────────────────────────────────────────────
        private SharpDX.Direct2D1.RenderTarget     _cachedRt;
        private SharpDX.Direct2D1.Brush            _dxBull, _dxBear;
        private SharpDX.Direct2D1.Brush            _dxAbsUp, _dxAbsDown;
        private SharpDX.Direct2D1.Brush            _dxSigLong, _dxSigShort;
        private SharpDX.Direct2D1.Brush            _dxBorder, _dxText;
        private SharpDX.DirectWrite.TextFormat     _textFmt;

        // ── Triple-A state ────────────────────────────────────────────────────
        private enum TaaPhase { None, Absorption, Accumulation, Aggression }
        private TaaPhase _longPhase  = TaaPhase.None;
        private TaaPhase _shortPhase = TaaPhase.None;
        private int      _longPhaseBar, _shortPhaseBar;

        private double _avgVol;
        private bool   _absUp, _absDown;
        private bool   _lastAbsorption;
        private double _lastAbsorptionPrice;

        private double _lastBid = double.NaN;
        private double _lastAsk = double.NaN;

        // ─────────────────────────────────────────────────────────────────────
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name        = "OrderFlowSignals";
                Description = "Big Trades (circles) + Absorption (diamonds) + Triple-A (Fabio Valentini).";
                Calculate   = Calculate.OnEachTick;
                IsOverlay   = true;
                MaximumBarsLookBack      = MaximumBarsLookBack.Infinite;
                IsSuspendedWhileInactive = true;

                ShowBubbles         = true;
                BigPrintSize        = 50;
                BigTradeMultiplier  = 3.0;
                BubbleMaxRadius     = 28;
                BubbleOpacity       = 85;
                ShowBubbleDelta     = false;

                EnableAbsorption     = true;
                AbsorptionMultiplier = 2.5;
                AbsorptionAtrFactor  = 0.30;
                AbsDir               = AbsorptionDirection.Both;
                DiamondMaxRadius     = 22;

                EnableTripleA   = true;
                TripleALookback = 20;
                TaaPhaseTimeout = 10;
                ShowTaaLabels   = true;
                SignalSize      = 14;
                SignalType      = SignalMarkerShape.Arrow;

                ColorBigBull  = Brushes.LimeGreen;
                ColorBigBear  = Brushes.IndianRed;
                ColorAbsUp    = Brushes.DeepSkyBlue;
                ColorAbsDown  = Brushes.Magenta;
                ColorTaaLong  = Brushes.Lime;
                ColorTaaShort = Brushes.OrangeRed;

                // Invisible placeholder plots — only used for cross-indicator Values series
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Dot, "PhaseLong");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Dot, "PhaseShort");
            }
            else if (State == State.DataLoaded)
            {
                lock (_renderLock) { _bubbles.Clear(); _diamonds.Clear(); }
                _longPhase  = TaaPhase.None;
                _shortPhase = TaaPhase.None;
                _lastAbsorption      = false;
                _lastAbsorptionPrice = double.NaN;
            }
            else if (State == State.Terminated)
            {
                DisposeDxResources();
            }
        }

        // ── Bar update: absorption + 3A machine ───────────────────────────────
        protected override void OnBarUpdate()
        {
            int warmup = Math.Max(20, TripleALookback);
            if (CurrentBar < warmup)
            {
                Values[0][0] = Values[1][0] = double.NaN;
                return;
            }

            _avgVol = SMA(Volume, 20)[0];
            double atr     = ATR(14)[0];
            double atrSafe = Math.Max(atr, TickSize);

            _absUp = _absDown = false;
            _lastAbsorption = false;

            if (EnableAbsorption) DetectAbsorption(atrSafe);

            Values[0][0] = Values[1][0] = double.NaN;

            if (EnableTripleA && IsFirstTickOfBar) RunTripleAMachine(atrSafe);
        }

        // ── Absorption detection ──────────────────────────────────────────────
        private void DetectAbsorption(double atrSafe)
        {
            double move  = High[0] - Low[0];
            bool highVol = _avgVol > 0 && Volume[0] > _avgVol * AbsorptionMultiplier;
            bool narrow  = move < atrSafe * AbsorptionAtrFactor;
            if (!highVol || !narrow) return;

            bool bull = Close[0] >= Open[0];

            _absUp   = bull  && AbsDir != AbsorptionDirection.BearOnly;
            _absDown = !bull && AbsDir != AbsorptionDirection.BullOnly;

            _lastAbsorption      = _absUp || _absDown;
            _lastAbsorptionPrice = (High[0] + Low[0]) * 0.5;

            if (!_lastAbsorption) return;

            double volRatio = _avgVol > 0 ? Volume[0] / _avgVol : 1.0;
            double price    = _absUp ? Low[0] - TickSize * 3 : High[0] + TickSize * 3;

            lock (_renderLock)
            {
                _diamonds.Enqueue(new DiamondPrint
                    { BarIdx = CurrentBar, Price = price, VolRatio = volRatio, IsBull = _absUp });
                if (_diamonds.Count > MaxDiamonds) _diamonds.Dequeue();
            }
        }

        // ── Triple-A machine (Fabio Valentini) ────────────────────────────────
        private void RunTripleAMachine(double atrSafe)
        {
            int timeout = Math.Max(3, TaaPhaseTimeout);

            // LONG: Absorption↑ → tight bar → breakout+vol
            switch (_longPhase)
            {
                case TaaPhase.None:
                    if (_absUp) { _longPhase = TaaPhase.Absorption; _longPhaseBar = CurrentBar; }
                    break;
                case TaaPhase.Absorption:
                    if (CurrentBar - _longPhaseBar > timeout) { _longPhase = TaaPhase.None; break; }
                    if ((High[0] <= High[1] && Low[0] >= Low[1]) || (High[0] - Low[0]) < atrSafe * 0.6)
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
                        Values[0][0] = Low[0] - TickSize * 9;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_AGG_L_{CurrentBar}", "③ Aggr↑", 0,
                                Low[0] - TickSize * 13, ColorTaaLong);
                        _longPhase = TaaPhase.None;
                    }
                    break;
            }

            // SHORT: Absorption↓ → tight bar → breakdown+vol
            switch (_shortPhase)
            {
                case TaaPhase.None:
                    if (_absDown) { _shortPhase = TaaPhase.Absorption; _shortPhaseBar = CurrentBar; }
                    break;
                case TaaPhase.Absorption:
                    if (CurrentBar - _shortPhaseBar > timeout) { _shortPhase = TaaPhase.None; break; }
                    if ((High[0] <= High[1] && Low[0] >= Low[1]) || (High[0] - Low[0]) < atrSafe * 0.6)
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
                        Values[1][0] = High[0] + TickSize * 9;
                        if (ShowTaaLabels)
                            Draw.Text(this, $"OFS_AGG_S_{CurrentBar}", "③ Aggr↓", 0,
                                High[0] + TickSize * 13, ColorTaaShort);
                        _shortPhase = TaaPhase.None;
                    }
                    break;
            }
        }

        // ── OnMarketData: tick-level big print detection ──────────────────────
        protected override void OnMarketData(MarketDataEventArgs md)
        {
            if (md == null) return;
            try
            {
                if (md.MarketDataType == MarketDataType.Bid) { _lastBid = md.Price; return; }
                if (md.MarketDataType == MarketDataType.Ask) { _lastAsk = md.Price; return; }
                if (!ShowBubbles || md.MarketDataType != MarketDataType.Last) return;
                if (md.Volume < (long)Math.Max(1, BigPrintSize))              return;
                if (double.IsNaN(md.Price) || md.Price <= 0 || CurrentBar < 0) return;

                bool isBuy  = !double.IsNaN(_lastAsk) && md.Price >= _lastAsk;
                bool isSell = !double.IsNaN(_lastBid) && md.Price <= _lastBid;
                if (!isBuy && !isSell) isBuy = true;

                lock (_renderLock)
                {
                    _bubbles.Enqueue(new BubblePrint
                        { BarIdx = CurrentBar, Price = md.Price, Volume = md.Volume, IsBuy = isBuy });
                    if (_bubbles.Count > MaxBubbles) _bubbles.Dequeue();
                }
            }
            catch { }
        }

        // ── SharpDX render ────────────────────────────────────────────────────
        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);
            if (ChartBars == null || RenderTarget == null || IsInHitTest) return;

            try
            {
                EnsureDxResources(RenderTarget);

                float opF = Math.Max(0.1f, Math.Min(1f, BubbleOpacity / 100f));
                if (_dxBull    != null) _dxBull.Opacity    = opF;
                if (_dxBear    != null) _dxBear.Opacity    = opF;
                if (_dxAbsUp   != null) _dxAbsUp.Opacity   = opF;
                if (_dxAbsDown != null) _dxAbsDown.Opacity = opF;
                if (_dxSigLong != null) _dxSigLong.Opacity = opF;
                if (_dxSigShort!= null) _dxSigShort.Opacity= opF;
                if (_dxBorder  != null) _dxBorder.Opacity  = 0.35f;
                if (_dxText    != null) _dxText.Opacity     = 1f;

                BubblePrint[]  bubbles;
                DiamondPrint[] diamonds;
                lock (_renderLock) { bubbles = _bubbles.ToArray(); diamonds = _diamonds.ToArray(); }

                // ── circles (Big Trades) ──────────────────────────────────────
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
                        if (brush   != null) RenderTarget.FillEllipse(ellipse, brush);
                        if (_dxBorder != null) RenderTarget.DrawEllipse(ellipse, _dxBorder, 1.2f);

                        if (ShowBubbleDelta && _textFmt != null && _dxText != null && r >= 8f)
                        {
                            string lbl  = FormatVolume(b.Volume);
                            float  fs   = Math.Max(7f, Math.Min(r * 0.72f, 16f));
                            DrawCenteredText(lbl, x, y, r * 2f, r * 2f, fs);
                        }
                    }
                }

                // ── diamonds (Absorption) ─────────────────────────────────────
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
                        DrawDiamond(x, y, r, brush);
                    }
                }

                // ── signal markers (Triple-A Aggression) ──────────────────────
                if (EnableTripleA)
                {
                    int sz = Math.Max(6, SignalSize);
                    for (int i = ChartBars.FromIndex; i <= ChartBars.ToIndex; i++)
                    {
                        double pL = Values[0].GetValueAt(i);
                        double pS = Values[1].GetValueAt(i);
                        float  xi = chartControl.GetXByBarIndex(ChartBars, i);

                        if (!double.IsNaN(pL) && pL != 0 && _dxSigLong != null)
                            DrawSignal(xi, chartScale.GetYByValue(pL), sz, true,  _dxSigLong);
                        if (!double.IsNaN(pS) && pS != 0 && _dxSigShort != null)
                            DrawSignal(xi, chartScale.GetYByValue(pS), sz, false, _dxSigShort);
                    }
                }
            }
            catch { }
        }

        // ── Shape draw helpers ────────────────────────────────────────────────
        private void DrawDiamond(float x, float y, float r, SharpDX.Direct2D1.Brush brush)
        {
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
            finally { sink?.Dispose(); geo?.Dispose(); }
        }

        private void DrawSignal(float x, float y, int sz, bool isLong,
                                SharpDX.Direct2D1.Brush brush)
        {
            float s = sz;
            SharpDX.Direct2D1.PathGeometry geo  = null;
            SharpDX.Direct2D1.GeometrySink sink = null;
            try
            {
                geo  = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
                sink = geo.Open();
                float dir = isLong ? -1f : 1f;  // -1 = up (long), +1 = down (short)

                switch (SignalType)
                {
                    case SignalMarkerShape.Triangle:
                        // Simple triangle pointing in direction
                        sink.BeginFigure(new SharpDX.Vector2(x,       y + dir * s),
                                         SharpDX.Direct2D1.FigureBegin.Filled);
                        sink.AddLine(new SharpDX.Vector2(x - s * 0.7f, y - dir * s * 0.3f));
                        sink.AddLine(new SharpDX.Vector2(x + s * 0.7f, y - dir * s * 0.3f));
                        break;

                    case SignalMarkerShape.Arrow:
                        // Arrowhead + stem: tip → wide base → shoulders → stem → stem base
                        float hw = s * 0.65f;  // half-width of arrowhead
                        float sw = s * 0.20f;  // half-width of stem
                        float ah = s * 0.55f;  // arrowhead height
                        float sh = s * 0.60f;  // stem height
                        sink.BeginFigure(new SharpDX.Vector2(x,        y + dir * s),
                                         SharpDX.Direct2D1.FigureBegin.Filled);
                        sink.AddLine(new SharpDX.Vector2(x - hw,       y + dir * (s - ah)));
                        sink.AddLine(new SharpDX.Vector2(x - sw,       y + dir * (s - ah)));
                        sink.AddLine(new SharpDX.Vector2(x - sw,       y - dir * (sh - s)));
                        sink.AddLine(new SharpDX.Vector2(x + sw,       y - dir * (sh - s)));
                        sink.AddLine(new SharpDX.Vector2(x + sw,       y + dir * (s - ah)));
                        sink.AddLine(new SharpDX.Vector2(x + hw,       y + dir * (s - ah)));
                        break;

                    case SignalMarkerShape.Diamond:
                        sink.BeginFigure(new SharpDX.Vector2(x,     y + dir * s),
                                         SharpDX.Direct2D1.FigureBegin.Filled);
                        sink.AddLine(new SharpDX.Vector2(x + s * 0.6f, y));
                        sink.AddLine(new SharpDX.Vector2(x,     y - dir * s));
                        sink.AddLine(new SharpDX.Vector2(x - s * 0.6f, y));
                        break;

                    default: // Dot — drawn separately, skip path
                        sink.BeginFigure(new SharpDX.Vector2(x, y), SharpDX.Direct2D1.FigureBegin.Filled);
                        break;
                }
                sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                sink.Close();

                if (SignalType == SignalMarkerShape.Dot)
                {
                    var el = new SharpDX.Direct2D1.Ellipse(new SharpDX.Vector2(x, y), s * 0.65f, s * 0.65f);
                    RenderTarget.FillEllipse(el, brush);
                    if (_dxBorder != null) RenderTarget.DrawEllipse(el, _dxBorder, 1f);
                }
                else
                {
                    RenderTarget.FillGeometry(geo, brush);
                    if (_dxBorder != null) RenderTarget.DrawGeometry(geo, _dxBorder, 1f);
                }
            }
            catch { }
            finally { sink?.Dispose(); geo?.Dispose(); }
        }

        // ── Delta text inside bubble ──────────────────────────────────────────
        private void DrawCenteredText(string text, float cx, float cy, float w, float h, float fontSize)
        {
            if (_textFmt == null || _dxText == null) return;
            SharpDX.DirectWrite.TextFormat fmt = null;
            try
            {
                fmt = new SharpDX.DirectWrite.TextFormat(
                    Core.Globals.DirectWriteFactory, "Arial", null,
                    SharpDX.DirectWrite.FontWeight.Bold,
                    SharpDX.DirectWrite.FontStyle.Normal,
                    SharpDX.DirectWrite.FontStretch.Normal, fontSize);
                fmt.TextAlignment      = SharpDX.DirectWrite.TextAlignment.Center;
                fmt.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
                var rect = new SharpDX.RectangleF(cx - w / 2f, cy - h / 2f, w, h);
                RenderTarget.DrawText(text, fmt, rect, _dxText);
            }
            catch { }
            finally { fmt?.Dispose(); }
        }

        private static string FormatVolume(long vol)
        {
            if (vol >= 10000) return $"{vol / 1000}K";
            if (vol >= 1000)  return $"{vol / 1000.0:F1}K";
            return vol.ToString();
        }

        // ── Radius helpers ────────────────────────────────────────────────────
        private float GetBubbleRadius(long vol)
        {
            const float minR = 5f;
            float maxR = Math.Max(minR + 1, (float)BubbleMaxRadius);
            if (vol <= 0 || BigPrintSize <= 0) return minR;
            double logRatio = Math.Log10(1.0 + (double)vol / BigPrintSize);
            double logMax   = Math.Log10(51.0);
            return Math.Max(minR, minR + (float)(Math.Min(logRatio / logMax, 1.0) * (maxR - minR)));
        }

        private float GetDiamondRadius(double volRatio)
        {
            const float minR = 5f;
            float maxR = Math.Max(minR + 1, (float)DiamondMaxRadius);
            double floor    = Math.Max(1, AbsorptionMultiplier);
            double logRatio = Math.Log10(1.0 + volRatio / floor);
            double logMax   = Math.Log10(6.0);
            return Math.Max(minR, minR + (float)(Math.Min(logRatio / logMax, 1.0) * (maxR - minR)));
        }

        // ── DX resource cache ─────────────────────────────────────────────────
        private void EnsureDxResources(SharpDX.Direct2D1.RenderTarget rt)
        {
            if (rt == _cachedRt && _dxBull != null) return;
            DisposeDxResources();
            _dxBull     = ColorBigBull.ToDxBrush(rt);
            _dxBear     = ColorBigBear.ToDxBrush(rt);
            _dxAbsUp    = ColorAbsUp.ToDxBrush(rt);
            _dxAbsDown  = ColorAbsDown.ToDxBrush(rt);
            _dxSigLong  = ColorTaaLong.ToDxBrush(rt);
            _dxSigShort = ColorTaaShort.ToDxBrush(rt);
            _dxBorder   = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(0f, 0f, 0f, 0.35f));
            _dxText     = new SharpDX.Direct2D1.SolidColorBrush(rt, new SharpDX.Color4(1f, 1f, 1f, 1f));
            _textFmt    = new SharpDX.DirectWrite.TextFormat(
                Core.Globals.DirectWriteFactory, "Arial", null,
                SharpDX.DirectWrite.FontWeight.Bold,
                SharpDX.DirectWrite.FontStyle.Normal,
                SharpDX.DirectWrite.FontStretch.Normal, 9f);
            _cachedRt = rt;
        }

        private void DisposeDxResources()
        {
            _dxBull?.Dispose();     _dxBull     = null;
            _dxBear?.Dispose();     _dxBear     = null;
            _dxAbsUp?.Dispose();    _dxAbsUp    = null;
            _dxAbsDown?.Dispose();  _dxAbsDown  = null;
            _dxSigLong?.Dispose();  _dxSigLong  = null;
            _dxSigShort?.Dispose(); _dxSigShort = null;
            _dxBorder?.Dispose();   _dxBorder   = null;
            _dxText?.Dispose();     _dxText     = null;
            _textFmt?.Dispose();    _textFmt    = null;
            _cachedRt = null;
        }

        // ── Properties ────────────────────────────────────────────────────────
        #region Properties

        // Big Trades
        [NinjaScriptProperty, Display(Name = "Show Bubbles",               GroupName = "Big Trades", Order = 1)] public bool   ShowBubbles        { get; set; }
        [NinjaScriptProperty, Range(1, int.MaxValue), Display(Name = "Min Print Size (contracts)",   GroupName = "Big Trades", Order = 2)] public int    BigPrintSize       { get; set; }
        [NinjaScriptProperty, Range(1.0, 20.0), Display(Name = "Sensitivity (× avg vol)",           GroupName = "Big Trades", Order = 3)] public double BigTradeMultiplier { get; set; }
        [NinjaScriptProperty, Range(10, 80),    Display(Name = "Max Bubble Size (px)",              GroupName = "Big Trades", Order = 4)] public int    BubbleMaxRadius    { get; set; }
        [NinjaScriptProperty, Range(10, 100),   Display(Name = "Opacity %",                         GroupName = "Big Trades", Order = 5)] public int    BubbleOpacity      { get; set; }
        [NinjaScriptProperty,                   Display(Name = "Show Delta inside Bubble",          GroupName = "Big Trades", Order = 6)] public bool   ShowBubbleDelta    { get; set; }

        // Absorption
        [NinjaScriptProperty, Display(Name = "Enable Absorption",           GroupName = "Absorption", Order = 1)] public bool              EnableAbsorption     { get; set; }
        [NinjaScriptProperty, Range(1.0, 15.0), Display(Name = "Sensitivity (× avg vol)",           GroupName = "Absorption", Order = 2)] public double AbsorptionMultiplier { get; set; }
        [NinjaScriptProperty, Range(0.05, 1.0), Display(Name = "Max Range (× ATR)",                 GroupName = "Absorption", Order = 3)] public double AbsorptionAtrFactor  { get; set; }
        [NinjaScriptProperty, Display(Name = "Direction",                   GroupName = "Absorption", Order = 4)] public AbsorptionDirection AbsDir            { get; set; }
        [NinjaScriptProperty, Range(10, 80),    Display(Name = "Max Diamond Size (px)",             GroupName = "Absorption", Order = 5)] public int    DiamondMaxRadius   { get; set; }

        // Triple-A
        [NinjaScriptProperty, Display(Name = "Enable Triple-A",             GroupName = "Triple-A", Order = 1)] public bool               EnableTripleA   { get; set; }
        [NinjaScriptProperty, Range(5, 200),    Display(Name = "Lookback Bars",                     GroupName = "Triple-A", Order = 2)] public int    TripleALookback { get; set; }
        [NinjaScriptProperty, Range(3, 50),     Display(Name = "Phase Timeout (bars)",              GroupName = "Triple-A", Order = 3)] public int    TaaPhaseTimeout { get; set; }
        [NinjaScriptProperty, Display(Name = "Show Labels",                 GroupName = "Triple-A", Order = 4)] public bool   ShowTaaLabels   { get; set; }
        [NinjaScriptProperty, Range(6, 40),     Display(Name = "Signal Size (px)",                  GroupName = "Triple-A", Order = 5)] public int    SignalSize      { get; set; }
        [NinjaScriptProperty, Display(Name = "Signal Shape",                GroupName = "Triple-A", Order = 6)] public SignalMarkerShape SignalType { get; set; }

        // Colors
        [XmlIgnore, Display(Name = "Big Buy",        GroupName = "Colors", Order = 1)] public Brush ColorBigBull  { get; set; }
        [Browsable(false)] public string ColorBigBullSerializable  { get => Serialize.BrushToString(ColorBigBull);  set => ColorBigBull  = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Big Sell",       GroupName = "Colors", Order = 2)] public Brush ColorBigBear  { get; set; }
        [Browsable(false)] public string ColorBigBearSerializable  { get => Serialize.BrushToString(ColorBigBear);  set => ColorBigBear  = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Absorption ↑",   GroupName = "Colors", Order = 3)] public Brush ColorAbsUp    { get; set; }
        [Browsable(false)] public string ColorAbsUpSerializable    { get => Serialize.BrushToString(ColorAbsUp);    set => ColorAbsUp    = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Absorption ↓",   GroupName = "Colors", Order = 4)] public Brush ColorAbsDown  { get; set; }
        [Browsable(false)] public string ColorAbsDownSerializable  { get => Serialize.BrushToString(ColorAbsDown);  set => ColorAbsDown  = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Triple-A Long",  GroupName = "Colors", Order = 5)] public Brush ColorTaaLong  { get; set; }
        [Browsable(false)] public string ColorTaaLongSerializable  { get => Serialize.BrushToString(ColorTaaLong);  set => ColorTaaLong  = Serialize.StringToBrush(value); }

        [XmlIgnore, Display(Name = "Triple-A Short", GroupName = "Colors", Order = 6)] public Brush ColorTaaShort { get; set; }
        [Browsable(false)] public string ColorTaaShortSerializable { get => Serialize.BrushToString(ColorTaaShort); set => ColorTaaShort = Serialize.StringToBrush(value); }

        // Cross-indicator (hidden)
        [Browsable(false)] public Series<double> PhaseLong           => Values[0];
        [Browsable(false)] public Series<double> PhaseShort          => Values[1];
        [Browsable(false)] public bool   LastAbsorption              => _lastAbsorption;
        [Browsable(false)] public double LastAbsorptionPrice         => _lastAbsorptionPrice;

        #endregion
    }
}
