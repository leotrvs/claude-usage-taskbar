using System;

// Special dates when the mascot dresses up by itself, if the user has not picked an outfit.
static class Seasons
{
    const int ChristmasFromDay = 18; // December.
    const int ChristmasToDay = 26;
    const int HalloweenFromDay = 24; // October, until November 1.
    const int CarnivalDaysBefore = 50; // From the Friday before Carnival Tuesday...
    const int CarnivalTuesday = 47; // ...to the Tuesday itself, counted back from Easter.

    // The outfit picked by the user wins; the seasonal one only replaces the plain looks.
    public static MascotStyle Effective(MascotStyle chosen)
    {
        if (!Settings.SeasonalOutfits) return chosen;
        if (chosen != MascotStyle.Classic && chosen != MascotStyle.Pixel) return chosen;
        MascotStyle? special = Today(DateTime.Now);
        return special.HasValue ? special.Value : chosen;
    }

    public static MascotStyle? Today(DateTime now)
    {
        DateTime day = now.Date;
        if (day.Month == 12 && day.Day >= ChristmasFromDay && day.Day <= ChristmasToDay) return MascotStyle.Santa;
        if ((day.Month == 10 && day.Day >= HalloweenFromDay) || (day.Month == 11 && day.Day == 1)) return MascotStyle.Halloween;
        DateTime easter = Easter(day.Year);
        if (day >= easter.AddDays(-CarnivalDaysBefore) && day <= easter.AddDays(-CarnivalTuesday)) return MascotStyle.Carnival;
        return null;
    }

    // The anonymous Gregorian algorithm.
    static DateTime Easter(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int dayOfMonth = (h + l - 7 * m + 114) % 31 + 1;
        return new DateTime(year, month, dayOfMonth);
    }
}
