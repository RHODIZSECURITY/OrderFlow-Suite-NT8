// Última actualización: 2026-04-13

#region Using declarations
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

namespace NinjaTrader.NinjaScript.Indicators.OrderFlow_Suite_RHODIZ_v1_0_0
{
	public class BigTrades : Indicator
	{
		private long tradeCounter;
		private const int MAX_OPACITY = 100;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description								= @"Detecta y marca operaciones grandes (big trades) en tiempo real.";
				Name									= "Big Trades";
				Calculate							= Calculate.OnEachTick;
				IsOverlay							= true;
				DisplayInDataBox					= true;
				DrawOnPricePanel					= true;
				PaintPriceMarkers					= true;
				IsSuspendedWhileInactive			= false;

				MinTradeSize = 50;
				MinBubbleSizeTicks = 2;
				MaxBubbleSizeTicks = 12;
				VolumeScale = 25;
				BubbleWidthSeconds = 2;
				FillOpacity = 55;
				ShowTradeSizeText = false;
				BuyTradeColor = Brushes.LimeGreen;
				SellTradeColor = Brushes.OrangeRed;
				NeutralTradeColor = Brushes.Gold;
			}
			else if (State == State.DataLoaded)
			{
				tradeCounter = 0;
			}
		}

		protected override void OnMarketData(MarketDataEventArgs marketArgs)
		{
			if (marketArgs == null || marketArgs.MarketDataType != MarketDataType.Last)
				return;
			if (marketArgs.Volume < MinTradeSize)
				return;
			if (CurrentBar < 0)
				return;

			tradeCounter++;
			bool isBuy = marketArgs.Ask > 0 && marketArgs.Price >= marketArgs.Ask;
			bool isSell = marketArgs.Bid > 0 && marketArgs.Price <= marketArgs.Bid;

			Brush markerBrush = NeutralTradeColor;
			double y = marketArgs.Price;
			string sideTag = "N";

			if (isBuy)
			{
				markerBrush = BuyTradeColor;
				sideTag = "B";
			}
			else if (isSell)
			{
				markerBrush = SellTradeColor;
				sideTag = "S";
			}

			string tag = string.Format("BigTrade_{0}_{1}_{2}", CurrentBar, tradeCounter, sideTag);
			double bubbleTicks = getBubbleSizeTicks(marketArgs.Volume);
			double y1 = y - (bubbleTicks * TickSize);
			double y2 = y + (bubbleTicks * TickSize);
			double halfSeconds = Math.Max(0.5, BubbleWidthSeconds / 2.0);
			DateTime startTime = marketArgs.Time.AddSeconds(-halfSeconds);
			DateTime endTime = marketArgs.Time.AddSeconds(halfSeconds);

			Draw.Ellipse(this, tag, false, startTime, y1, endTime, y2, markerBrush, markerBrush, FillOpacity);

			if (ShowTradeSizeText)
			{
				string textTag = string.Format("BigTradeTxt_{0}_{1}_{2}", CurrentBar, tradeCounter, sideTag);
				Draw.Text(this, textTag, false,
					string.Format("{0}", marketArgs.Volume),
					0,
					y,
					0,
					Brushes.White,
					new SimpleFont("Arial", 9),
					TextAlignment.Center,
					Brushes.Transparent,
					Brushes.Transparent,
					0);
			}
		}

		private double getBubbleSizeTicks(long volume)
		{
			double extra = volume / (double)Math.Max(1, VolumeScale);
			double ticks = MinBubbleSizeTicks + extra;
			return Math.Max(MinBubbleSizeTicks, Math.Min(MaxBubbleSizeTicks, ticks));
		}

		#region Properties

		[NinjaScriptProperty]
		[Range(1, long.MaxValue)]
		[Display(Name = "Min trade size", Order = 0, GroupName = "Big Trades")]
		public long MinTradeSize
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "Min bubble (ticks)", Order = 1, GroupName = "Big Trades")]
		public int MinBubbleSizeTicks
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, 80)]
		[Display(Name = "Max bubble (ticks)", Order = 2, GroupName = "Big Trades")]
		public int MaxBubbleSizeTicks
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, 1000)]
		[Display(Name = "Volume scale", Order = 3, GroupName = "Big Trades")]
		public int VolumeScale
		{ get; set; }

		[NinjaScriptProperty]
		[Range(1, 20)]
		[Display(Name = "Bubble width (sec)", Order = 4, GroupName = "Big Trades")]
		public int BubbleWidthSeconds
		{ get; set; }

		[NinjaScriptProperty]
		[Range(0, MAX_OPACITY)]
		[Display(Name = "Bubble opacity %", Order = 5, GroupName = "Big Trades")]
		public int FillOpacity
		{ get; set; }

		[NinjaScriptProperty]
		[Display(Name = "Show size text", Order = 6, GroupName = "Big Trades")]
		public bool ShowTradeSizeText
		{ get; set; }

		[XmlIgnore]
		[Display(Name = "Buy trade color", Order = 7, GroupName = "Big Trades")]
		public Brush BuyTradeColor
		{ get; set; }
		[Browsable(false)]
		public string BuyTradeColorSerializable
		{
			get { return Serialize.BrushToString(BuyTradeColor); }
			set { BuyTradeColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Sell trade color", Order = 8, GroupName = "Big Trades")]
		public Brush SellTradeColor
		{ get; set; }
		[Browsable(false)]
		public string SellTradeColorSerializable
		{
			get { return Serialize.BrushToString(SellTradeColor); }
			set { SellTradeColor = Serialize.StringToBrush(value); }
		}

		[XmlIgnore]
		[Display(Name = "Neutral trade color", Order = 9, GroupName = "Big Trades")]
		public Brush NeutralTradeColor
		{ get; set; }
		[Browsable(false)]
		public string NeutralTradeColorSerializable
		{
			get { return Serialize.BrushToString(NeutralTradeColor); }
			set { NeutralTradeColor = Serialize.StringToBrush(value); }
		}

		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades[] cacheBigTrades;
		public OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades BigTrades(long minTradeSize, int minBubbleSizeTicks, int maxBubbleSizeTicks, int volumeScale, int bubbleWidthSeconds, int fillOpacity, bool showTradeSizeText)
		{
			return BigTrades(Input, minTradeSize, minBubbleSizeTicks, maxBubbleSizeTicks, volumeScale, bubbleWidthSeconds, fillOpacity, showTradeSizeText);
		}

		public OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades BigTrades(ISeries<double> input, long minTradeSize, int minBubbleSizeTicks, int maxBubbleSizeTicks, int volumeScale, int bubbleWidthSeconds, int fillOpacity, bool showTradeSizeText)
		{
			if (cacheBigTrades != null)
				for (int idx = 0; idx < cacheBigTrades.Length; idx++)
					if (cacheBigTrades[idx] != null && cacheBigTrades[idx].MinTradeSize == minTradeSize && cacheBigTrades[idx].MinBubbleSizeTicks == minBubbleSizeTicks && cacheBigTrades[idx].MaxBubbleSizeTicks == maxBubbleSizeTicks && cacheBigTrades[idx].VolumeScale == volumeScale && cacheBigTrades[idx].BubbleWidthSeconds == bubbleWidthSeconds && cacheBigTrades[idx].FillOpacity == fillOpacity && cacheBigTrades[idx].ShowTradeSizeText == showTradeSizeText && cacheBigTrades[idx].EqualsInput(input))
						return cacheBigTrades[idx];
			return CacheIndicator<OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades>(new OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades(){ MinTradeSize = minTradeSize, MinBubbleSizeTicks = minBubbleSizeTicks, MaxBubbleSizeTicks = maxBubbleSizeTicks, VolumeScale = volumeScale, BubbleWidthSeconds = bubbleWidthSeconds, FillOpacity = fillOpacity, ShowTradeSizeText = showTradeSizeText }, input, ref cacheBigTrades);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades BigTrades(long minTradeSize, int minBubbleSizeTicks, int maxBubbleSizeTicks, int volumeScale, int bubbleWidthSeconds, int fillOpacity, bool showTradeSizeText)
		{
			return indicator.BigTrades(Input, minTradeSize, minBubbleSizeTicks, maxBubbleSizeTicks, volumeScale, bubbleWidthSeconds, fillOpacity, showTradeSizeText);
		}

		public Indicators.OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades BigTrades(ISeries<double> input , long minTradeSize, int minBubbleSizeTicks, int maxBubbleSizeTicks, int volumeScale, int bubbleWidthSeconds, int fillOpacity, bool showTradeSizeText)
		{
			return indicator.BigTrades(input, minTradeSize, minBubbleSizeTicks, maxBubbleSizeTicks, volumeScale, bubbleWidthSeconds, fillOpacity, showTradeSizeText);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades BigTrades(long minTradeSize, int minBubbleSizeTicks, int maxBubbleSizeTicks, int volumeScale, int bubbleWidthSeconds, int fillOpacity, bool showTradeSizeText)
		{
			return indicator.BigTrades(Input, minTradeSize, minBubbleSizeTicks, maxBubbleSizeTicks, volumeScale, bubbleWidthSeconds, fillOpacity, showTradeSizeText);
		}

		public Indicators.OrderFlow_Suite_RHODIZ_v1_0_0.BigTrades BigTrades(ISeries<double> input , long minTradeSize, int minBubbleSizeTicks, int maxBubbleSizeTicks, int volumeScale, int bubbleWidthSeconds, int fillOpacity, bool showTradeSizeText)
		{
			return indicator.BigTrades(input, minTradeSize, minBubbleSizeTicks, maxBubbleSizeTicks, volumeScale, bubbleWidthSeconds, fillOpacity, showTradeSizeText);
		}
	}
}

#endregion
