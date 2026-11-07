using System;
using System.Collections.Generic;

// Forecasts are only shown when there is enough data for them to mean something.
static class PaceEstimator
{
    const double MinimumPercent = 5;
    const int MinimumSamples = 3;
    const double MinimumSpanMinutes = 15;
    const double RecentWindowMinutes = 90;
    const double SessionHours = 5;
    const double WeekHours = 7 * 24;
    const double MinimumWeekFraction = 0.02;
    const double EvenPaceTolerance = 5;
    const double BurstMinutes = 6;
    const double ShortMinutes = 15;
    const double FarFromLimitPercent = 50;
    const double NearLimitPercent = 90;
    const double BurstWeightFar = 0.25;
    const double BurstWeightNear = 0.85;

    // Usage comes in bursts. Far from the limit, the steady pace keeps one burst from predicting doom.
    // Near the limit, the last few minutes matter most, because they decide when it runs out.
    public static string DescribeSession(UsageWindow window, List<UsageSample> samples, DateTime now)
    {
        double minutesToLimit;
        double remaining;
        double rate;
        if (!Forecast(window, samples, now, out minutesToLimit, out remaining, out rate)) return null;

        if (minutesToLimit < remaining)
            return Localization.T("paceLimit", now.AddMinutes(minutesToLimit).ToString("HH:mm", Localization.Culture));
        return Localization.T("paceReset", (int)Math.Round(window.Percent.Value + rate * remaining));
    }

    // Set only when the limit would be reached before the window resets.
    public static double? MinutesToLimit(UsageWindow window, List<UsageSample> samples, DateTime now)
    {
        double minutesToLimit;
        double remaining;
        double rate;
        if (!Forecast(window, samples, now, out minutesToLimit, out remaining, out rate)) return null;
        return minutesToLimit < remaining ? (double?)minutesToLimit : null;
    }

    static bool Forecast(UsageWindow window, List<UsageSample> samples, DateTime now, out double minutesToLimit, out double remaining, out double rate)
    {
        minutesToLimit = 0;
        remaining = 0;
        rate = 0;
        if (!window.Percent.HasValue || !window.ResetsAt.HasValue) return false;
        double percent = window.Percent.Value;
        DateTime resetsAt = window.ResetsAt.Value;
        DateTime start = resetsAt.AddHours(-SessionHours);
        double elapsed = (now - start).TotalMinutes;
        remaining = (resetsAt - now).TotalMinutes;
        if (percent < MinimumPercent || elapsed <= 0 || remaining <= 0) return false;

        List<UsageSample> inWindow = samples.FindAll(delegate(UsageSample s) { return s.Time >= start; });
        if (inWindow.Count < MinimumSamples) return false;
        if ((inWindow[inWindow.Count - 1].Time - inWindow[0].Time).TotalMinutes < MinimumSpanMinutes) return false;

        double average = percent / elapsed;
        DateTime recentStart = now.AddMinutes(-RecentWindowMinutes);
        List<UsageSample> recent = inWindow.FindAll(delegate(UsageSample s) { return s.Time >= recentStart; });
        double trend = recent.Count >= MinimumSamples ? Math.Max(0, Slope(recent)) : average;
        double steady = (average + trend) / 2;
        double burst = Math.Max(RateOver(inWindow, now, BurstMinutes), RateOver(inWindow, now, ShortMinutes));
        double closeness = Math.Max(0, Math.Min(1, (percent - FarFromLimitPercent) / (NearLimitPercent - FarFromLimitPercent)));
        double burstWeight = BurstWeightFar + (BurstWeightNear - BurstWeightFar) * closeness;
        rate = steady * (1 - burstWeight) + burst * burstWeight;
        if (rate <= 0) return false;

        minutesToLimit = (100 - percent) / rate;
        return true;
    }

    public static string DescribeWeekly(UsageWindow window, DateTime now)
    {
        if (!window.Percent.HasValue || !window.ResetsAt.HasValue) return null;
        double fraction = 1 - (window.ResetsAt.Value - now).TotalHours / WeekHours;
        if (fraction < MinimumWeekFraction || fraction > 1) return null;

        double difference = window.Percent.Value - fraction * 100;
        if (Math.Abs(difference) < EvenPaceTolerance) return Localization.T("paceEven");
        int points = (int)Math.Round(Math.Abs(difference));
        return Localization.T(difference > 0 ? "paceAhead" : "paceBehind", points);
    }

    // Percentage points per minute across the last few minutes.
    static double RateOver(List<UsageSample> samples, DateTime now, double minutes)
    {
        DateTime start = now.AddMinutes(-minutes);
        List<UsageSample> recent = samples.FindAll(delegate(UsageSample s) { return s.Time >= start; });
        if (recent.Count < 2) return 0;
        UsageSample first = recent[0];
        UsageSample last = recent[recent.Count - 1];
        double span = (last.Time - first.Time).TotalMinutes;
        return span <= 0 ? 0 : Math.Max(0, (last.Session - first.Session) / span);
    }

    // Least-squares slope, in percentage points per minute.
    static double Slope(List<UsageSample> samples)
    {
        DateTime origin = samples[0].Time;
        double sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;
        foreach (UsageSample sample in samples)
        {
            double x = (sample.Time - origin).TotalMinutes;
            sumX += x;
            sumY += sample.Session;
            sumXY += x * sample.Session;
            sumXX += x * x;
        }
        double n = samples.Count;
        double denominator = n * sumXX - sumX * sumX;
        return denominator == 0 ? 0 : (n * sumXY - sumX * sumY) / denominator;
    }
}
