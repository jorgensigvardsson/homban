using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using FileStream = System.IO.FileStream;

namespace Sigvardsson.Homban.Api.Services;

public interface IBackingStoreService
{
    Task<Board> Get(CancellationToken cancellationToken);
    System.Threading.Tasks.Task Set(Board board, CancellationToken cancellationToken);
}

public class BackingStoreService : IBackingStoreService, IDisposable
{
    private readonly IConfigurableJsonSerializer<StorageJsonSettings> m_jsonSerializer;
    private readonly ILogger<BackingStoreService> m_logger;
    private readonly string m_backingStorePath;
    private readonly Mutex m_mutex = new ();

    public BackingStoreService(IConfigurableJsonSerializer<StorageJsonSettings> jsonSerializer,
                               IConfiguration configuration,
                               ILogger<BackingStoreService> logger)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        m_jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        m_logger = logger ?? throw new ArgumentNullException(nameof(logger));
        m_backingStorePath = configuration["BackingStore"] ?? throw new ApplicationException("Missing configuration: BackingStore");
    }

    public Task<Board> Get(CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            if (!File.Exists(m_backingStorePath))
                return new Board(Tasks: ImmutableDictionary<Guid, Task>.Empty, ReadyLaneTasks: ImmutableArray<Guid>.Empty, InProgressLaneTasks: ImmutableArray<Guid>.Empty, DoneLaneTasks: ImmutableArray<Guid>.Empty, InactiveLaneTasks: ImmutableArray<Guid>.Empty);
            
            await using var fileStream = new FileStream(m_backingStorePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return ToModel(m_jsonSerializer.Deserialize<BoardStorageObject>(fileStream) ?? throw new ApplicationException("Backing store is corrupt"));
        }, cancellationToken);
    }

    public System.Threading.Tasks.Task Set(Board board, CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            await using var fileStream = new FileStream(m_backingStorePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            m_jsonSerializer.Serialize(fileStream, FromModel(board));
        }, cancellationToken);
    }

    public void Dispose()
    {
        m_mutex.Dispose();
    }

    private Board ToModel(BoardStorageObject board)
    {
        var tasks = board.Tasks.ToImmutableDictionary(kvp => kvp.Key, kvp => ToModel(kvp.Value));
        
        // Sanity check - ensure the data integrity is OK
        var unreferencedTasks = new List<Guid>();
        foreach (var taskId in board.Tasks.Keys)
        {
            if (!board.ReadyLaneTasks.Contains(taskId) &&
                !board.InProgressLaneTasks.Contains(taskId) &&
                !board.DoneLaneTasks.Contains(taskId) &&
                !board.InactiveLaneTasks.Contains(taskId))
            {
                unreferencedTasks.Add(taskId);
            }
        }

        if (unreferencedTasks.Any())
        {
            m_logger.LogCritical("There are unreferenced tasks:\r\n * {TaskIds}", string.Join("\r\n * ", unreferencedTasks.Select(id => id.ToString("D"))));
            throw new ApplicationException("Unreferenced tasks found in backing store. See logs for more information.");
        }

        var nonExistentTaskIds = new List<Guid>();
        nonExistentTaskIds.AddRange(board.ReadyLaneTasks.Where(id => !tasks.ContainsKey(id)));
        nonExistentTaskIds.AddRange(board.InProgressLaneTasks.Where(id => !tasks.ContainsKey(id)));
        nonExistentTaskIds.AddRange(board.DoneLaneTasks.Where(id => !tasks.ContainsKey(id)));
        nonExistentTaskIds.AddRange(board.InactiveLaneTasks.Where(id => !tasks.ContainsKey(id)));
        
        if (nonExistentTaskIds.Any())
        {
            m_logger.LogCritical("There are non-existent tasks referenced in lanes:\r\n * {TaskIds}", string.Join("\r\n * ", nonExistentTaskIds.Select(id => id.ToString("D"))));
            throw new ApplicationException("There are non-existent tasks referenced in backing store. See logs for more information.");
        }
        
        return new Board(
            Tasks: tasks, 
            ReadyLaneTasks: board.ReadyLaneTasks.ToImmutableArray(),
            InProgressLaneTasks: board.InProgressLaneTasks.ToImmutableArray(),
            DoneLaneTasks: board.DoneLaneTasks.ToImmutableArray(),
            InactiveLaneTasks: board.InactiveLaneTasks.ToImmutableArray()
        );
    }

    private Task ToModel(TaskStorageObject task)
    {
        return new Task(
            Title: task.Title,
            Description: task.Description,
            Schedule: ToModel(task.Schedule),
            Created: task.Created,
            LastChange: task.LastChange,
            LastMovedOnToBoardTime: task.LastMovedOnToBoardTime,
            LastMovedOffTheBoardTime: task.LastMovedOffTheBoardTime
        );
    }
    
    private Schedule ToModel(ScheduleStorageObject schedule)
    {
        return schedule switch
        {
            OneTimeScheduleStorageObject ots => new OneTimeSchedule(ots.When),
            PeriodicScheduleFollowingActivityStorageObject fa => new PeriodicScheduleFollowingActivity(fa.Start, ToDuration(fa.Period)),
            PeriodicScheduleFollowingCalendarStorageObject fc => new PeriodicScheduleFollowingActivity(fc.Start, ToDuration(fc.Period)),
            _ => throw new ArgumentException($"Unknown schedule object {schedule.GetType().FullName}")
        };
    }

    private Duration ToDuration(string durationText)
    {
        // This function is transitory - it is there to handle old serialized data that
        // used "TimeSpan" for duration. This can be removed before the release after this one!
        if (TimeSpan.TryParse(durationText, out var timeSpan))
        {
            // It's an old time span! Let's convert it into a "new" duration, and be happy with that
            var years = (int) timeSpan.TotalDays / 365;
            if (years > 0)
                timeSpan = timeSpan - TimeSpan.FromDays(years * 365);

            var months = (int) timeSpan.TotalDays / 30;
            if (months > 0)
                timeSpan = timeSpan - TimeSpan.FromDays(months * 30);
            
            var halfYears = months / 6;
            if (halfYears > 0)
                months -= halfYears * 6;

            var quarters = months / 3;
            if (quarters > 0)
                months -= quarters * 3;

            var weeks = (int) timeSpan.TotalDays / 7;
            if (weeks > 0)
                timeSpan = timeSpan - TimeSpan.FromDays(weeks * 7);

            return new Duration(
                Years: years,
                HalfYears: halfYears,
                Quarters: quarters,
                Months: months,
                Weeks: weeks,
                Days: timeSpan.Days,
                Hours: timeSpan.Hours,
                Minutes: timeSpan.Minutes,
                Seconds: timeSpan.Seconds
            );
        }

        return Duration.Parse(durationText);
    }
    
    private BoardStorageObject FromModel(Board board)
    {
        return new BoardStorageObject
        {
            Tasks = board.Tasks.ToDictionary(kvp => kvp.Key, kvp => FromModel(kvp.Value)),
            ReadyLaneTasks = board.ReadyLaneTasks.ToArray(),
            InProgressLaneTasks = board.InProgressLaneTasks.ToArray(),
            DoneLaneTasks = board.DoneLaneTasks.ToArray(),
            InactiveLaneTasks = board.InactiveLaneTasks.ToArray()
        };
    }
    
    private TaskStorageObject FromModel(Task task)
    {
        return new TaskStorageObject
        {
            Title = task.Title,
            Description = task.Description,
            Schedule = FromModel(task.Schedule),
            Created = task.Created,
            LastChange = task.LastChange,
            LastMovedOnToBoardTime = task.LastMovedOnToBoardTime,
            LastMovedOffTheBoardTime = task.LastMovedOffTheBoardTime
        };
    }
    
    private ScheduleStorageObject FromModel(Schedule schedule)
    {
        return schedule switch
        {
            OneTimeSchedule ots => new OneTimeScheduleStorageObject { When = ots.When },
            PeriodicScheduleFollowingActivity fa => new PeriodicScheduleFollowingActivityStorageObject { Start = fa.Start, Period = fa.Period.ToString() },
            PeriodicScheduleFollowingCalendar fc => new PeriodicScheduleFollowingActivityStorageObject { Start = fc.Start, Period = fc.Period.ToString() },
            _ => throw new ArgumentException($"Unknown schedule object {schedule.GetType().FullName}")
        };
    }
}

public class BoardStorageObject
{
    public required Dictionary<Guid, TaskStorageObject> Tasks { get; init; }
    public required Guid[] ReadyLaneTasks { get; init; }
    public required Guid[] InProgressLaneTasks { get; init; }
    public required Guid[] DoneLaneTasks { get; init; }
    public required Guid[] InactiveLaneTasks { get; init; }
}

public class TaskStorageObject
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required ScheduleStorageObject Schedule { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastChange { get; init; }
    public DateTimeOffset LastMovedOnToBoardTime { get; init; }
    public DateTimeOffset? LastMovedOffTheBoardTime { get; init; }
}

public abstract class ScheduleStorageObject
{
}

[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global")]
[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
public class PeriodicScheduleFollowingCalendarStorageObject : ScheduleStorageObject
{
    public required DateTimeOffset Start { get; init; }
    public required string Period { get; init; }
}

public class PeriodicScheduleFollowingActivityStorageObject : ScheduleStorageObject
{
    public required DateTimeOffset Start { get; init; }
    public required string Period { get; init; }
}

public class OneTimeScheduleStorageObject : ScheduleStorageObject
{
    public required DateTimeOffset When { get; init; }
}
