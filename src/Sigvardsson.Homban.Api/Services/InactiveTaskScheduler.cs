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
                if (task.LastMovedOffTheBoardTime != null)
                    return fa.Period.AddToDate(task.LastMovedOffTheBoardTime.Value);
                break;
            case PeriodicScheduleFollowingCalendar fc:
                return fc.Period.AddToDate(task.LastMovedOnToBoardTime);
            default:
                throw new ApplicationException($"Unknown schedule: {task.Schedule.GetType().Name}");
        }

        return null;
    }
}