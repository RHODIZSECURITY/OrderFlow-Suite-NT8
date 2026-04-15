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

// Converted from SMC / ICT Suite Pro Pine Script (BOLLINGER BANDS section)
// Source: SMC_ICT_Suite_Pro_v25_v10.11p_v30c_compile_fix_obstore_valuewhen.pine
// Pine: bb_length=20, bb_maType=SMA, bb_mult=2.0

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
    public enum BbMaType { SMA, EMA, WMA, VWMA }

    [Gui.CategoryOrder("Bollinger Bands Pro", 1)]
    public class BollingerBandsPro : Indicator
    {
        private int      _length;
        private BbMaType _maType;
        private double   _mult;
        private Brush    _basisColor, _upperColor, _lowerColor, _fillColor;

        // internal calculation
        private double _basis, _upper, _lower;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "BollingerBandsPro";
                Description        = "Bollinger Bands with selectable MA type (SMA/EMA/WMA/VWMA). Ported from SMC/ICT Suite Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                DisplayInDataBox   = true;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                _length     = 20;
                _maType     = BbMaType.SMA;
                _mult       = 2.0;
                _basisColor = Brushes.RoyalBlue;
                _upperColor = Brushes.Crimson;
                _lowerColor = Brushes.MediumSeaGreen;
                _fillColor  = new SolidColorBrush(Color.FromArgb(50, 33, 150, 243)); // blue 80% transparent

                AddPlot(new Stroke(Brushes.RoyalBlue,      2), PlotStyle.Line, "Basis");
                AddPlot(new Stroke(Brushes.Crimson,        1), PlotStyle.Line, "Upper");
                AddPlot(new Stroke(Brushes.MediumSeaGreen, 1), PlotStyle.Line, "Lower");
            }
        }

        private double CalcMA(int bar)
        {
            if (CurrentBar < _length - 1) return Close[0];

            switch (_maType)
            {
                case BbMaType.EMA:
                    return EMA(Close, _length)[0];
                case BbMaType.WMA:
                    return WMA(Close, _length)[0];
                case BbMaType.VWMA:
                {
                    double sumPV = 0, sumV = 0;
                    for (int i = 0; i < _length; i++) { sumPV += Close[i] * Volume[i]; sumV += Volume[i]; }
                    return sumV > 0 ? sumPV / sumV : Close[0];
                }
                default: // SMA
                    return SMA(Close, _length)[0];
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < _length - 1) return;

            _basis = CalcMA(CurrentBar);

            double sumSq = 0;
            for (int i = 0; i < _length; i++)
                sumSq += Math.Pow(Close[i] - _basis, 2);
            double stdev = Math.Sqrt(sumSq / _length);

            _upper = _basis + _mult * stdev;
            _lower = _basis - _mult * stdev;

            Values[0][0] = _basis;
            Values[1][0] = _upper;
            Values[2][0] = _lower;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            base.OnRender(chartControl, chartScale);

            // Draw channel fill between Upper and Lower bands
            if (Plots[1].Width > 0 && ChartBars != null)
            {
                var renderTarget = RenderTarget;
                if (renderTarget == null) return;

                var fillBrush = _fillColor.Clone();
                fillBrush.Freeze();
                var dx2Fill = fillBrush.ToDxBrush(renderTarget);

                int firstBar = ChartBars.FromIndex;
                int lastBar  = ChartBars.ToIndex;

                if (firstBar < _length) firstBar = _length;

                for (int i = firstBar; i <= lastBar - 1; i++)
                {
                    int x1 = chartControl.GetXByBarIndex(ChartBars, i);
                    int x2 = chartControl.GetXByBarIndex(ChartBars, i + 1);
                    float y1u = chartScale.GetYByValue(Values[1].GetValueAt(i));
                    float y2u = chartScale.GetYByValue(Values[1].GetValueAt(i + 1));
                    float y1l = chartScale.GetYByValue(Values[2].GetValueAt(i));
                    float y2l = chartScale.GetYByValue(Values[2].GetValueAt(i + 1));

                    var geo = new SharpDX.Direct2D1.PathGeometry(Core.Globals.D2DFactory);
                    var sink = geo.Open();
                    sink.BeginFigure(new SharpDX.Vector2(x1, y1u), SharpDX.Direct2D1.FigureBegin.Filled);
                    sink.AddLine(new SharpDX.Vector2(x2, y2u));
                    sink.AddLine(new SharpDX.Vector2(x2, y2l));
                    sink.AddLine(new SharpDX.Vector2(x1, y1l));
                    sink.EndFigure(SharpDX.Direct2D1.FigureEnd.Closed);
                    sink.Close();

                    renderTarget.FillGeometry(geo, dx2Fill);
                    geo.Dispose();
                }
                dx2Fill.Dispose();
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Period", Order = 1, GroupName = "Bollinger Bands Pro")]
        public int Length { get => _length; set => _length = Math.Max(1, value); }

        [NinjaScriptProperty]
        [Display(Name = "MA Type", Order = 2, GroupName = "Bollinger Bands Pro")]
        public BbMaType MaType { get => _maType; set => _maType = value; }

        [NinjaScriptProperty]
        [Range(0.001, 50)]
        [Display(Name = "StdDev Multiplier", Order = 3, GroupName = "Bollinger Bands Pro")]
        public double Mult { get => _mult; set => _mult = value; }

        [XmlIgnore]
        [Display(Name = "Basis Color", Order = 4, GroupName = "Bollinger Bands Pro")]
        public Brush BasisColor { get => _basisColor; set => _basisColor = value; }

        [Browsable(false)]
        public string BasisColorSerializable
        {
            get { return Serialize.BrushToString(_basisColor); }
            set { _basisColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Upper Color", Order = 5, GroupName = "Bollinger Bands Pro")]
        public Brush UpperColor { get => _upperColor; set => _upperColor = value; }

        [Browsable(false)]
        public string UpperColorSerializable
        {
            get { return Serialize.BrushToString(_upperColor); }
            set { _upperColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Lower Color", Order = 6, GroupName = "Bollinger Bands Pro")]
        public Brush LowerColor { get => _lowerColor; set => _lowerColor = value; }

        [Browsable(false)]
        public string LowerColorSerializable
        {
            get { return Serialize.BrushToString(_lowerColor); }
            set { _lowerColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Fill Color", Order = 7, GroupName = "Bollinger Bands Pro")]
        public Brush FillColor { get => _fillColor; set => _fillColor = value; }

        [Browsable(false)]
        public string FillColorSerializable
        {
            get { return Serialize.BrushToString(_fillColor); }
            set { _fillColor = Serialize.StringToBrush(value); }
        }

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> BasisValues => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> UpperValues => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> LowerValues => Values[2];

        #endregion
    }
}
