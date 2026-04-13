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
	public class NYPreMarketLevels : Indicator
	{
		private DateTime currentDate;
		private double preHigh;
		private double preLow;
		private bool finalized;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description = @"Muestra máximo y mínimo del pre-market de NY.";
				Name = "NY PreMarket Levels";
				Calculate = Calculate.OnEachTick;
				IsOverlay = true;
				IsSuspendedWhileInactive = true;
				PreMarketStart = 40000;
				PreMarketEnd = 93000;
				HighColor = Brushes.DeepSkyBlue;
				LowColor = Brushes.MediumPurple;
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
				finalized = false;
			}

			int now = ToTime(Time[0]);
			if (now >= PreMarketStart && now <= PreMarketEnd)
			{
				preHigh = Math.Max(preHigh, High[0]);
				preLow = Math.Min(preLow, Low[0]);
			}
			else if (now > PreMarketEnd)
			{
				finalized = true;
			}

			if (!finalized && (preHigh == double.MinValue || preLow == double.MaxValue))
				return;

			if (preHigh != double.MinValue)
				Draw.HorizontalLine(this, "NYPMH_" + currentDate.ToString("yyyyMMdd"), preHigh, HighColor);
			if (preLow != double.MaxValue)
				Draw.HorizontalLine(this, "NYPML_" + currentDate.ToString("yyyyMMdd"), preLow, LowColor);
		}

		#region Properties
		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "PreMarket Start (HHmmss)", GroupName = "NY PreMarket", Order = 0)]
		public int PreMarketStart { get; set; }

		[NinjaScriptProperty]
		[Range(0, 235959)]
		[Display(Name = "PreMarket End (HHmmss)", GroupName = "NY PreMarket", Order = 1)]
		public int PreMarketEnd { get; set; }

		[XmlIgnore]
		[Display(Name = "High color", GroupName = "NY PreMarket", Order = 2)]
		public Brush HighColor { get; set; }
		[Browsable(false)]
		public string HighColorSerializable { get { return Serialize.BrushToString(HighColor); } set { HighColor = Serialize.StringToBrush(value); } }

		[XmlIgnore]
		[Display(Name = "Low color", GroupName = "NY PreMarket", Order = 3)]
		public Brush LowColor { get; set; }
		[Browsable(false)]
		public string LowColorSerializable { get { return Serialize.BrushToString(LowColor); } set { LowColor = Serialize.StringToBrush(value); } }
		#endregion
	}
}
