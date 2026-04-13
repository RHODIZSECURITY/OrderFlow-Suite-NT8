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
	public class FairValueGaps : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Detecta Fair Value Gaps (FVG) alcistas y bajistas de 3 velas.";
				Name = "Fair Value Gaps";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				ForwardBars = 20;
				BullColor = Brushes.Lime;
				BearColor = Brushes.Red;
				Opacity = 20;
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 2)
				return;

			bool bullishFvg = Low[0] > High[2];
			bool bearishFvg = High[0] < Low[2];

			if (bullishFvg)
			{
				double top = Low[0];
				double bottom = High[2];
				Draw.Rectangle(this, "FVG_BULL_" + CurrentBar, false, 0, top, -ForwardBars, bottom, BullColor, BullColor, Opacity);
			}
			if (bearishFvg)
			{
				double top = Low[2];
				double bottom = High[0];
				Draw.Rectangle(this, "FVG_BEAR_" + CurrentBar, false, 0, top, -ForwardBars, bottom, BearColor, BearColor, Opacity);
			}
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(5, 200)]
		[Display(Name = "Forward bars", GroupName = "FVG", Order = 0)]
		public int ForwardBars { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Opacity %", GroupName = "FVG", Order = 1)]
		public int Opacity { get; set; }

		[XmlIgnore]
		[Display(Name = "Bull color", GroupName = "FVG", Order = 2)]
		public Brush BullColor { get; set; }
		[Browsable(false)]
		public string BullColorSerializable { get { return NinjaTrader.Gui.Tools.Serialize.BrushToString(BullColor); } set { BullColor = NinjaTrader.Gui.Tools.Serialize.StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Bear color", GroupName = "FVG", Order = 3)]
		public Brush BearColor { get; set; }
		[Browsable(false)]
		public string BearColorSerializable { get { return NinjaTrader.Gui.Tools.Serialize.BrushToString(BearColor); } set { BearColor = NinjaTrader.Gui.Tools.Serialize.StringToBrush(value); } }
		#endregion
	}
}
