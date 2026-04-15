// Ultima actualizacion: 2026-04-14

#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class SupportResistance : Indicator
    {
        private struct SRLevel
        {
            public double Price;
            public bool   IsSupport;
            public int    Strength;
            public int    Sweeps;
            public int    StartBar;
            public string Tag;
        }

        private readonly List<SRLevel> _levels = new List<SRLevel>();
        private Swing _swing;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description    = "Support/Resistance zones with strength scoring and sweep tracking.";
                Name           = "Support Resistance";
                Calculate      = Calculate.OnBarClose;
                IsOverlay      = true;
                IsSuspendedWhileInactive = true;
                DrawOnPricePanel = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                Strength        = 5;
                MaxLevels       = 8;
                TouchTolerance  = 3;
                ShowStrength    = true;
                SupportColor    = Brushes.DodgerBlue;
                ResistanceColor = Brushes.Crimson;
            }
            else if (State == State.DataLoaded) { _swing = Swing(Strength); _levels.Clear(); }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < Strength * 2) return;
            double sHigh = _swing.SwingHigh[0];
            double sLow  = _swing.SwingLow[0];
            double tol   = TickSize * TouchTolerance;
            if (sHigh > 0) TryAddLevel(sHigh, false, tol);
            if (sLow  > 0) TryAddLevel(sLow,  true,  tol);
            for (int i = 0; i < _levels.Count; i++)
            {
                var lvl = _levels[i];
                bool nearLevel = Math.Abs(Close[0] - lvl.Price) <= tol;
                if (nearLevel) { lvl.Strength++; _levels[i] = lvl; }
                bool wickThrough = lvl.IsSupport
                    ? Low[0] < lvl.Price - tol && Close[0] > lvl.Price
                    : High[0] > lvl.Price + tol && Close[0] < lvl.Price;
                if (wickThrough) { lvl.Sweeps++; _levels[i] = lvl; }
                DrawLevel(_levels[i], tol);
            }
        }

        private void TryAddLevel(double price, bool isSupport, double tol)
        {
            for (int i = 0; i < _levels.Count; i++)
            {
                if (Math.Abs(_levels[i].Price - price) <= tol)
                {
                    var merged = _levels[i];
                    merged.Price = (merged.Price + price) / 2.0;
                    merged.Strength++;
                    _levels[i] = merged;
                    return;
                }
            }
            if (_levels.Count >= MaxLevels) _levels.RemoveAt(0);
            string tag = (isSupport ? "SUP_" : "RES_") + CurrentBar;
            _levels.Add(new SRLevel { Price = price, IsSupport = isSupport, Strength = 1, Sweeps = 0, StartBar = CurrentBar, Tag = tag });
        }

        private void DrawLevel(SRLevel lvl, double tol)
        {
            Brush brush = lvl.IsSupport ? SupportColor : ResistanceColor;
            int   width = Math.Min(4, 1 + lvl.Strength / 2);
            Draw.HorizontalLine(this, lvl.Tag, lvl.Price, brush, DashStyleHelper.Solid, width);
            if (ShowStrength)
            {
                string label = string.Format("S:{0} Sw:{1}", lvl.Strength, lvl.Sweeps);
                Draw.Text(this, lvl.Tag + "_Lbl", false, 0,
                    lvl.IsSupport ? lvl.Price - tol * 3 : lvl.Price + tol * 3,
                    label, 0, brush,
                    new System.Windows.Media.FontFamily("Arial"), 7,
                    NinjaTrader.Gui.Chart.TextAlignment.Left, false, false, null, 0, 0);
            }
        }

        #region Properties
        [NinjaScriptProperty][Range(2,50)][Display(Name="Swing Strength",GroupName="S/R",Order=0)] public int Strength { get; set; }
        [NinjaScriptProperty][Range(2,30)][Display(Name="Max Levels",GroupName="S/R",Order=1)] public int MaxLevels { get; set; }
        [NinjaScriptProperty][Range(1,20)][Display(Name="Touch Tolerance (ticks)",GroupName="S/R",Order=2)] public int TouchTolerance { get; set; }
        [NinjaScriptProperty][Display(Name="Show Strength Label",GroupName="S/R",Order=3)] public bool ShowStrength { get; set; }
        [XmlIgnore][Display(Name="Support Color",GroupName="S/R",Order=4)] public Brush SupportColor { get; set; }
        [Browsable(false)] public string SupportColorSerializable { get { return Serialize.BrushToString(SupportColor); } set { SupportColor = Serialize.StringToBrush(value); } }
        [XmlIgnore][Display(Name="Resistance Color",GroupName="S/R",Order=5)] public Brush ResistanceColor { get; set; }
        [Browsable(false)] public string ResistanceColorSerializable { get { return Serialize.BrushToString(ResistanceColor); } set { ResistanceColor = Serialize.StringToBrush(value); } }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.
namespace NinjaTrader.NinjaScript.Indicators
{
    public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
    {
        private WyckoffZen.SupportResistance[] cacheSupportResistance;
        public WyckoffZen.SupportResistance SupportResistance(int strength, int maxLevels, int touchTolerance, bool showStrength)
        { return SupportResistance(Input, strength, maxLevels, touchTolerance, showStrength); }
        public WyckoffZen.SupportResistance SupportResistance(ISeries<double> input, int strength, int maxLevels, int touchTolerance, bool showStrength)
        {
            if (cacheSupportResistance != null)
                for (int idx = 0; idx < cacheSupportResistance.Length; idx++)
                    if (cacheSupportResistance[idx] != null && cacheSupportResistance[idx].Strength == strength && cacheSupportResistance[idx].MaxLevels == maxLevels && cacheSupportResistance[idx].TouchTolerance == touchTolerance && cacheSupportResistance[idx].ShowStrength == showStrength && cacheSupportResistance[idx].EqualsInput(input))
                        return cacheSupportResistance[idx];
            return CacheIndicator<WyckoffZen.SupportResistance>(new WyckoffZen.SupportResistance(){ Strength = strength, MaxLevels = maxLevels, TouchTolerance = touchTolerance, ShowStrength = showStrength }, input, ref cacheSupportResistance);
        }
    }
}
namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
    public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
    {
        public Indicators.WyckoffZen.SupportResistance SupportResistance(int strength, int maxLevels, int touchTolerance, bool showStrength)
        { return indicator.SupportResistance(Input, strength, maxLevels, touchTolerance, showStrength); }
    }
}
namespace NinjaTrader.NinjaScript.Strategies
{
    public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
    {
        public Indicators.WyckoffZen.SupportResistance SupportResistance(int strength, int maxLevels, int touchTolerance, bool showStrength)
        { return indicator.SupportResistance(Input, strength, maxLevels, touchTolerance, showStrength); }
    }
}
#endregion
