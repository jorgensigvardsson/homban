using System;
using System.Linq;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;

namespace Sigvardsson.Homban.Api.Controllers;

[JsonConverter(typeof(StringEnumConverter))]
public enum State
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

// ReSharper disable NotAccessedPositionalProperty.Global
public class Board
{
    public ImmutableArray<Task> Tasks { get; init; }
}

public class TaskData
{
    public string Title { get; init; }
    public string Description { get; init; }
    public State State { get; init; }
    public Schedule Schedule { get; init; }
}

public class Task : TaskData
{
    public Guid Id { get; init; }
    public DateTimeOffset Created { get; init; }
    public DateTimeOffset LastChange { get; init; }
}

public class BoardAndTask
{
    public Board Board { get; init; }
    public Task Task { get; init; }
}

[JsonConverter(typeof(ScheduleConverter))]
public abstract class Schedule
{
}

public class PeriodicScheduleFollowingCalendar : Schedule
{
    public DateTimeOffset Start { get; init; }
    public TimeSpan Period { get; init; }
}

public class PeriodicScheduleFollowingActivity : Schedule
{
    public DateTimeOffset Start { get; init; }
    public TimeSpan Period { get; init; }
}

public class OneTimeSchedule : Schedule
{
    public DateTimeOffset When { get; init; }
}

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
                writer.WriteValue(c.Period.ToString("c"));
                break;
            }
            case PeriodicScheduleFollowingActivity a:
            {
                writer.WriteValue("periodic-activity");
                writer.WritePropertyName("start");
                writer.WriteValue(a.Start.ToString("O"));
                writer.WritePropertyName("period");
                writer.WriteValue(a.Period.ToString("c"));
                break;
            }
            default:
                throw new ArgumentException($"Unknown schedule type {value?.GetType().FullName}");
        }
        writer.WriteEndObject();
    }

    public override Schedule? ReadJson(JsonReader reader, Type objectType, Schedule? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        var o = JObject.Load(reader);

        Schedule s;
        switch (o["type"]?.Value<string>())
        {
            case "one-time":
                s = new OneTimeSchedule();
                break;
            case "periodic-calendar":
                s = new PeriodicScheduleFollowingCalendar();
                break;
            case "periodic-activity":
                s = new PeriodicScheduleFollowingActivity();
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
    Services.State ToModel(State state);
    Services.Schedule ToModel(Schedule schedule);
    Board FromModel(Services.Board board);
    BoardAndTask FromModel(Services.BoardAndTask boardAndTask);
    Task FromModel(Services.Task task);
    State FromModel(Services.State state);
    Schedule FromModel(Services.Schedule schedule);
}

public class DtoMapper : IDtoMapper
{
    public Services.TaskData ToModel(TaskData taskData) => new(Title: taskData.Title, Description: taskData.Description, State: ToModel(taskData.State), Schedule: ToModel(taskData.Schedule));
    
    public Services.State ToModel(State state) =>
        state switch
        {
            State.Done => Services.State.Done,
            State.Inactive => Services.State.Inactive,
            State.Ready => Services.State.Ready,
            State.InProgress => Services.State.InProgress,
            _ => throw new ArgumentException($"Unknown state {state:G}")
        };
    
    public Services.Schedule ToModel(Schedule schedule) =>
        schedule switch
        {
            OneTimeSchedule ots => new Services.OneTimeSchedule(ots.When),
            PeriodicScheduleFollowingActivity fa => new Services.PeriodicScheduleFollowingActivity(fa.Start, fa.Period),
            PeriodicScheduleFollowingCalendar fc => new Services.PeriodicScheduleFollowingCalendar(fc.Start, fc.Period),
            _ => throw new ArgumentException($"Unknown schedule {schedule.GetType().Name}")
        };

    public Board FromModel(Services.Board board) => new Board { Tasks = board.Tasks.Select(FromModel).ToImmutableArray() };

    public BoardAndTask FromModel(Services.BoardAndTask boardAndTask) => new BoardAndTask { Board = FromModel(boardAndTask.Board), Task = FromModel(boardAndTask.Task) };

    public Task FromModel(Services.Task task) =>
        new Task {
            Id = task.Id,
            Title = task.Title,
            State = FromModel(task.State),
            Description = task.Description,
            Schedule = FromModel(task.Schedule),
            Created = task.Created,
            LastChange = task.LastChange
        };

    public State FromModel(Services.State state) =>
        state switch
        {
            Services.State.Done => State.Done,
            Services.State.Inactive => State.Inactive,
            Services.State.Ready => State.Ready,
            Services.State.InProgress => State.InProgress,
            _ => throw new ArgumentException($"Unknown state {state:G}")
        };

    public Schedule FromModel(Services.Schedule schedule) =>
        schedule switch
        {
            Services.OneTimeSchedule ots => new OneTimeSchedule { When = ots.When },
            Services.PeriodicScheduleFollowingActivity fa => new PeriodicScheduleFollowingActivity { Start = fa.Start, Period = fa.Period },
            Services.PeriodicScheduleFollowingCalendar fc => new PeriodicScheduleFollowingCalendar { Start = fc.Start, Period = fc.Period },
            _ => throw new ArgumentException($"Unknown schedule {schedule.GetType().Name}")
        };
}