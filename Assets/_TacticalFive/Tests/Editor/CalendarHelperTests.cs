using System;
using NUnit.Framework;

public class CalendarHelperTests
{
    [Test]
    public void SeasonStart_IsOctober22()
    {
        Assert.That(CalendarHelper.SeasonStart(2026), Is.EqualTo(new DateTime(2026, 10, 22)));
    }

    [Test]
    public void DateToGameDay_SeasonStart_IsDayOne()
    {
        Assert.That(CalendarHelper.DateToGameDay(2026, new DateTime(2026, 10, 22)), Is.EqualTo(1));
    }

    [Test]
    public void DateToGameDay_NextDay_IsDayTwo()
    {
        Assert.That(CalendarHelper.DateToGameDay(2026, new DateTime(2026, 10, 23)), Is.EqualTo(2));
    }

    [Test]
    public void DateToGameDay_DayBeforeSeason_IsNegative()
    {
        Assert.That(CalendarHelper.DateToGameDay(2026, new DateTime(2026, 10, 21)), Is.EqualTo(-1));
    }

    [Test]
    public void DateToGameDay_Preseason_IsNegative()
    {
        // 28/09 está 24 días antes del 22/10 → día -24
        Assert.That(CalendarHelper.DateToGameDay(2026, new DateTime(2026, 9, 28)), Is.EqualTo(-24));
    }

    [Test]
    public void DateToGameDay_EndOfYear_IsExpected()
    {
        // 31/12 son 70 días después del 22/10 → día 71
        Assert.That(CalendarHelper.DateToGameDay(2026, new DateTime(2026, 12, 31)), Is.EqualTo(71));
    }

    [Test]
    public void DateToGameDay_EndOfRegularSeason_IsExpected()
    {
        // 15/04/2027 son 175 días después del 22/10/2026 → día 176
        Assert.That(CalendarHelper.DateToGameDay(2026, new DateTime(2027, 4, 15)), Is.EqualTo(176));
    }

    [Test]
    public void DateToGameDay_UsesTheGivenYearStart()
    {
        Assert.That(CalendarHelper.DateToGameDay(2027, new DateTime(2027, 10, 22)), Is.EqualTo(1));
        Assert.That(CalendarHelper.DateToGameDay(2027, new DateTime(2027, 10, 21)), Is.EqualTo(-1));
    }

    [Test]
    public void DateToGameDay_IsMonotonic()
    {
        int previous = int.MinValue;
        var date = new DateTime(2026, 9, 20);
        for (int i = 0; i < 240; i++)
        {
            int day = CalendarHelper.DateToGameDay(2026, date);
            Assert.That(day, Is.GreaterThan(previous), $"regresión en {date:yyyy-MM-dd}");
            previous = day;
            date = date.AddDays(1);
        }
    }
}
