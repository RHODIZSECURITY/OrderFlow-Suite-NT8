// Última actualización: 2026-04-13

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
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
		public string BullColorSerializable { get { return NinjaTrader.Gui.Tools.Serialize.BrushToString(BullColor); } set { BullColor = NinjaTrader.Gui.Tools.Serialize.StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Bear color", GroupName = "Order Blocks", Order = 3)]
		public Brush BearColor { get; set; }
		[Browsable(false)]
		public string BearColorSerializable { get { return NinjaTrader.Gui.Tools.Serialize.BrushToString(BearColor); } set { BearColor = NinjaTrader.Gui.Tools.Serialize.StringToBrush(value); } }
		#endregion
	}
}
