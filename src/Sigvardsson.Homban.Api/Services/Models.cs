using System;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Sigvardsson.Homban.Api.Services;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable NotAccessedPositionalProperty.Global

public record Board(ImmutableArray<Task> Tasks);

public record TaskData(string Title, string Description, State State, Schedule Schedule);
public record Task(Guid Id, string Title, string Description, State State, Schedule Schedule, DateTimeOffset Created, DateTimeOffset LastChange, DateTimeOffset LastMovedOnToBoardTime, DateTimeOffset? LastMovedOffTheBoardTime) : TaskData(Title, Description, State, Schedule);

public record BoardAndTask(Board Board, Task Task);

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

public abstract record Schedule(string Type);

public record PeriodicScheduleFollowingCalendar(DateTimeOffset Start, TimeSpan Period) : Schedule("periodic-calendar");

public record PeriodicScheduleFollowingActivity(DateTimeOffset Start, TimeSpan Period) : Schedule("periodic-activity");

public record OneTimeSchedule(DateTimeOffset When) : Schedule("one-time");