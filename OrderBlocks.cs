// Última actualización: 2026-04-13

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ
{
	public class OrderBlocks : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Detecta Order Blocks básicos usando velas de impulso/engulfing.";
				Name = "Order Blocks";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				BullColor = Brushes.LimeGreen;
				BearColor = Brushes.OrangeRed;
				Opacity = 20;
				ForwardBars = 30;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 3)
				return;

			bool bullishImpulse = Close[0] > Open[0] && Close[0] > High[1] && Open[1] > Close[1];
			bool bearishImpulse = Close[0] < Open[0] && Close[0] < Low[1] && Open[1] < Close[1];

			if (bullishImpulse)
			{
				double top = High[1];
				double bot = Low[1];
				Draw.Rectangle(this, "OB_BULL_" + CurrentBar, false, 1, top, -ForwardBars, bot, BullColor, BullColor, Opacity);
			}
			else if (bearishImpulse)
			{
				double top = High[1];
				double bot = Low[1];
				Draw.Rectangle(this, "OB_BEAR_" + CurrentBar, false, 1, top, -ForwardBars, bot, BearColor, BearColor, Opacity);
			}
		}

		private string BrushToString(Brush brush)
		{
			return brush == null
				? string.Empty
				: new BrushConverter().ConvertToString(null, CultureInfo.InvariantCulture, brush);
		}

		private Brush StringToBrush(string value)
		{
			if (string.IsNullOrWhiteSpace(value))
				return Brushes.Transparent;

			var converted = new BrushConverter().ConvertFromString(null, CultureInfo.InvariantCulture, value);
			return converted as Brush ?? Brushes.Transparent;
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(5, 200)]
		[Display(Name = "Forward bars", GroupName = "Order Blocks", Order = 0)]
		public int ForwardBars { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Opacity %", GroupName = "Order Blocks", Order = 1)]
		public int Opacity { get; set; }

		[XmlIgnore]
		[Display(Name = "Bull color", GroupName = "Order Blocks", Order = 2)]
		public Brush BullColor { get; set; }
		[Browsable(false)]
		public string BullColorSerializable { get { return BrushToString(BullColor); } set { BullColor = StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Bear color", GroupName = "Order Blocks", Order = 3)]
		public Brush BearColor { get; set; }
		[Browsable(false)]
		public string BearColorSerializable { get { return BrushToString(BearColor); } set { BearColor = StringToBrush(value); } }
		#endregion
	}
}
