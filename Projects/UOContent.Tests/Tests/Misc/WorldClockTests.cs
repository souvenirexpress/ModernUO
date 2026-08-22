using System;
using Server.Items;
using Xunit;

namespace Server.Tests;

public class WorldClockTests
{
    [Theory]
    [InlineData(2026, 1, 15, 12, 0, 13)]
    [InlineData(2026, 7, 15, 12, 0, 14)]
    public void ToWorldTime_UsesBerlinDaylightSaving(
        int year,
        int month,
        int day,
        int utcHour,
        int utcMinute,
        int expectedHour
    )
    {
        var utc = new DateTime(year, month, day, utcHour, utcMinute, 0, DateTimeKind.Utc);

        var local = Clock.ToWorldTime(utc);

        Assert.Equal(expectedHour, local.Hour);
        Assert.Equal(utcMinute, local.Minute);
    }

    [Theory]
    [InlineData(2, 0, LightCycle.NightLevel)]
    [InlineData(4, 0, LightCycle.NightLevel)]
    [InlineData(5, 0, 6)]
    [InlineData(6, 0, LightCycle.DayLevel)]
    [InlineData(21, 59, LightCycle.DayLevel)]
    [InlineData(23, 0, 6)]
    public void ComputeLevelAt_FollowsRealWorldHour(int hour, int minute, int expected)
    {
        Assert.Equal(expected, LightCycle.ComputeLevelAt(hour, minute));
    }
}
