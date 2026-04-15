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
	public class NYPreMarketLevels : Indicator
	{
		private const string SuiteVersion = "v1.0.2";
		private DateTime currentDate;
		private double preHigh;
		private double preLow;
		private double preVwap;
		private double preCumPV;
		private double preCumVol;
		private bool finalized;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Muestra máximo y mínimo del pre-market de NY.";
				Name = "NY PreMarket Levels [" + SuiteVersion + "]";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				IsSuspendedWhileInactive = true;
				PreMarketStart = 40000;
				PreMarketEnd = 93000;
				HighColor = Brushes.DeepSkyBlue;
				LowColor = Brushes.MediumPurple;
				ShowLabels = true;
				DisplayStart = 93000;
				DisplayEnd = 160000;
					HighLineStyle = DashStyleHelper.Solid;
					LowLineStyle = DashStyleHelper.Dash;
					LineWidth = 2;
					ShowPMVwapLine = true;
					PMVwapColor = Brushes.Goldenrod;
					PMVwapLineStyle = DashStyleHelper.Dot;
				}
			}

		protected override void OnBarUpdate()
		{
			if (CurrentBar < 1)
				return;

			if (currentDate != Time[0].Date)
			{
				currentDate = Time[0].Date;
					preHigh = double.MinValue;
					preLow = double.MaxValue;
					preVwap = 0;
					preCumPV = 0;
					preCumVol = 0;
					finalized = false;
				}

			int now = ToTime(Time[0]);
			if (now >= PreMarketStart && now <= PreMarketEnd)
				{
					preHigh = Math.Max(preHigh, High[0]);
					preLow = Math.Min(preLow, Low[0]);
					double tp = (High[0] + Low[0] + Close[0]) / 3.0;
					preCumPV += tp * Volume[0];
					preCumVol += Volume[0];
					preVwap = preCumVol > 0 ? preCumPV / preCumVol : preVwap;
				}
			else if (now > PreMarketEnd)
			{
				finalized = true;
			}

			if (!finalized && (preHigh == double.MinValue || preLow == double.MaxValue))
				return;

			if (now < DisplayStart || now > DisplayEnd)
			{
				RemoveDrawObject("NYPMH_" + currentDate.ToString("yyyyMMdd"));
					RemoveDrawObject("NYPML_" + currentDate.ToString("yyyyMMdd"));
					RemoveDrawObject("NYPMVWAP_" + currentDate.ToString("yyyyMMdd"));
					RemoveDrawObject("NYPMH_LABEL_" + currentDate.ToString("yyyyMMdd"));
					RemoveDrawObject("NYPML_LABEL_" + currentDate.ToString("yyyyMMdd"));
					RemoveDrawObject("NYPMVWAP_LABEL_" + currentDate.ToString("yyyyMMdd"));
					return;
				}

			if (preHigh != double.MinValue)
			{
				var hLine = Draw.HorizontalLine(this, "NYPMH_" + currentDate.ToString("yyyyMMdd"), preHigh, HighColor);
				hLine.Stroke = new Stroke(HighColor, HighLineStyle, LineWidth);
				if (ShowLabels)
					Draw.Text(this, "NYPMH_LABEL_" + currentDate.ToString("yyyyMMdd"), "PRE HIGH", 0, preHigh, HighColor);
			}
			if (preLow != double.MaxValue)
			{
				var lLine = Draw.HorizontalLine(this, "NYPML_" + currentDate.ToString("yyyyMMdd"), preLow, LowColor);
				lLine.Stroke = new Stroke(LowColor, LowLineStyle, LineWidth);
				if (ShowLabels)
					Draw.Text(this, "NYPML_LABEL_" + currentDate.ToString("yyyyMMdd"), "PRE LOW", 0, preLow, LowColor);
			}
			if (ShowPMVwapLine && preVwap > 0)
			{
				var vLine = Draw.HorizontalLine(this, "NYPMVWAP_" + currentDate.ToString("yyyyMMdd"), preVwap, PMVwapColor);
				vLine.Stroke = new Stroke(PMVwapColor, PMVwapLineStyle, LineWidth);
				if (ShowLabels)
					Draw.Text(this, "NYPMVWAP_LABEL_" + currentDate.ToString("yyyyMMdd"), "PRE VWAP", 0, preVwap, PMVwapColor);
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
		[Range(0, 235959)]
		[Display(Name = "PreMarket Start (HHmmss)", GroupName = "NY PreMarket", Order = 0)]
		public int PreMarketStart { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "PreMarket End (HHmmss)", GroupName = "NY PreMarket", Order = 1)]
		public int PreMarketEnd { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show labels", GroupName = "NY PreMarket", Order = 2)]
		public bool ShowLabels { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Display start (HHmmss)", GroupName = "NY PreMarket", Order = 3)]
		public int DisplayStart { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "Display end (HHmmss)", GroupName = "NY PreMarket", Order = 4)]
		public int DisplayEnd { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "High line style", GroupName = "NY PreMarket", Order = 5)]
		public DashStyleHelper HighLineStyle { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Low line style", GroupName = "NY PreMarket", Order = 6)]
		public DashStyleHelper LowLineStyle { get; set; }

		[NinjaScriptProperty]
		[Range(1, 10)]
		[Display(Name = "Line width", GroupName = "NY PreMarket", Order = 7)]
		public int LineWidth { get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show PM VWAP", GroupName = "NY PreMarket", Order = 8)]
		public bool ShowPMVwapLine { get; set; }

		[XmlIgnore]
		[Display(Name = "High color", GroupName = "NY PreMarket", Order = 9)]
		public Brush HighColor { get; set; }
		[Browsable(false)]
		public string HighColorSerializable { get { return BrushToString(HighColor); } set { HighColor = StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Low color", GroupName = "NY PreMarket", Order = 10)]
		public Brush LowColor { get; set; }
		[Browsable(false)]
		public string LowColorSerializable { get { return BrushToString(LowColor); } set { LowColor = StringToBrush(value); } }

		[NinjaScriptProperty]
		[Display(Name = "PM VWAP line style", GroupName = "NY PreMarket", Order = 11)]
		public DashStyleHelper PMVwapLineStyle { get; set; }

		[XmlIgnore]
		[Display(Name = "PM VWAP color", GroupName = "NY PreMarket", Order = 12)]
		public Brush PMVwapColor { get; set; }
		[Browsable(false)]
		public string PMVwapColorSerializable { get { return BrushToString(PMVwapColor); } set { PMVwapColor = StringToBrush(value); } }
		#endregion
	}
}
