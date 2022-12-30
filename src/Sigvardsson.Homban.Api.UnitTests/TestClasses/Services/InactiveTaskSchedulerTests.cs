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
    public void OneTimeSchedule_OneTimeSchedule_HasNeverBeenMovedOffTheBoard()
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
    public void OneTimeSchedule_OneTimeSchedule_HasBeenMovedOffTheBoard()
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
    public void OneTimeSchedule_PeriodicScheduleFollowingActivity_HasBeenMovedOffTheBoard()
    {
        // Arrange
        var start = CreateSpecimen<DateTimeOffset>();
        var period = CreateSpecimen<Duration>();
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingActivity(start, period),
            LastMovedOffTheBoardTime = CreateSpecimen<DateTimeOffset>()
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(period.AddToDate(task.LastMovedOffTheBoardTime.Value));
    }
    
    [Fact]
    public void OneTimeSchedule_PeriodicScheduleFollowingActivity_HasNotBeenMovedOffTheBoard()
    {
        // Arrange
        var start = CreateSpecimen<DateTimeOffset>();
        var period = CreateSpecimen<Duration>();
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingActivity(start, period),
            LastMovedOffTheBoardTime = null
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBeNull();
    }
    
    [Fact]
    public void OneTimeSchedule_PeriodicScheduleFollowingCalendar()
    {
        // Arrange
        var start = CreateSpecimen<DateTimeOffset>();
        var period = CreateSpecimen<Duration>();
        var now = CreateSpecimen<DateTimeOffset>();
        var task = CreateSpecimen<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingCalendar(start, period),
            LastMovedOnToBoardTime = CreateSpecimen<DateTimeOffset>()
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(period.AddToDate(task.LastMovedOnToBoardTime));
    }
}