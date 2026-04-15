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

// Converted from SMC / ICT Suite Pro Pine Script (EMA SERIES + SMA SERIES sections)
// Source: SMC_ICT_Suite_Pro_v25_v10.11p_v30c_compile_fix_obstore_valuewhen.pine

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
    [Gui.CategoryOrder("EMA Series", 1)]
    [Gui.CategoryOrder("SMA Series", 2)]
    public class MASeries : Indicator
    {
        // ── EMA ──────────────────────────────────────────────────────────────
        private bool   _ema1Show, _ema2Show, _ema3Show, _ema4Show;
        private int    _ema1Len,  _ema2Len,  _ema3Len,  _ema4Len;
        private Brush  _ema1Color, _ema2Color, _ema3Color, _ema4Color;

        // ── SMA ──────────────────────────────────────────────────────────────
        private bool   _sma1Show, _sma2Show, _sma3Show, _sma4Show, _sma5Show;
        private int    _sma1Len,  _sma2Len,  _sma3Len,  _sma4Len,  _sma5Len;
        private Brush  _sma1Color, _sma2Color, _sma3Color, _sma4Color, _sma5Color;

        // ── NT8 EMA/SMA indicator references ─────────────────────────────────
        private EMA _ema1, _ema2, _ema3, _ema4;
        private SMA _sma1, _sma2, _sma3, _sma4, _sma5;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name                = "MASeries";
                Description         = "4 EMAs + 5 SMAs fully configurable. Ported from SMC/ICT Suite Pro Pine Script.";
                Calculate           = Calculate.OnBarClose;
                IsOverlay           = true;
                DisplayInDataBox    = true;
                DrawOnPricePanel    = true;
                ScaleJustification  = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                // EMA defaults (Pine: 9 off, 20 on, 50 off, 200 off)
                _ema1Show  = false; _ema1Len = 9;   _ema1Color = Brushes.Green;
                _ema2Show  = true;  _ema2Len = 20;  _ema2Color = Brushes.White;
                _ema3Show  = false; _ema3Len = 50;  _ema3Color = Brushes.Orange;
                _ema4Show  = false; _ema4Len = 200; _ema4Color = Brushes.LightBlue;

                // SMA defaults (Pine: 50 on, 100 off, 150 off, 200 on, 600 off)
                _sma1Show  = true;  _sma1Len = 50;  _sma1Color = Brushes.Yellow;
                _sma2Show  = false; _sma2Len = 100; _sma2Color = Brushes.Blue;
                _sma3Show  = false; _sma3Len = 150; _sma3Color = Brushes.Teal;
                _sma4Show  = true;  _sma4Len = 200; _sma4Color = Brushes.DarkRed;
                _sma5Show  = false; _sma5Len = 600; _sma5Color = Brushes.Fuchsia;

                AddPlot(new Stroke(Brushes.Green,     3), PlotStyle.Line, "EMA1");
                AddPlot(new Stroke(Brushes.White,     3), PlotStyle.Line, "EMA2");
                AddPlot(new Stroke(Brushes.Orange,    3), PlotStyle.Line, "EMA3");
                AddPlot(new Stroke(Brushes.LightBlue, 3), PlotStyle.Line, "EMA4");

                AddPlot(new Stroke(Brushes.Yellow,  3), PlotStyle.Line, "SMA1");
                AddPlot(new Stroke(Brushes.Blue,    3), PlotStyle.Line, "SMA2");
                AddPlot(new Stroke(Brushes.Teal,    3), PlotStyle.Line, "SMA3");
                AddPlot(new Stroke(Brushes.DarkRed, 3), PlotStyle.Line, "SMA4");
                AddPlot(new Stroke(Brushes.Fuchsia, 3), PlotStyle.Line, "SMA5");
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

        protected override void OnBarUpdate()
        {
            // EMA plots — hidden when disabled
            Values[0][0] = _ema1Show ? _ema1[0] : double.NaN;
            Values[1][0] = _ema2Show ? _ema2[0] : double.NaN;
            Values[2][0] = _ema3Show ? _ema3[0] : double.NaN;
            Values[3][0] = _ema4Show ? _ema4[0] : double.NaN;

            // SMA plots
            Values[4][0] = _sma1Show ? _sma1[0] : double.NaN;
            Values[5][0] = _sma2Show ? _sma2[0] : double.NaN;
            Values[6][0] = _sma3Show ? _sma3[0] : double.NaN;
            Values[7][0] = _sma4Show ? _sma4[0] : double.NaN;
            Values[8][0] = _sma5Show ? _sma5[0] : double.NaN;
        }

        #region Properties

        // ── EMA ──────────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show EMA1", Order = 1, GroupName = "EMA Series")]
        public bool Ema1Show { get => _ema1Show; set => _ema1Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA1 Period", Order = 2, GroupName = "EMA Series")]
        public int Ema1Len { get => _ema1Len; set => _ema1Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "EMA1 Color", Order = 3, GroupName = "EMA Series")]
        public Brush Ema1Color { get => _ema1Color; set => _ema1Color = value; }

        [Browsable(false)]
        public string Ema1ColorSerializable
        {
            get { return Serialize.BrushToString(_ema1Color); }
            set { _ema1Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA2", Order = 4, GroupName = "EMA Series")]
        public bool Ema2Show { get => _ema2Show; set => _ema2Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA2 Period", Order = 5, GroupName = "EMA Series")]
        public int Ema2Len { get => _ema2Len; set => _ema2Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "EMA2 Color", Order = 6, GroupName = "EMA Series")]
        public Brush Ema2Color { get => _ema2Color; set => _ema2Color = value; }

        [Browsable(false)]
        public string Ema2ColorSerializable
        {
            get { return Serialize.BrushToString(_ema2Color); }
            set { _ema2Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA3", Order = 7, GroupName = "EMA Series")]
        public bool Ema3Show { get => _ema3Show; set => _ema3Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA3 Period", Order = 8, GroupName = "EMA Series")]
        public int Ema3Len { get => _ema3Len; set => _ema3Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "EMA3 Color", Order = 9, GroupName = "EMA Series")]
        public Brush Ema3Color { get => _ema3Color; set => _ema3Color = value; }

        [Browsable(false)]
        public string Ema3ColorSerializable
        {
            get { return Serialize.BrushToString(_ema3Color); }
            set { _ema3Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show EMA4", Order = 10, GroupName = "EMA Series")]
        public bool Ema4Show { get => _ema4Show; set => _ema4Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "EMA4 Period", Order = 11, GroupName = "EMA Series")]
        public int Ema4Len { get => _ema4Len; set => _ema4Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "EMA4 Color", Order = 12, GroupName = "EMA Series")]
        public Brush Ema4Color { get => _ema4Color; set => _ema4Color = value; }

        [Browsable(false)]
        public string Ema4ColorSerializable
        {
            get { return Serialize.BrushToString(_ema4Color); }
            set { _ema4Color = Serialize.StringToBrush(value); }
        }

        // ── SMA ──────────────────────────────────────────────────────────────
        [NinjaScriptProperty]
        [Display(Name = "Show SMA1", Order = 1, GroupName = "SMA Series")]
        public bool Sma1Show { get => _sma1Show; set => _sma1Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SMA1 Period", Order = 2, GroupName = "SMA Series")]
        public int Sma1Len { get => _sma1Len; set => _sma1Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "SMA1 Color", Order = 3, GroupName = "SMA Series")]
        public Brush Sma1Color { get => _sma1Color; set => _sma1Color = value; }

        [Browsable(false)]
        public string Sma1ColorSerializable
        {
            get { return Serialize.BrushToString(_sma1Color); }
            set { _sma1Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA2", Order = 4, GroupName = "SMA Series")]
        public bool Sma2Show { get => _sma2Show; set => _sma2Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SMA2 Period", Order = 5, GroupName = "SMA Series")]
        public int Sma2Len { get => _sma2Len; set => _sma2Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "SMA2 Color", Order = 6, GroupName = "SMA Series")]
        public Brush Sma2Color { get => _sma2Color; set => _sma2Color = value; }

        [Browsable(false)]
        public string Sma2ColorSerializable
        {
            get { return Serialize.BrushToString(_sma2Color); }
            set { _sma2Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA3", Order = 7, GroupName = "SMA Series")]
        public bool Sma3Show { get => _sma3Show; set => _sma3Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SMA3 Period", Order = 8, GroupName = "SMA Series")]
        public int Sma3Len { get => _sma3Len; set => _sma3Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "SMA3 Color", Order = 9, GroupName = "SMA Series")]
        public Brush Sma3Color { get => _sma3Color; set => _sma3Color = value; }

        [Browsable(false)]
        public string Sma3ColorSerializable
        {
            get { return Serialize.BrushToString(_sma3Color); }
            set { _sma3Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA4", Order = 10, GroupName = "SMA Series")]
        public bool Sma4Show { get => _sma4Show; set => _sma4Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SMA4 Period", Order = 11, GroupName = "SMA Series")]
        public int Sma4Len { get => _sma4Len; set => _sma4Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "SMA4 Color", Order = 12, GroupName = "SMA Series")]
        public Brush Sma4Color { get => _sma4Color; set => _sma4Color = value; }

        [Browsable(false)]
        public string Sma4ColorSerializable
        {
            get { return Serialize.BrushToString(_sma4Color); }
            set { _sma4Color = Serialize.StringToBrush(value); }
        }

        [NinjaScriptProperty]
        [Display(Name = "Show SMA5", Order = 13, GroupName = "SMA Series")]
        public bool Sma5Show { get => _sma5Show; set => _sma5Show = value; }

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "SMA5 Period", Order = 14, GroupName = "SMA Series")]
        public int Sma5Len { get => _sma5Len; set => _sma5Len = Math.Max(1, value); }

        [XmlIgnore]
        [Display(Name = "SMA5 Color", Order = 15, GroupName = "SMA Series")]
        public Brush Sma5Color { get => _sma5Color; set => _sma5Color = value; }

        [Browsable(false)]
        public string Sma5ColorSerializable
        {
            get { return Serialize.BrushToString(_sma5Color); }
            set { _sma5Color = Serialize.StringToBrush(value); }
        }

        // ── Plot accessors ────────────────────────────────────────────────────
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Ema1Values => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Ema2Values => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Ema3Values => Values[2];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Ema4Values => Values[3];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Sma1Values => Values[4];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Sma2Values => Values[5];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Sma3Values => Values[6];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Sma4Values => Values[7];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Sma5Values => Values[8];

        #endregion
    }
}
