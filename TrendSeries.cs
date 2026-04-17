#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
#endregion

// Consolidated EMA/SMA/Bollinger Bands Pro implementation

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
    public enum BbMaType { SMA, EMA, WMA, VWMA }

    [Gui.CategoryOrder("EMA Series",         1)]
    [Gui.CategoryOrder("SMA Series",         2)]
    [Gui.CategoryOrder("Bollinger Bands Pro", 3)]
    public class TrendSeries : Indicator
    {
        // ── EMA ───────────────────────────────────────────────────────────────
        private bool  _ema1Show, _ema2Show, _ema3Show, _ema4Show;
        private int   _ema1Len,  _ema2Len,  _ema3Len,  _ema4Len;
        private Brush _ema1Color, _ema2Color, _ema3Color, _ema4Color;

        // ── SMA ───────────────────────────────────────────────────────────────
        private bool  _sma1Show, _sma2Show, _sma3Show, _sma4Show, _sma5Show;
        private int   _sma1Len,  _sma2Len,  _sma3Len,  _sma4Len,  _sma5Len;
        private Brush _sma1Color, _sma2Color, _sma3Color, _sma4Color, _sma5Color;

        // ── Bollinger Bands ───────────────────────────────────────────────────
        private bool     _bbShow;
        private int      _bbLength;
        private BbMaType _bbMaType;
        private double   _bbMult;
        private Brush    _bbBasisColor, _bbUpperColor, _bbLowerColor, _bbFillColor;
        private double   _bbBasis, _bbUpper, _bbLower;

        // ── NT8 indicator references ──────────────────────────────────────────
        private EMA _ema1, _ema2, _ema3, _ema4;
        private SMA _sma1, _sma2, _sma3, _sma4, _sma5;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "TrendSeries";
                Description        = "EMA Series + SMA Series + Bollinger Bands Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                MaxLookBack = MaximumBarsLookBack.Infinite;
                DisplayInDataBox   = true;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                // EMA defaults (lengths: 9, 20, 50, 200)
                _ema1Show = false; _ema1Len = 9;   _ema1Color = Brushes.Green;
                _ema2Show = true;  _ema2Len = 20;  _ema2Color = Brushes.White;
                _ema3Show = false; _ema3Len = 50;  _ema3Color = Brushes.Orange;
                _ema4Show = false; _ema4Len = 200; _ema4Color = Brushes.LightBlue;

                // SMA defaults (lengths: 50, 100, 150, 200, 600)
                _sma1Show = true;  _sma1Len = 50;  _sma1Color = Brushes.Yellow;
                _sma2Show = false; _sma2Len = 100; _sma2Color = Brushes.Blue;
                _sma3Show = false; _sma3Len = 150; _sma3Color = Brushes.Teal;
                _sma4Show = true;  _sma4Len = 200; _sma4Color = Brushes.DarkRed;
                _sma5Show = false; _sma5Len = 600; _sma5Color = Brushes.Fuchsia;

                // BB defaults (length=20, maType=SMA, mult=2.0)
                _bbShow       = true;
                _bbLength     = 20;
                _bbMaType     = BbMaType.SMA;
                _bbMult       = 2.0;
                _bbBasisColor = Brushes.RoyalBlue;
                _bbUpperColor = Brushes.Crimson;
                _bbLowerColor = Brushes.MediumSeaGreen;
                _bbFillColor  = new SolidColorBrush(Color.FromArgb(50, 33, 150, 243));

                // Plots 0–3: EMA1–EMA4
                AddPlot(new Stroke(Brushes.Green,     3), PlotStyle.Line, "EMA1");
                AddPlot(new Stroke(Brushes.White,     3), PlotStyle.Line, "EMA2");
                AddPlot(new Stroke(Brushes.Orange,    3), PlotStyle.Line, "EMA3");
                AddPlot(new Stroke(Brushes.LightBlue, 3), PlotStyle.Line, "EMA4");
                // Plots 4–8: SMA1–SMA5
                AddPlot(new Stroke(Brushes.Yellow,  3), PlotStyle.Line, "SMA1");
                AddPlot(new Stroke(Brushes.Blue,    3), PlotStyle.Line, "SMA2");
                AddPlot(new Stroke(Brushes.Teal,    3), PlotStyle.Line, "SMA3");
                AddPlot(new Stroke(Brushes.DarkRed, 3), PlotStyle.Line, "SMA4");
                AddPlot(new Stroke(Brushes.Fuchsia, 3), PlotStyle.Line, "SMA5");
                // Plots 9–11: BB Basis, Upper, Lower
                AddPlot(new Stroke(Brushes.RoyalBlue,      2), PlotStyle.Line, "BB_Basis");
                AddPlot(new Stroke(Brushes.Crimson,        1), PlotStyle.Line, "BB_Upper");
                AddPlot(new Stroke(Brushes.MediumSeaGreen, 1), PlotStyle.Line, "BB_Lower");
            }
            else if (State == State.Configure)
            {
                _ema1 = EMA(Close, _ema1Len);
                _ema2 = EMA(Close, _ema2Len);
                _ema3 = EMA(Close, _ema3Len);
                _ema4 = EMA(Close, _ema4Len);

                _sma1 = SMA(Close, _sma1Len);
                _sma2 = SMA(Close, _sma2Len);
                _sma3 = SMA(Close, _sma3Len);
                _sma4 = SMA(Close, _sma4Len);
                _sma5 = SMA(Close, _sma5Len);
            }
        }

        // ── BB: MA selector (support: SMA, EMA, WMA, VWMA) ────────
        private double CalcBbMA()
        {
            if (CurrentBar < _bbLength - 1) return Close[0];
            switch (_bbMaType)
            {
                case BbMaType.EMA:  return EMA(Close, _bbLength)[0];
                case BbMaType.WMA:  return WMA(Close, _bbLength)[0];
                case BbMaType.VWMA:
                {
                    double sumPV = 0, sumV = 0;
                    for (int i = 0; i < _bbLength; i++) { sumPV += Close[i] * Volume[i]; sumV += Volume[i]; }
                    return sumV > 0 ? sumPV / sumV : Close[0];
                }
                default: return SMA(Close, _bbLength)[0]; // SMA
            }
        }

        protected override void OnBarUpdate()
        {
            // ── EMA plots ─────────────────────────────────────────────────────
            Values[0][0] = _ema1Show ? _ema1[0] : double.NaN;
            Values[1][0] = _ema2Show ? _ema2[0] : double.NaN;
            Values[2][0] = _ema3Show ? _ema3[0] : double.NaN;
            Values[3][0] = _ema4Show ? _ema4[0] : double.NaN;

            // ── SMA plots ─────────────────────────────────────────────────────
            Values[4][0] = _sma1Show ? _sma1[0] : double.NaN;
            Values[5][0] = _sma2Show ? _sma2[0] : double.NaN;
            Values[6][0] = _sma3Show ? _sma3[0] : double.NaN;
            Values[7][0] = _sma4Show ? _sma4[0] : double.NaN;
            Values[8][0] = _sma5Show ? _sma5[0] : double.NaN;

            // ── Bollinger Bands ───────────────────────────────────────────────
            if (!_bbShow || CurrentBar < _bbLength - 1)
            {
                Values[9][0] = Values[10][0] = Values[11][0] = double.NaN;
                return;
            }

            _bbBasis = CalcBbMA();
            if (double.IsNaN(_bbBasis)) { Values[9][0] = Values[10][0] = Values[11][0] = double.NaN; return; }

            double sumSq = 0;
            for (int i = 0; i < _bbLength; i++)
            {
                if (double.IsNaN(Close[i])) { Values[9][0] = Values[10][0] = Values[11][0] = double.NaN; return; }
                sumSq += Math.Pow(Close[i] - _bbBasis, 2);
            }
            double stdev = Math.Sqrt(Math.Max(0, sumSq / Math.Max(1, _bbLength - 1)));

            _bbUpper = _bbBasis + _bbMult * stdev;
            _bbLower = _bbBasis - _bbMult * stdev;

            Values[9][0]  = _bbBasis;
            Values[10][0] = _bbUpper;
            Values[11][0] = _bbLower;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            if (!_bbShow || Plots[10].Width <= 0 || ChartBars == null) return;

            var renderTarget = RenderTarget;
            if (renderTarget == null) return;

            SharpDX.Direct2D1.Brush dx2Fill = null;
            try
            {
                var fillBrush = _bbFillColor.Clone();
                fillBrush.Freeze();
                dx2Fill = fillBrush.ToDxBrush(renderTarget);

                int firstBar = Math.Max(ChartBars.FromIndex, _bbLength);
                int lastBar  = ChartBars.ToIndex;

                for (int i = firstBar; i <= lastBar - 1; i++)
                {
                    double u1 = Values[10].GetValueAt(i);
                    double u2 = Values[10].GetValueAt(i + 1);
                    double l1 = Values[11].GetValueAt(i);
                    double l2 = Values[11].GetValueAt(i + 1);
                    if (double.IsNaN(u1) || double.IsNaN(u2) || double.IsNaN(l1) || double.IsNaN(l2)) continue;

                    SharpDX.Direct2D1.PathGeometry geo = null;
                    SharpDX.Direct2D1.GeometrySink sink = null;
                    try
                    {
                        int   x1  = chartControl.GetXByBarIndex(ChartBars, i);
                        int   x2  = chartControl.GetXByBarIndex(ChartBars, i + 1);
                        float y1u = chartScale.GetYByValue(u1);
                        float y2u = chartScale.GetYByValue(u2);
                        float y1l = chartScale.GetYByValue(l1);
                        float y2l = chartScale.GetYByValue(l2);

                        geo  = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
                        sink = geo.Open();
                        sink.BeginFigure(new SharpDX.Vector2(x1, y1u), SharpDX.Direct2D1.FigureBegin.Filled);
                        sink.AddLine(new SharpDX.Vector2(x2, y2u));
                        sink.AddLine(new SharpDX.Vector2(x2, y2l));
                        sink.AddLine(new SharpDX.Vector2(x1, y1l));
                        sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                        sink.Close();

                        renderTarget.FillGeometry(geo, dx2Fill);
                    }
                    catch { /* skip malformed segment, keep chart alive */ }
                    finally
                    {
                        if (sink != null) sink.Dispose();
                        if (geo  != null) geo.Dispose();
                    }
                }
            }
            catch { /* render never throws — chart must stay alive */ }
            finally
            {
                if (dx2Fill != null) dx2Fill.Dispose();
            }
        }

        #region Properties

        // ── EMA Series ────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show EMA1", Order = 1, GroupName = "EMA Series")]
        public bool Ema1Show { get => _ema1Show; set => _ema1Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "EMA1 Period", Order = 2, GroupName = "EMA Series")]
        public int Ema1Len { get => _ema1Len; set => _ema1Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "EMA1 Color", Order = 3, GroupName = "EMA Series")]
        public Brush Ema1Color { get => _ema1Color; set => _ema1Color = value; }

        [Browsable(false)]
        public string Ema1ColorSerializable
        { get => Serialize.BrushToString(_ema1Color); set => _ema1Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA2", Order = 4, GroupName = "EMA Series")]
        public bool Ema2Show { get => _ema2Show; set => _ema2Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "EMA2 Period", Order = 5, GroupName = "EMA Series")]
        public int Ema2Len { get => _ema2Len; set => _ema2Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "EMA2 Color", Order = 6, GroupName = "EMA Series")]
        public Brush Ema2Color { get => _ema2Color; set => _ema2Color = value; }

        [Browsable(false)]
        public string Ema2ColorSerializable
        { get => Serialize.BrushToString(_ema2Color); set => _ema2Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA3", Order = 7, GroupName = "EMA Series")]
        public bool Ema3Show { get => _ema3Show; set => _ema3Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "EMA3 Period", Order = 8, GroupName = "EMA Series")]
        public int Ema3Len { get => _ema3Len; set => _ema3Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "EMA3 Color", Order = 9, GroupName = "EMA Series")]
        public Brush Ema3Color { get => _ema3Color; set => _ema3Color = value; }

        [Browsable(false)]
        public string Ema3ColorSerializable
        { get => Serialize.BrushToString(_ema3Color); set => _ema3Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA4", Order = 10, GroupName = "EMA Series")]
        public bool Ema4Show { get => _ema4Show; set => _ema4Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "EMA4 Period", Order = 11, GroupName = "EMA Series")]
        public int Ema4Len { get => _ema4Len; set => _ema4Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "EMA4 Color", Order = 12, GroupName = "EMA Series")]
        public Brush Ema4Color { get => _ema4Color; set => _ema4Color = value; }

        [Browsable(false)]
        public string Ema4ColorSerializable
        { get => Serialize.BrushToString(_ema4Color); set => _ema4Color = Serialize.StringToBrush(value); }

        // ── SMA Series ────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show SMA1", Order = 1, GroupName = "SMA Series")]
        public bool Sma1Show { get => _sma1Show; set => _sma1Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "SMA1 Period", Order = 2, GroupName = "SMA Series")]
        public int Sma1Len { get => _sma1Len; set => _sma1Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "SMA1 Color", Order = 3, GroupName = "SMA Series")]
        public Brush Sma1Color { get => _sma1Color; set => _sma1Color = value; }

        [Browsable(false)]
        public string Sma1ColorSerializable
        { get => Serialize.BrushToString(_sma1Color); set => _sma1Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA2", Order = 4, GroupName = "SMA Series")]
        public bool Sma2Show { get => _sma2Show; set => _sma2Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "SMA2 Period", Order = 5, GroupName = "SMA Series")]
        public int Sma2Len { get => _sma2Len; set => _sma2Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "SMA2 Color", Order = 6, GroupName = "SMA Series")]
        public Brush Sma2Color { get => _sma2Color; set => _sma2Color = value; }

        [Browsable(false)]
        public string Sma2ColorSerializable
        { get => Serialize.BrushToString(_sma2Color); set => _sma2Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA3", Order = 7, GroupName = "SMA Series")]
        public bool Sma3Show { get => _sma3Show; set => _sma3Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "SMA3 Period", Order = 8, GroupName = "SMA Series")]
        public int Sma3Len { get => _sma3Len; set => _sma3Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "SMA3 Color", Order = 9, GroupName = "SMA Series")]
        public Brush Sma3Color { get => _sma3Color; set => _sma3Color = value; }

        [Browsable(false)]
        public string Sma3ColorSerializable
        { get => Serialize.BrushToString(_sma3Color); set => _sma3Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA4", Order = 10, GroupName = "SMA Series")]
        public bool Sma4Show { get => _sma4Show; set => _sma4Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "SMA4 Period", Order = 11, GroupName = "SMA Series")]
        public int Sma4Len { get => _sma4Len; set => _sma4Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "SMA4 Color", Order = 12, GroupName = "SMA Series")]
        public Brush Sma4Color { get => _sma4Color; set => _sma4Color = value; }

        [Browsable(false)]
        public string Sma4ColorSerializable
        { get => Serialize.BrushToString(_sma4Color); set => _sma4Color = Serialize.StringToBrush(value); }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA5", Order = 13, GroupName = "SMA Series")]
        public bool Sma5Show { get => _sma5Show; set => _sma5Show = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "SMA5 Period", Order = 14, GroupName = "SMA Series")]
        public int Sma5Len { get => _sma5Len; set => _sma5Len = Math.Max(1, Math.Min(5000, value)); }

        [XmlIgnore]
        [Display(Name = "SMA5 Color", Order = 15, GroupName = "SMA Series")]
        public Brush Sma5Color { get => _sma5Color; set => _sma5Color = value; }

        [Browsable(false)]
        public string Sma5ColorSerializable
        { get => Serialize.BrushToString(_sma5Color); set => _sma5Color = Serialize.StringToBrush(value); }

        // ── Bollinger Bands Pro ───────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show Bollinger Bands", Order = 1, GroupName = "Bollinger Bands Pro")]
        public bool BbShow { get => _bbShow; set => _bbShow = value; }

        [NinjaScriptProperty]
        [Range(1, 5000)]
        [Display(Name = "Period", Order = 2, GroupName = "Bollinger Bands Pro")]
        public int BbLength { get => _bbLength; set => _bbLength = Math.Max(1, Math.Min(5000, value)); }

        [NinjaScriptProperty]
        [Display(Name = "MA Type", Order = 3, GroupName = "Bollinger Bands Pro")]
        public BbMaType BbMaType { get => _bbMaType; set => _bbMaType = value; }

        [NinjaScriptProperty]
        [Range(0.001, 50)]
        [Display(Name = "StdDev Multiplier", Order = 4, GroupName = "Bollinger Bands Pro")]
        public double BbMult { get => _bbMult; set => _bbMult = value; }

        [XmlIgnore]
        [Display(Name = "Basis Color", Order = 5, GroupName = "Bollinger Bands Pro")]
        public Brush BbBasisColor { get => _bbBasisColor; set => _bbBasisColor = value; }

        [Browsable(false)]
        public string BbBasisColorSerializable
        { get => Serialize.BrushToString(_bbBasisColor); set => _bbBasisColor = Serialize.StringToBrush(value); }

        [XmlIgnore]
        [Display(Name = "Upper Color", Order = 6, GroupName = "Bollinger Bands Pro")]
        public Brush BbUpperColor { get => _bbUpperColor; set => _bbUpperColor = value; }

        [Browsable(false)]
        public string BbUpperColorSerializable
        { get => Serialize.BrushToString(_bbUpperColor); set => _bbUpperColor = Serialize.StringToBrush(value); }

        [XmlIgnore]
        [Display(Name = "Lower Color", Order = 7, GroupName = "Bollinger Bands Pro")]
        public Brush BbLowerColor { get => _bbLowerColor; set => _bbLowerColor = value; }

        [Browsable(false)]
        public string BbLowerColorSerializable
        { get => Serialize.BrushToString(_bbLowerColor); set => _bbLowerColor = Serialize.StringToBrush(value); }

        [XmlIgnore]
        [Display(Name = "Fill Color", Order = 8, GroupName = "Bollinger Bands Pro")]
        public Brush BbFillColor { get => _bbFillColor; set => _bbFillColor = value; }

        [Browsable(false)]
        public string BbFillColorSerializable
        { get => Serialize.BrushToString(_bbFillColor); set => _bbFillColor = Serialize.StringToBrush(value); }

        // ── Plot accessors ────────────────────────────────────────────────────
        [Browsable(false)] [XmlIgnore] public Series<double> Ema1Values  => Values[0];
        [Browsable(false)] [XmlIgnore] public Series<double> Ema2Values  => Values[1];
        [Browsable(false)] [XmlIgnore] public Series<double> Ema3Values  => Values[2];
        [Browsable(false)] [XmlIgnore] public Series<double> Ema4Values  => Values[3];
        [Browsable(false)] [XmlIgnore] public Series<double> Sma1Values  => Values[4];
        [Browsable(false)] [XmlIgnore] public Series<double> Sma2Values  => Values[5];
        [Browsable(false)] [XmlIgnore] public Series<double> Sma3Values  => Values[6];
        [Browsable(false)] [XmlIgnore] public Series<double> Sma4Values  => Values[7];
        [Browsable(false)] [XmlIgnore] public Series<double> Sma5Values  => Values[8];
        [Browsable(false)] [XmlIgnore] public Series<double> BbBasis     => Values[9];
        [Browsable(false)] [XmlIgnore] public Series<double> BbUpper     => Values[10];
        [Browsable(false)] [XmlIgnore] public Series<double> BbLower     => Values[11];

        #endregion
    }
}
