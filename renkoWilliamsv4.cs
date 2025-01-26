#region Using declarations
using System;
using NinjaTrader.Cbi;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using System.ComponentModel.DataAnnotations;
using System.Collections.ObjectModel;
#endregion

namespace NinjaTrader.NinjaScript.Strategies
{
    public class WilliamsRenko : Strategy
    {
        private int williamsRPeriod = 14;
        private int trailStopDistance = 80;
        private int profitTargetTicks = 80;
        private double overboughtLevel = -10;
        private double oversoldLevel = -90;

        private double dailyGoal = 1000;
        private double dailyLossLimit = -1000;
        private DateTime lastTradeDate = DateTime.MinValue;
        private double entryPrice = 0;
        private TimeSpan tradingStartTime = new TimeSpan(3, 30, 0);
        private TimeSpan tradingEndTime = new TimeSpan(4, 0, 0);

        // New variables for cooldown
        private bool profitTargetHit = false;
        private int barsSinceProfitTarget = 0;
        private int cooldownPeriod = 2; // Number of bars to wait after profit target

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Renko strategy using Williams %R with momentum-based entries.";
                Name = "WilliamsRenko";
                Calculate = Calculate.OnBarClose;
                EntriesPerDirection = 1;
                EntryHandling = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy = true;
                ExitOnSessionCloseSeconds = 30;
                BarsRequiredToTrade = 20;
                StartBehavior = StartBehavior.WaitUntilFlat;
                Slippage = 0;
                TimeInForce = Cbi.TimeInForce.Gtc;
                TraceOrders = false;
                IsInstantiatedOnEachOptimizationIteration = false;
            }
            else if (State == State.DataLoaded)
            {
                lastTradeDate = DateTime.MinValue;
                profitTargetHit = false;
                barsSinceProfitTarget = 0;
            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < BarsRequiredToTrade)
                return;

            // Reset daily PNL and cooldown tracking at the start of a new trading day
            if (Time[0].Date != lastTradeDate.Date)
            {
                lastTradeDate = Time[0].Date;
                profitTargetHit = false;
                barsSinceProfitTarget = 0;
            }

            // Track bars since profit target
            if (profitTargetHit)
            {
                barsSinceProfitTarget++;
            }

            // Reset cooldown if we've waited the required number of bars
            if (profitTargetHit && barsSinceProfitTarget >= cooldownPeriod)
            {
                profitTargetHit = false;
                barsSinceProfitTarget = 0;
            }

            // Ensure we are within trading hours
            TimeSpan currentTime = Time[0].TimeOfDay;
            if (currentTime < tradingStartTime || currentTime > tradingEndTime)
                return;

            // Check if cumulative profit/loss has reached daily limits
            double cumulativeProfitLoss = SystemPerformance.RealTimeTrades.TradesPerformance.Currency.CumProfit;

            if (cumulativeProfitLoss >= dailyGoal || cumulativeProfitLoss <= dailyLossLimit)
            {
                if (Position.MarketPosition != MarketPosition.Flat)
                    ExitLong();
                return;
            }

            // Calculate Williams %R
            double williamsRValue = WilliamsR(williamsRPeriod)[0];

            // Modified Entry Logic with Cooldown
            if (williamsRValue > overboughtLevel &&
                Position.MarketPosition != MarketPosition.Long &&
                !profitTargetHit)
            {
                EnterLong("LongRenko");
                entryPrice = Close[0];
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);
                SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);
            }
            else if (williamsRValue < oversoldLevel &&
                     Position.MarketPosition != MarketPosition.Short &&
                     !profitTargetHit)
            {
                EnterShort("ShortRenko");
                entryPrice = Close[0];
                SetTrailStop(CalculationMode.Ticks, trailStopDistance);
                SetProfitTarget(CalculationMode.Ticks, profitTargetTicks);
            }
        }

        protected override void OnExecutionUpdate(Cbi.Execution execution, string executionId, double price, int quantity, MarketPosition marketPosition, string orderId, DateTime time)
        {
            // Check if the profit target was hit
            if (execution.Order != null && execution.Order.Name == "Profit target" &&
                (execution.Order.OrderState == OrderState.Filled ||
                 execution.Order.OrderState == OrderState.PartFilled))
            {
                profitTargetHit = true;
                barsSinceProfitTarget = 0;
            }
        }

        #region Properties

        [Display(Name = "Start Time", Description = "Trading session start time", Order = 1, GroupName = "Parameters")]
        public TimeSpan TradingStartTime
        {
            get { return tradingStartTime; }
            set { tradingStartTime = value; }
        }

        [Display(Name = "End Time", Description = "Trading session end time", Order = 2, GroupName = "Parameters")]
        public TimeSpan TradingEndTime
        {
            get { return tradingEndTime; }
            set { tradingEndTime = value; }
        }

        [Display(Name = "Daily Profit Goal", Description = "Maximum profit allowed per day", Order = 3, GroupName = "Parameters")]
        public double DailyGoal
        {
            get { return dailyGoal; }
            set { dailyGoal = value; }
        }

        [Display(Name = "Daily Loss Limit", Description = "Maximum loss allowed per day", Order = 4, GroupName = "Parameters")]
        public double DailyLossLimit
        {
            get { return dailyLossLimit; }
            set { dailyLossLimit = value; }
        }

        [Display(Name = "Trail Stop Distance (Ticks)", Description = "Trailing stop distance in ticks", Order = 5, GroupName = "Parameters")]
        public int TrailStopDistance
        {
            get { return trailStopDistance; }
            set { trailStopDistance = value; }
        }

        [Display(Name = "Profit Target (Ticks)", Description = "Profit target in ticks", Order = 6, GroupName = "Parameters")]
        public int ProfitTargetTicks
        {
            get { return profitTargetTicks; }
            set { profitTargetTicks = value; }
        }
        [Display(Name = "Williams %R Period", Description = "Number of periods for Williams %R calculation", Order = 7, GroupName = "Parameters")]
        public int WilliamsRPeriod
        {
            get { return williamsRPeriod; }
            set { williamsRPeriod = Math.Max(1, value); }
        }

        [Display(Name = "Overbought Level", Description = "Williams %R level considered overbought", Order = 8, GroupName = "Parameters")]
        public double OverboughtLevel
        {
            get { return overboughtLevel; }
            set { overboughtLevel = value; }
        }

        [Display(Name = "Oversold Level", Description = "Williams %R level considered oversold", Order = 9, GroupName = "Parameters")]
        public double OversoldLevel
        {
            get { return oversoldLevel; }
            set { oversoldLevel = value; }
        }
        [Display(Name = "Cooldown Bars", Description = "Number of bars to wait after hitting profit target", Order = 10, GroupName = "Parameters")]
        public int CooldownPeriod
        {
            get { return cooldownPeriod; }
            set { cooldownPeriod = Math.Max(0, value); }
        }
        #endregion
    }
}
