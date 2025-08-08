using System.Globalization;

namespace Hasm.Extensions;

public static class TimeSpanExtensions
{
    public static bool TimeOfDayBetween(this TimeSpan timeSpan, string startTime, string endTime, bool includeBoundary)
    {
        var start = TimeSpan.ParseExact(startTime, "h\\:mm", CultureInfo.InvariantCulture);
        var end = TimeSpan.ParseExact(endTime, "h\\:mm", CultureInfo.InvariantCulture);
        return TimeOfDayBetween(timeSpan, start, end, includeBoundary);
    }

    public static bool TimeOfDayBetween(this TimeSpan timeSpan, TimeSpan startTime, TimeSpan endTime, bool includeBoundary)
    {
        if (startTime.Hours > 24 || startTime.Hours < 0 || (startTime.Hours == 24 && startTime.Minutes > 0))
        {
            throw new ArgumentException("startTime");
        }
        if (endTime.Hours > 24 || endTime.Hours < 0 || (endTime.Hours == 24 && endTime.Minutes > 0))
        {
            throw new ArgumentException("endTime");
        }

        if (includeBoundary && (timeSpan == startTime || timeSpan == endTime))
        {
            return true;
        }

        return ((startTime < endTime && timeSpan > startTime && timeSpan < endTime)
             || (startTime > endTime && (timeSpan > startTime || timeSpan < endTime)));
    }


}
