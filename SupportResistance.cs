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
	public class SupportResistance : Indicator
	{
		private Swing swing;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Soportes y resistencias automáticas basadas en Swing.";
				Name = "Support Resistance";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				Strength = 5;
				SupportColor = Brushes.DodgerBlue;
				ResistanceColor = Brushes.Crimson;
			}
			else if (State == State.DataLoaded)
			{
				swing = Swing(Strength);
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < Strength * 2)
				return;

			double sHigh = swing.SwingHigh[0];
			double sLow = swing.SwingLow[0];

			if (sHigh > 0)
				Draw.HorizontalLine(this, "RES_" + CurrentBar, sHigh, ResistanceColor);
			if (sLow > 0)
				Draw.HorizontalLine(this, "SUP_" + CurrentBar, sLow, SupportColor);
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(2, 50)]
		[Display(Name = "Swing strength", GroupName = "S/R", Order = 0)]
		public int Strength { get; set; }

		[XmlIgnore]
		[Display(Name = "Support color", GroupName = "S/R", Order = 1)]
		public Brush SupportColor { get; set; }
		[Browsable(false)]
		public string SupportColorSerializable { get { return Serialize.BrushToString(SupportColor); } set { SupportColor = Serialize.StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Resistance color", GroupName = "S/R", Order = 2)]
		public Brush ResistanceColor { get; set; }
		[Browsable(false)]
		public string ResistanceColorSerializable { get { return Serialize.BrushToString(ResistanceColor); } set { ResistanceColor = Serialize.StringToBrush(value); } }
		#endregion
	}
}
