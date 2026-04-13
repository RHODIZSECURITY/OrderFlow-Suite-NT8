// Última actualización: 2026-04-13

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.WyckoffZen
{
	public class PreviousDayLevels : Indicator
	{
		private double prevHigh;
		private double prevLow;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Muestra máximo y mínimo del día previo.";
				Name = "Previous Day Levels";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				IsSuspendedWhileInactive = true;
				ShowLabels = true;
				HighColor = Brushes.LimeGreen;
				LowColor = Brushes.OrangeRed;
			}
			else if (State == State.Configure)
			{
				AddDataSeries(BarsPeriodType.Day, 1);
			}
		}

		protected override void OnBarUpdate()
		{
			if (CurrentBars[0] < 1 || CurrentBars[1] < 2)
				return;

			if (BarsInProgress == 1)
			{
				prevHigh = Highs[1][1];
				prevLow = Lows[1][1];
				return;
			}

			if (prevHigh <= 0 || prevLow <= 0)
				return;

			Draw.HorizontalLine(this, "PDH", prevHigh, HighColor);
			Draw.HorizontalLine(this, "PDL", prevLow, LowColor);

			if (ShowLabels)
			{
				Draw.TextFixed(this, "PDLevels", string.Format("PDH: {0:F2} | PDL: {1:F2}", prevHigh, prevLow), TextPosition.TopRight);
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
		[Display(Name = "Show labels", GroupName = "Previous Day Levels", Order = 0)]
		public bool ShowLabels { get; set; }

		[XmlIgnore]
		[Display(Name = "High color", GroupName = "Previous Day Levels", Order = 1)]
		public Brush HighColor { get; set; }
		[Browsable(false)]
		public string HighColorSerializable { get { return BrushToString(HighColor); } set { HighColor = StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Low color", GroupName = "Previous Day Levels", Order = 2)]
		public Brush LowColor { get; set; }
		[Browsable(false)]
		public string LowColorSerializable { get { return BrushToString(LowColor); } set { LowColor = StringToBrush(value); } }
		#endregion
	}
}
