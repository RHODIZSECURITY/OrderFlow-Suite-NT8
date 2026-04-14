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

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
	public class PreviousDayLevels : Indicator
	{
		private const string SuiteVersion = "v1.0.1-phase1";
		private double prevHigh;
		private double prevLow;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Muestra máximo y mínimo del día previo.";
				Name = "Previous Day Levels [" + SuiteVersion + "]";
				Calculate = Calculate.OnBarClose;
				IsOverlay = true;
				IsSuspendedWhileInactive = true;
				ShowLabels = true;
				HighColor = Brushes.LimeGreen;
				LowColor = Brushes.OrangeRed;
				StartTime = 93000;
				EndTime = 160000;
				HighLineStyle = DashStyleHelper.Solid;
				LowLineStyle = DashStyleHelper.Dash;
				LineWidth = 2;
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

			int now = ToTime(Time[0]);
			if (now < StartTime || now > EndTime)
			{
				RemoveDrawObject("PDH");
				RemoveDrawObject("PDL");
				RemoveDrawObject("PDH_LABEL");
				RemoveDrawObject("PDL_LABEL");
				return;
			}

			var pdh = Draw.HorizontalLine(this, "PDH", prevHigh, HighColor);
			pdh.Stroke = new Stroke(HighColor, HighLineStyle, LineWidth);
			var pdl = Draw.HorizontalLine(this, "PDL", prevLow, LowColor);
			pdl.Stroke = new Stroke(LowColor, LowLineStyle, LineWidth);

			if (ShowLabels)
			{
				Draw.Text(this, "PDH_LABEL", "PDH", 0, prevHigh, HighColor);
				Draw.Text(this, "PDL_LABEL", "PDL", 0, prevLow, LowColor);
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
		[Display(Name = "Show labels", GroupName = "Previous Day Levels", Order = 0)]
		public bool ShowLabels { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Start Time (HHmmss)", GroupName = "Previous Day Levels", Order = 1)]
		public int StartTime { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "End Time (HHmmss)", GroupName = "Previous Day Levels", Order = 2)]
		public int EndTime { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "High line style", GroupName = "Previous Day Levels", Order = 3)]
		public DashStyleHelper HighLineStyle { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Low line style", GroupName = "Previous Day Levels", Order = 4)]
		public DashStyleHelper LowLineStyle { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Line width", GroupName = "Previous Day Levels", Order = 5)]
		public int LineWidth { get; set; }

		[XmlIgnore]
		[Display(Name = "High color", GroupName = "Previous Day Levels", Order = 6)]
		public Brush HighColor { get; set; }
		[Browsable(false)]
		public string HighColorSerializable { get { return BrushToString(HighColor); } set { HighColor = StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Low color", GroupName = "Previous Day Levels", Order = 7)]
		public Brush LowColor { get; set; }
		[Browsable(false)]
		public string LowColorSerializable { get { return BrushToString(LowColor); } set { LowColor = StringToBrush(value); } }
		#endregion
	}
}
