using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace InvestmentSimulation
{
    class Program
    {
        private const double PurchaseAmount = 100000;

        private const int PurchasePeriod = 18;
        private const int HoldPeriod = 12*20 - 18;
        private const int SellPeriod = 12*10;
        private const string DataFilePath = "../../../197812-202102 MCSI Worldchart.csv";
        private const double ExpectedAnnualizedReturnInPercent = 5d;

        private static double CalculateAnnualizedReturnInPercent(double totalReturnInPercent, double lengthInYears)
        {
            double absoluteReturn = totalReturnInPercent / 100;
            return (Math.Pow(1 + absoluteReturn, 1 / lengthInYears) - 1) * 100;
        }

        static void Main(string[] args)
        {
            var lines = File.ReadAllLines(DataFilePath);

            var dataPoints = new List<DataPoint>();

            foreach (string line in lines)
            {
                if (!(line.StartsWith('1') || line.StartsWith('2')))
                {
                    continue;
                }

                var fields = line.Split(',');
                string yearMonth = fields[0];
                double sharePrice = double.Parse(fields[1], CultureInfo.InvariantCulture);

                var dateFields = yearMonth.Split('-');
                int year = int.Parse(dateFields[0], CultureInfo.InvariantCulture);
                int month = int.Parse(dateFields[1], CultureInfo.InvariantCulture);

                dataPoints.Add(new DataPoint(year, month, sharePrice));
            }

            double worstResult = Double.MaxValue;
            double worstAnnualized = 0;
            double bestResult = Double.MinValue;
            double bestAnnualized = 0;
            int numberOfResults = 0;
            int negativeResults = 0;
            int positiveResults = 0;
            double averageResult = 0;
            double averageAnnualized = 0;

            for (int startAt = 0; startAt < dataPoints.Count - PurchasePeriod - HoldPeriod - SellPeriod; startAt++)
            {
                var simulation =
                    new Simulation(dataPoints, startAt, PurchasePeriod, HoldPeriod, SellPeriod, PurchaseAmount);

                var result = simulation.Run(false);
                var annualizedReturn = CalculateAnnualizedReturnInPercent(result.ResultInPercent, simulation.TotalLenghtInYears);


                if (annualizedReturn < ExpectedAnnualizedReturnInPercent)
                {
                    Console.Out.Write($"In: {simulation.FirstPurchaseDataPoint.PeriodName} - {simulation.LastPurchaseDataPoint.PeriodName} | ");
                    Console.Out.Write($"Out: {simulation.FirstSellDataPoint.PeriodName} - {simulation.LastSellDataPoint.PeriodName} | ");
                    Console.Out.Write($"Result: {result.ResultInPercent:F1}% | ");
                    Console.Out.WriteLine($"Annualized: {annualizedReturn:F1}%");
                }

                if (result.ResultInPercent < worstResult)
                {
                    worstResult = result.ResultInPercent;
                    worstAnnualized = annualizedReturn;
                }

                if (result.ResultInPercent > bestResult)
                {
                    bestResult = result.ResultInPercent;
                    bestAnnualized = annualizedReturn;
                }

                if (numberOfResults == 0)
                {
                    averageResult = result.ResultInPercent;
                    averageAnnualized = annualizedReturn;
                }
                else
                {
                    averageResult = (averageResult * numberOfResults + result.ResultInPercent) / (numberOfResults + 1);
                    averageAnnualized = CalculateAnnualizedReturnInPercent(averageResult, simulation.TotalLenghtInYears);
                }

                if (annualizedReturn < ExpectedAnnualizedReturnInPercent)
                {
                    negativeResults++;
                }
                else
                {
                    positiveResults++;
                }

                numberOfResults++;
            }

            Console.Out.WriteLine();
            Console.Out.WriteLine($@"Worst annualized result: {worstAnnualized:F1}%
Best annualized result: {bestAnnualized:F1}%
Average annualized result: {averageAnnualized:F1}%
Results not meeting expectations: {negativeResults}
Results meeting expectations: {positiveResults}");
        }
    }

    public class Simulation
    {
        public Simulation(IReadOnlyList<DataPoint> dataPoints, int startAt,
            int purchasePeriod, int holdPeriod, int sellPeriod, double purchaseAmount)
        {
            DataPoints = dataPoints ?? throw new ArgumentNullException(nameof(dataPoints));

            if (startAt + purchasePeriod + holdPeriod + sellPeriod > DataPoints.Count)
            {
                throw new ArgumentOutOfRangeException();
            }

            StartAt = startAt;
            PurchasePeriod = purchasePeriod;
            HoldPeriod = holdPeriod;
            SellPeriod = sellPeriod;
            PurchaseAmount = purchaseAmount;
        }

        public IReadOnlyList<DataPoint> DataPoints { get; }
        public int StartAt { get; }
        public int PurchasePeriod { get; }
        public int HoldPeriod { get; }
        public int SellPeriod { get; }
        public double PurchaseAmount { get; }
        public DataPoint FirstPurchaseDataPoint => DataPoints[StartAt];
        public DataPoint LastPurchaseDataPoint => DataPoints[StartAt + PurchasePeriod - 1];
        public DataPoint FirstSellDataPoint => DataPoints[StartAt + PurchasePeriod + HoldPeriod];
        public DataPoint LastSellDataPoint => DataPoints[StartAt + PurchasePeriod + HoldPeriod + SellPeriod - 1];
        public double TotalLenghtInYears => (double) (PurchasePeriod + HoldPeriod + SellPeriod) / 12;

        public SimulationResult Run(bool verbose)
        {
            var result = new SimulationResult();

            int offset = StartAt;

            for (int i = 0; i < PurchasePeriod; i++)
            {
                var dataPoint = DataPoints[offset + i];

                var sharesBought = result.Buy(dataPoint.SharePrice, PurchaseAmount);

                WriteIfVerbose(verbose, $"{sharesBought} shares bought at {dataPoint.SharePrice} per share");
            }

            offset += PurchasePeriod;

            if (HoldPeriod > 0)
            {
                offset += HoldPeriod;

                WriteIfVerbose(verbose, $"{result.ShareBalance} shares held for {HoldPeriod} months");
            }

            for (int i = 0; i < SellPeriod; i++)
            {
                var dataPoint = DataPoints[offset + i];

                var sharesSold = result.Sell(dataPoint.SharePrice, (double)1 / (SellPeriod - i));
                
                WriteIfVerbose(verbose, $"{sharesSold} shares sold at {dataPoint.SharePrice} per share");
            }

            return result;
        }

        private void WriteIfVerbose(bool verbose, string message)
        {
            if (!verbose)
            {
                return;
            }

            Console.Out.WriteLine(message);
        }
    }

    public class SimulationResult
    {
        public double ShareBalance { get; private set; }
        public double MonetaryBalance { get; private set; }
        public int BuyCount { get; private set; }
        public double AccumulatedInvestment { get; private set; }
        public int SellCount { get; private set; }
        public double AverageBuyPrice { get; private set; }
        public double AverageSellPrice { get; private set; }
        public double ResultInPercent => MonetaryBalance * 100 / AccumulatedInvestment;

        public override string ToString()
        {
            return @$"Monetary balance: {MonetaryBalance}
Share balance: {ShareBalance}
Average buy price: {AverageBuyPrice}
Average sell price: {AverageSellPrice}";
        }

        public double Buy(double sharePrice, double monetaryAmount)
        {
            AccumulatedInvestment += monetaryAmount;

            double sharesBought = monetaryAmount / sharePrice;

            ShareBalance += sharesBought;
            MonetaryBalance -= monetaryAmount;

            if (BuyCount == 0)
            {
                AverageBuyPrice = sharePrice;
            }
            else
            {
                AverageBuyPrice = (AverageBuyPrice * BuyCount + sharePrice) / (BuyCount + 1);
            }

            BuyCount++;

            return sharesBought;
        }

        public double Sell(double sharePrice, double proportionOfShareBalance)
        {
            double sharesSold = ShareBalance * proportionOfShareBalance;
            double monetaryAmount = sharesSold * sharePrice;

            ShareBalance -= sharesSold;
            MonetaryBalance += monetaryAmount;

            if (SellCount == 0)
            {
                AverageSellPrice = sharePrice;
            }
            else
            {
                AverageSellPrice = (AverageSellPrice * SellCount + sharePrice) / (SellCount + 1);
            }

            SellCount++;

            return sharesSold;
        }
    }

    public class DataPoint
    {
        public DataPoint(int year, int month, double sharePrice)
        {
            Year = year;
            Month = month;
            SharePrice = sharePrice;
        }

        public int Year { get; }
        public int Month { get; }
        public double SharePrice { get; }
        public string PeriodName => $"{Year:D4}-{Month:D2}";

        public override string ToString()
        {
            return $"{Year}-{Month} - {SharePrice}";
        }
    }
}
