using System;

namespace Mechanize;

/// <summary>
/// Shared frontier time — military 24h clocks for claim Day/Night pairs (12h apart).
/// </summary>
public static class FrontierClock
{
	public const int MinutesPerDay = 24 * 60;
	public const int HalfDayMinutes = 12 * 60;
	/// <summary>Day band for label emphasis (local claim day-clock hours).</summary>
	public const int DayBandStartHour = 6;
	public const int DayBandEndHour = 18;

	public static DateTime NowUtc => DateTime.UtcNow;

	/// <summary>Minutes since 00:00 UTC (0–1439).</summary>
	public static int MinutesOfDayUtc(DateTime? utc = null)
	{
		var t = utc ?? NowUtc;
		return t.Hour * 60 + t.Minute;
	}

	/// <summary>Day-entry clock — wall UTC minutes.</summary>
	public static int DayMinutes(DateTime? utc = null) => MinutesOfDayUtc(utc);

	/// <summary>Night-entry clock — always 12h ahead of the day clock.</summary>
	public static int NightMinutes(DateTime? utc = null) =>
		(DayMinutes(utc) + HalfDayMinutes) % MinutesPerDay;

	public static int MinutesFor(ArenaPeriod period, DateTime? utc = null) =>
		period == ArenaPeriod.Day ? DayMinutes(utc) : NightMinutes(utc);

	/// <summary>Military time without colon — e.g. 1417, 0600.</summary>
	public static string FormatMilitary(int minutesOfDay)
	{
		var m = ((minutesOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay;
		var h = m / 60;
		var min = m % 60;
		return $"{h:D2}{min:D2}";
	}

	public static string FormatMilitary(ArenaPeriod period, DateTime? utc = null) =>
		FormatMilitary(MinutesFor(period, utc));

	public static bool IsDayBandHour(int minutesOfDay)
	{
		var h = (((minutesOfDay % MinutesPerDay) + MinutesPerDay) % MinutesPerDay) / 60;
		return h >= DayBandStartHour && h < DayBandEndHour;
	}

	public static string PeriodLabel(ArenaPeriod period) =>
		period == ArenaPeriod.Day ? "DAY" : "NIGHT";
}
