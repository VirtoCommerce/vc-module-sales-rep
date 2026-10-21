using System;
using System.Collections.Generic;
using System.Linq;

namespace VirtoCommerce.SalesRep.Tests.StorefrontE2E.Infrastructure;

/// <summary>
/// New York time, the zone the time-sensitive groups pin their browser to, so an expected day or instant never depends
/// on the machine's zone. Today is resolved once per run, as the groups' seeds are.
/// </summary>
internal static class NewYork
{
    public const string TimeZoneId = "America/New_York";

    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);

    public static DateTime Today { get; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone).Date;

    /// <summary>The instant of a New York time of day.</summary>
    public static DateTime At(DateTime day, int hour, int minute = 0, int second = 0)
        => TimeZoneInfo.ConvertTimeToUtc(day.Add(new TimeSpan(hour, minute, second)), Zone);

    /// <summary>The instant of a time of day the clocks pass twice, on its first pass: still daylight time.</summary>
    public static DateTime AtFirstPass(DateTime day, int hour, int minute) => AtPass(day, hour, minute, Enumerable.Max);

    /// <summary>The instant of a time of day the clocks pass twice, on its second pass: standard time.</summary>
    public static DateTime AtSecondPass(DateTime day, int hour, int minute) => AtPass(day, hour, minute, Enumerable.Min);

    private static DateTime AtPass(DateTime day, int hour, int minute, Func<IEnumerable<TimeSpan>, TimeSpan> pickOffset)
    {
        // Throws unless the time happens twice that day; the larger offset is the earlier instant.
        var local = day.Add(new TimeSpan(hour, minute, 0));
        return DateTime.SpecifyKind(local - pickOffset(Zone.GetAmbiguousTimeOffsets(local)), DateTimeKind.Utc);
    }

    /// <summary>The next day on which a time of day does not exist: the clocks skip the hour it falls in.</summary>
    public static DateTime NextDaySkipping(int hour, int minute) => NextDay(day => Zone.IsInvalidTime(day.Add(new TimeSpan(hour, minute, 0))));

    /// <summary>The next day on which a time of day happens twice: the clocks go back over it.</summary>
    public static DateTime NextDayRepeating(int hour, int minute) => NextDay(day => Zone.IsAmbiguousTime(day.Add(new TimeSpan(hour, minute, 0))));

    private static DateTime NextDay(Func<DateTime, bool> matches)
    {
        for (var day = Today.AddDays(1); day < Today.AddYears(2); day = day.AddDays(1))
        {
            if (matches(day))
            {
                return day;
            }
        }

        throw new InvalidOperationException($"{TimeZoneId} changes its clocks on no day in the next two years.");
    }
}
