using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

namespace Sigvardsson.Homban.Api.Controllers;

// ReSharper disable NotAccessedPositionalProperty.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
public class Board
{
    public required Dictionary<Guid, Task> Tasks { get; init; }
    public required Guid[] ReadyLaneTasks { get; init; }
    public required Guid[] InProgressLaneTasks { get; init; } 
    public required Guid[] DoneLaneTasks { get; init; }
    public required Guid[] InactiveLaneTasks { get; init; }
}

public class TaskData
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required Schedule Schedule { get; init; }
}

public class MoveData
{
    public Lane Lane { get; init; }
    public int Index { get; init; }
}

public class Task : TaskData
{
    public required DateTimeOffset Created { get; init; }
    public required DateTimeOffset LastChange { get; init; }
}

public class IdentifiedTask : Task
{
    public required Guid Id { get; init; }
}

public class BoardAndTask
{
    public required Board Board { get; init; }
    public required IdentifiedTask Task { get; init; }
}

[JsonConverter(typeof(ScheduleConverter))]
public abstract class Schedule
{
}

public class PeriodicScheduleFollowingCalendar : Schedule
{
    public required DateTimeOffset Start { get; init; }
    public required string Period { get; init; }
}

public class PeriodicScheduleFollowingActivity : Schedule
{
    public required DateTimeOffset Start { get; init; }
    public required string Period { get; init; }
}

public class OneTimeSchedule : Schedule
{
    public required DateTimeOffset When { get; init; }
}

// ReSharper restore UnusedAutoPropertyAccessor.Global
// ReSharper restore NotAccessedPositionalProperty.Global
class ScheduleConverter : JsonConverter<Schedule>
{
    public override void WriteJson(JsonWriter writer, Schedule? value, JsonSerializer serializer)
    {
        writer.WriteStartObject();
        writer.WritePropertyName("type");
        switch (value)
        {
            case OneTimeSchedule ots:
            {
                writer.WriteValue("one-time");
                writer.WritePropertyName("when");
                writer.WriteValue(ots.When.ToString("O"));
                break;
            }
            case PeriodicScheduleFollowingCalendar c:
            {
                writer.WriteValue("periodic-calendar");
                writer.WritePropertyName("start");
                writer.WriteValue(c.Start.ToString("O"));
                writer.WritePropertyName("period");
                writer.WriteValue(c.Period);
                break;
            }
            case PeriodicScheduleFollowingActivity a:
            {
                writer.WriteValue("periodic-activity");
                writer.WritePropertyName("start");
                writer.WriteValue(a.Start.ToString("O"));
                writer.WritePropertyName("period");
                writer.WriteValue(a.Period);
                break;
            }
            default:
                throw new ArgumentException($"Unknown schedule type {value?.GetType().FullName}");
        }
        writer.WriteEndObject();
    }

    public override Schedule ReadJson(JsonReader reader, Type objectType, Schedule? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var o = JObject.Load(reader);

        Schedule s;
        switch (o["type"]?.Value<string>())
        {
            case "one-time":
                s = new OneTimeSchedule { When = DateTimeOffset.MinValue };
                break;
            case "periodic-calendar":
                s = new PeriodicScheduleFollowingCalendar { Start = DateTimeOffset.MinValue, Period = "" };
                break;
            case "periodic-activity":
                s = new PeriodicScheduleFollowingActivity { Start = DateTimeOffset.MinValue, Period = "" };
                break;
            default:
                throw new ArgumentException($"Unknown schedule type '{o["type"]?.Value<string>()}");
        }

        serializer.Populate(o.CreateReader(), s);
        return s;
    }
}

public interface IDtoMapper
{
    Services.TaskData ToModel(TaskData taskData);
    Services.Schedule ToModel(Schedule schedule);
    Services.Lane ToModel(Lane lane);
    Board FromModel(Services.Board board);
    BoardAndTask FromModel(Services.BoardAndTask boardAndTask);
    Task FromModel(Services.Task task);
    Schedule FromModel(Services.Schedule schedule);
}

public enum Lane
{
    [EnumMember(Value="inactive")]
    Inactive,
    
    [EnumMember(Value="ready")]
    Ready,
    
    [EnumMember(Value="in-progress")]
    InProgress,
    
    [EnumMember(Value="done")]
    Done
} 

public class DtoMapper : IDtoMapper
{
    public Services.TaskData ToModel(TaskData taskData) => new(Title: taskData.Title, Description: taskData.Description, Schedule: ToModel(taskData.Schedule));
    
    public Services.Lane ToModel(Lane lane) =>
        lane switch
        {
            Lane.Done => Services.Lane.Done,
            Lane.Inactive => Services.Lane.Inactive,
            Lane.Ready => Services.Lane.Ready,
            Lane.InProgress => Services.Lane.InProgress,
            _ => throw new ArgumentException($"Unknown lane {lane:G}")
        };
    
    public Services.Schedule ToModel(Schedule schedule) =>
        schedule switch
        {
            OneTimeSchedule ots => new Services.OneTimeSchedule(ots.When),
            PeriodicScheduleFollowingActivity fa => new Services.PeriodicScheduleFollowingActivity(fa.Start, Services.Duration.Parse(fa.Period)),
            PeriodicScheduleFollowingCalendar fc => new Services.PeriodicScheduleFollowingCalendar(fc.Start, Services.Duration.Parse(fc.Period)),
            _ => throw new ArgumentException($"Unknown schedule {schedule.GetType().Name}")
        };

    public Board FromModel(Services.Board board) => new ()
    {
        Tasks = board.Tasks.ToDictionary(kvp => kvp.Key, kvp => FromModel(kvp.Value)),
        ReadyLaneTasks = board.ReadyLaneTasks.ToArray(),
        InProgressLaneTasks = board.InProgressLaneTasks.ToArray(),
        DoneLaneTasks = board.DoneLaneTasks.ToArray(),
        InactiveLaneTasks = board.InactiveLaneTasks.ToArray()
    };

    public BoardAndTask FromModel(Services.BoardAndTask boardAndTask) => new() { Board = FromModel(boardAndTask.Board), Task = FromModel(boardAndTask.Task) };

    public Task FromModel(Services.Task task) => new()
    {
        Title = task.Title,
        Description = task.Description,
        Schedule = FromModel(task.Schedule),
        Created = task.Created,
        LastChange = task.LastChange
    };
    
    public IdentifiedTask FromModel(Services.IdentifiedTask task) => new()
    {
        Id = task.Id,
        Title = task.Title,
        Description = task.Description,
        Schedule = FromModel(task.Schedule),
        Created = task.Created,
        LastChange = task.LastChange
    };

    public Schedule FromModel(Services.Schedule schedule) =>
        schedule switch
        {
            Services.OneTimeSchedule ots => new OneTimeSchedule { When = ots.When },
            Services.PeriodicScheduleFollowingActivity fa => new PeriodicScheduleFollowingActivity { Start = fa.Start, Period = fa.Period.ToString() },
            Services.PeriodicScheduleFollowingCalendar fc => new PeriodicScheduleFollowingCalendar { Start = fc.Start, Period = fc.Period.ToString() },
            _ => throw new ArgumentException($"Unknown schedule {schedule.GetType().Name}")
        };
}