using System;
using AutoFixture;
using Shouldly;
using Sigvardsson.Homban.Api.Services;
using Sigvardsson.Homban.Api.UnitTests.Infrastructure;
using Xunit;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class InactiveTaskSchedulerTests : TestBase<InactiveTaskScheduler>
{
    public InactiveTaskSchedulerTests()
    {
        m_fixture.Customize<Schedule>(
            c => c.FromFactory(() =>
            {
                return (m_fixture.Create<int>() % 3) switch
                {
                    0 => m_fixture.Create<OneTimeSchedule>(),
                    1 => m_fixture.Create<PeriodicScheduleFollowingActivity>(),
                    _ => m_fixture.Create<PeriodicScheduleFollowingCalendar>()
                };
            })
        );
    }
   
    [Fact]
    public void OneTimeSchedule_HasNeverBeenMovedOffTheBoard()
    {
        // Arrange
        var when = CreateSpecimen<DateTimeOffset>();
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new OneTimeSchedule(when),
            LastMovedOffTheBoardTime = null
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(when);
    }
    
    [Fact]
    public void OneTimeSchedule_HasBeenMovedOffTheBoard()
    {
        // Arrange
        var when = CreateSpecimen<DateTimeOffset>();
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new OneTimeSchedule(when),
            LastMovedOffTheBoardTime = CreateSpecimen<DateTimeOffset>()
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void PeriodicScheduleFollowingCalendar_LastMoveOnToBoardIsBeforeStartPeriod()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2023-01-03T00:00:00+00:00");
        var period = Duration.Parse("1q");
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingCalendar(start, period),
            LastMovedOnToBoardTime = start - TimeSpan.FromDays(1)
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(period.AddToDate(start));
    }
    
    [Fact]
    public void PeriodicScheduleFollowingCalendar_LastMoveOnToBoardIsAfterStartPeriod()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2023-01-03T00:00:00+00:00");
        var period = Duration.Parse("1q");
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingCalendar(start, period),
            LastMovedOnToBoardTime = start + TimeSpan.FromDays(1)
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(period.AddToDate(task.LastMovedOnToBoardTime));
    }
    
    [Fact]
    public void PeriodicScheduleFollowingActivity_NeverBeenMovedOffTheBoard()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2023-01-03T00:00:00+00:00");
        var period = Duration.Parse("1q");
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingActivity(start, period),
            LastMovedOffTheBoardTime = null
        };
        var now = DateTimeOffset.Parse("2023-01-01T21:42:49.522254+00:00");

        // Act
        var sut = CreateSut();
        var newTime = sut.ScheduleReady(task, now);

        // Assert
        newTime.ShouldBe(start);
    }
    
    [Fact]
    public void PeriodicScheduleFollowingActivity_HasBeenMovedOffTheBoardPreviouslyButBeforeStartPeriod()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2023-01-03T00:00:00+00:00");
        var period = Duration.Parse("1q");
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingActivity(start, period),
            LastMovedOffTheBoardTime = start - TimeSpan.FromDays(1)
        };
        var now = DateTimeOffset.Parse("2023-01-01T21:42:49.522254+00:00");

        // Act
        var sut = CreateSut();
        var newTime = sut.ScheduleReady(task, now);

        // Assert
        newTime.ShouldBe(start);
    }
    
    [Fact]
    public void PeriodicScheduleFollowingActivity_HasBeenMovedOffTheBoardPreviouslyButAfterStartPeriod()
    {
        // Arrange
        var start = DateTimeOffset.Parse("2023-01-03T01:01:01+00:00");
        var period = Duration.Parse("1q");
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingActivity(start, period),
            LastMovedOffTheBoardTime = start + TimeSpan.FromDays(1)
        };
        var now = DateTimeOffset.Parse("2023-01-01T21:42:49.522254+00:00");

        // Act
        var sut = CreateSut();
        var newTime = sut.ScheduleReady(task, now);

        // Assert
        newTime.ShouldBe(MidnightOf(period.AddToDate(start + TimeSpan.FromDays(1))));
    }
    
    private DateTimeOffset MidnightOf(DateTimeOffset time)
    {
        return new DateTimeOffset(year: time.Year, month: time.Month, day: time.Day, hour: 0, minute: 0, second: 0, time.Offset);
    }
}