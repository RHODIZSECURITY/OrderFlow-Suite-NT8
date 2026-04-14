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

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
	public class SupportResistance : Indicator
	{
		private const string SuiteVersion = "v1.0.1-phase1";
		private Swing swing;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Soportes y resistencias automáticas basadas en Swing, pintadas como zona.";
				Name = "Support Resistance [" + SuiteVersion + "]";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				Strength = 5;
				SupportColor = Brushes.DodgerBlue;
				ResistanceColor = Brushes.Crimson;
				ZoneTicks = 8;
				ForwardBars = 120;
				Opacity = 20;
				ShowLabels = true;
				LabelColor = Brushes.White;
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
			{
				double resTop = sHigh + (ZoneTicks * TickSize * 0.5);
				double resBot = sHigh - (ZoneTicks * TickSize * 0.5);
				Draw.Rectangle(this, "RES_ZONE_" + CurrentBar, false, 0, resTop, -ForwardBars, resBot, ResistanceColor, ResistanceColor, Opacity);
				if (ShowLabels)
					Draw.Text(this, "RES_LABEL_" + CurrentBar, "RESISTANCE", 0, (resTop + resBot) * 0.5, LabelColor);
			}

			if (sLow > 0)
			{
				double supTop = sLow + (ZoneTicks * TickSize * 0.5);
				double supBot = sLow - (ZoneTicks * TickSize * 0.5);
				Draw.Rectangle(this, "SUP_ZONE_" + CurrentBar, false, 0, supTop, -ForwardBars, supBot, SupportColor, SupportColor, Opacity);
				if (ShowLabels)
					Draw.Text(this, "SUP_LABEL_" + CurrentBar, "SUPPORT", 0, (supTop + supBot) * 0.5, LabelColor);
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
		[Display(Name = "Suite version", GroupName = "Info", Order = 0)]
		public string VersionInfo { get { return SuiteVersion; } }

		[NinjaScriptProperty]
		[Range(2, 50)]
		[Display(Name = "Swing strength", GroupName = "S/R", Order = 0)]
		public int Strength { get; set; }


		[NinjaScriptProperty]
		[Range(2, 100)]
		[Display(Name = "Zone ticks", GroupName = "S/R", Order = 1)]
		public int ZoneTicks { get; set; }

		[NinjaScriptProperty]
		[Range(5, 500)]
		[Display(Name = "Forward bars", GroupName = "S/R", Order = 2)]
		public int ForwardBars { get; set; }

		[NinjaScriptProperty]
		[Range(0, 100)]
		[Display(Name = "Opacity %", GroupName = "S/R", Order = 3)]
		public int Opacity { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show labels", GroupName = "S/R", Order = 4)]
		public bool ShowLabels { get; set; }

		[XmlIgnore]
		[Display(Name = "Label color", GroupName = "S/R", Order = 5)]
		public Brush LabelColor { get; set; }
		[Browsable(false)]
		public string LabelColorSerializable { get { return BrushToString(LabelColor); } set { LabelColor = StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Support color", GroupName = "S/R", Order = 6)]
		public Brush SupportColor { get; set; }
		[Browsable(false)]
		public string SupportColorSerializable { get { return BrushToString(SupportColor); } set { SupportColor = StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Resistance color", GroupName = "S/R", Order = 7)]
		public Brush ResistanceColor { get; set; }
		[Browsable(false)]
		public string ResistanceColorSerializable { get { return BrushToString(ResistanceColor); } set { ResistanceColor = StringToBrush(value); } }
		#endregion
	}
}
