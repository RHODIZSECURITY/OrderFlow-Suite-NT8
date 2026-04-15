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

// Converted from SMC / ICT Suite Pro Pine Script
// Section: PREMIUM / DISCOUNT ZONES (lines 4453-4530)
// Based on trailing swing high/low range divided into:
//   Premium  = top 5%   (above equilibrium — supply zone, sell opportunities)
//   Equilibrium = 47.5-52.5% (midpoint — balanced)
//   Discount = bottom 5% (below equilibrium — demand zone, buy opportunities)
// Uses Fibonacci logic: 0.5 = equilibrium, >0.5 = premium, <0.5 = discount

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
    public class PremiumDiscountZones : Indicator
    {
        // ── Swing tracking ────────────────────────────────────────────────────
        private double _swingTop    = double.NaN;
        private double _swingBottom = double.NaN;

        // ── Zone levels (updated each bar) ────────────────────────────────────
        private double _premiumTop, _premiumBottom;
        private double _eqTop, _eqBottom;
        private double _discountTop, _discountBottom;

        // ── Settings ──────────────────────────────────────────────────────────
        private int  _swingLen;
        private bool _showLabels;
        private Brush _premiumColor, _eqColor, _discountColor;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Name               = "PremiumDiscountZones";
                Description        = "Premium / Equilibrium / Discount zones based on trailing swing range. Ported from SMC/ICT Suite Pro.";
                Calculate          = Calculate.OnBarClose;
                IsOverlay          = true;
                DisplayInDataBox   = false;
                DrawOnPricePanel   = true;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;

                _swingLen     = 10;
                _showLabels   = true;

                // Pine defaults: premium=#F23645 (red), eq=gray, discount=#089981 (teal)
                _premiumColor  = new SolidColorBrush(Color.FromArgb(55, 242,  54,  69));  // Red transparent
                _eqColor       = new SolidColorBrush(Color.FromArgb(40, 150, 150, 150));  // Gray transparent
                _discountColor = new SolidColorBrush(Color.FromArgb(55,   8, 153, 129));  // Teal transparent

                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "Equilibrium");
            }
            else if (State == State.DataLoaded)
            {
                _swingTop = double.NaN; _swingBottom = double.NaN;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < _swingLen * 2 + 1) return;

            // ── Track swing high/low for trailing range ───────────────────────
            double ph = double.NaN;
            bool isPH = true;
            double pivH = High[_swingLen];
            for (int i = 0; i < _swingLen * 2 + 1; i++)
            {
                if (i == _swingLen) continue;
                if (High[i] >= pivH) { isPH = false; break; }
            }
            if (isPH) ph = pivH;

            double pl = double.NaN;
            bool isPL = true;
            double pivL = Low[_swingLen];
            for (int i = 0; i < _swingLen * 2 + 1; i++)
            {
                if (i == _swingLen) continue;
                if (Low[i] <= pivL) { isPL = false; break; }
            }
            if (isPL) pl = pivL;

            if (!double.IsNaN(ph)) _swingTop    = ph;
            if (!double.IsNaN(pl)) _swingBottom = pl;

            if (double.IsNaN(_swingTop) || double.IsNaN(_swingBottom) || _swingTop <= _swingBottom)
                return;

            double top = _swingTop, bot = _swingBottom;

            // Pine: premiumBottom = 0.95*top + 0.05*bot  (top 5% zone)
            //       eqTop    = 0.525*top + 0.475*bot
            //       eqBottom = 0.525*bot + 0.475*top (= 0.475*top + 0.525*bot)
            //       discountTop = 0.95*bot + 0.05*top  (bottom 5% zone)
            _premiumTop    = top;
            _premiumBottom = 0.95 * top  + 0.05 * bot;
            _eqTop         = 0.525 * top + 0.475 * bot;
            _eqBottom      = 0.475 * top + 0.525 * bot;
            _discountTop   = 0.05 * top  + 0.95 * bot;
            _discountBottom = bot;

            double equilibrium = (top + bot) / 2.0;
            Values[0][0] = equilibrium;

            // ── Draw zones (replace on each bar) ──────────────────────────────
            Draw.Rectangle(this, "PremZone", false, _swingLen + 1, _premiumTop, 0, _premiumBottom,
                _premiumColor, _premiumColor, 0);
            Draw.Rectangle(this, "EqZone",   false, _swingLen + 1, _eqTop,      0, _eqBottom,
                _eqColor, _eqColor, 0);
            Draw.Rectangle(this, "DiscZone", false, _swingLen + 1, _discountTop, 0, _discountBottom,
                _discountColor, _discountColor, 0);

            Draw.HorizontalLine(this, "Equilibrium", equilibrium,
                new SolidColorBrush(Color.FromArgb(160, 200, 200, 200)), DashStyleHelper.Dash, 1);

            if (_showLabels)
            {
                Draw.Text(this, "PremLbl",  true, "Premium",     0, _premiumTop    - TickSize * 4,  0, _premiumColor,
                    new SimpleFont("Arial", 8), TextAlignment.Right, Brushes.Transparent, Brushes.Transparent, 0);
                Draw.Text(this, "EqLbl",    true, "Equilibrium", 0, equilibrium    + TickSize * 2,  0, new SolidColorBrush(Colors.White),
                    new SimpleFont("Arial", 8), TextAlignment.Right, Brushes.Transparent, Brushes.Transparent, 0);
                Draw.Text(this, "DiscLbl",  true, "Discount",    0, _discountBottom + TickSize * 2, 0, _discountColor,
                    new SimpleFont("Arial", 8), TextAlignment.Right, Brushes.Transparent, Brushes.Transparent, 0);
            }
        }

        #region Properties

        [NinjaScriptProperty]
        [Range(2, 100)]
        [Display(Name = "Swing Length", Order = 1, GroupName = "Premium/Discount Zones")]
        public int SwingLen { get => _swingLen; set => _swingLen = Math.Max(2, value); }

        [NinjaScriptProperty]
        [Display(Name = "Show Labels", Order = 2, GroupName = "Premium/Discount Zones")]
        public bool ShowLabels { get => _showLabels; set => _showLabels = value; }

        [XmlIgnore]
        [Display(Name = "Premium Color", Order = 3, GroupName = "Premium/Discount Zones")]
        public Brush PremiumColor { get => _premiumColor; set => _premiumColor = value; }

        [Browsable(false)]
        public string PremiumColorSerializable
        {
            get { return Serialize.BrushToString(_premiumColor); }
            set { _premiumColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Equilibrium Color", Order = 4, GroupName = "Premium/Discount Zones")]
        public Brush EqColor { get => _eqColor; set => _eqColor = value; }

        [Browsable(false)]
        public string EqColorSerializable
        {
            get { return Serialize.BrushToString(_eqColor); }
            set { _eqColor = Serialize.StringToBrush(value); }
        }

        [XmlIgnore]
        [Display(Name = "Discount Color", Order = 5, GroupName = "Premium/Discount Zones")]
        public Brush DiscountColor { get => _discountColor; set => _discountColor = value; }

        [Browsable(false)]
        public string DiscountColorSerializable
        {
            get { return Serialize.BrushToString(_discountColor); }
            set { _discountColor = Serialize.StringToBrush(value); }
        }

        // Expose levels for strategies
        [Browsable(false)] public double PremiumTop     => _premiumTop;
        [Browsable(false)] public double PremiumBottom  => _premiumBottom;
        [Browsable(false)] public double EqTop          => _eqTop;
        [Browsable(false)] public double EqBottom       => _eqBottom;
        [Browsable(false)] public double DiscountTop    => _discountTop;
        [Browsable(false)] public double DiscountBottom => _discountBottom;

        [Browsable(false)] [XmlIgnore]
        public Series<double> EquilibriumLine => Values[0];

        #endregion
    }
}
