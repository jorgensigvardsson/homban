using System;

namespace Sigvardsson.Homban.Api.Services;

public interface IInactiveTaskScheduler
{
    DateTimeOffset? ScheduleReady(Task task, DateTimeOffset now);
}

public class InactiveTaskScheduler : IInactiveTaskScheduler
{
    public DateTimeOffset? ScheduleReady(Task task, DateTimeOffset now)
    {
        switch (task.Schedule)
        {
            case OneTimeSchedule ots:
                if (task.LastMovedOffTheBoardTime == null)
                    return ots.When;
                break;
            case PeriodicScheduleFollowingActivity fa:
                if (task.LastMovedOffTheBoardTime == null || task.LastMovedOffTheBoardTime.Value < fa.Start)
                    return Normalize(fa.Start, fa.Period);

                return Normalize(fa.Period.AddToDate(task.LastMovedOffTheBoardTime.Value), fa.Period);
            case PeriodicScheduleFollowingCalendar fc:
                return Normalize(fc.Period.AddToDate(task.LastMovedOnToBoardTime > fc.Start ? task.LastMovedOnToBoardTime : fc.Start), fc.Period);
            default:
                throw new ApplicationException($"Unknown schedule: {task.Schedule.GetType().Name}");
        }

        return null;
    }

    private DateTimeOffset Normalize(DateTimeOffset time, Duration d)
    {
        return d.HasTimePart ? time : MidnightOf(time);
    }
    
    private DateTimeOffset MidnightOf(DateTimeOffset time)
    {
        return new DateTimeOffset(year: time.Year, month: time.Month, day: time.Day, hour: 0, minute: 0, second: 0, time.Offset);
    }
}