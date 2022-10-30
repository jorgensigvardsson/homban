using System;
using AutoFixture;
using Shouldly;
using Sigvardsson.Homban.Api.Services;
using Xunit;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses;

public class InactiveTaskSchedulerTests
{
    private readonly Fixture m_fixture;

    public InactiveTaskSchedulerTests()
    {
        m_fixture = new Fixture();
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
        var when = m_fixture.Create<DateTimeOffset>();
        var now = m_fixture.Create<DateTimeOffset>();
        var task = m_fixture.Create<Task>() with
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
        var when = m_fixture.Create<DateTimeOffset>();
        var now = m_fixture.Create<DateTimeOffset>();
        var task = m_fixture.Create<Task>() with
        {
            Schedule = new OneTimeSchedule(when),
            LastMovedOffTheBoardTime = m_fixture.Create<DateTimeOffset>()
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
        var start = m_fixture.Create<DateTimeOffset>();
        var period = m_fixture.Create<TimeSpan>();
        var now = m_fixture.Create<DateTimeOffset>();
        var task = m_fixture.Create<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingActivity(start, period),
            LastMovedOffTheBoardTime = m_fixture.Create<DateTimeOffset>()
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(task.LastMovedOffTheBoardTime.Value + period);
    }
    
    [Fact]
    public void OneTimeSchedule_PeriodicScheduleFollowingActivity_HasNotBeenMovedOffTheBoard()
    {
        // Arrange
        var start = m_fixture.Create<DateTimeOffset>();
        var period = m_fixture.Create<TimeSpan>();
        var now = m_fixture.Create<DateTimeOffset>();
        var task = m_fixture.Create<Task>() with
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
        var start = m_fixture.Create<DateTimeOffset>();
        var period = m_fixture.Create<TimeSpan>();
        var now = m_fixture.Create<DateTimeOffset>();
        var task = m_fixture.Create<Task>() with
        {
            Schedule = new PeriodicScheduleFollowingCalendar(start, period),
            LastMovedOnToBoardTime = m_fixture.Create<DateTimeOffset>()
        };

        // Act
        var sut = new InactiveTaskScheduler();
        var result = sut.ScheduleReady(task, now);

        // Assert
        result.ShouldBe(task.LastMovedOnToBoardTime + period);
    }
}