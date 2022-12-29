using System;
using System.Collections.Immutable;

namespace Sigvardsson.Homban.Api.Services;

// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global
// ReSharper disable NotAccessedPositionalProperty.Global

public record Board(ImmutableDictionary<Guid, Task> Tasks,  ImmutableArray<Guid> ReadyLaneTasks, ImmutableArray<Guid> InProgressLaneTasks, ImmutableArray<Guid> DoneLaneTasks, ImmutableArray<Guid> InactiveLaneTasks);
public record TaskData(string Title, string Description, Schedule Schedule);
public record Task(string Title, string Description, Schedule Schedule, DateTimeOffset Created, DateTimeOffset LastChange, DateTimeOffset LastMovedOnToBoardTime, DateTimeOffset? LastMovedOffTheBoardTime) : TaskData(Title, Description, Schedule);
public record IdentifiedTask(Guid Id, string Title, string Description, Schedule Schedule, DateTimeOffset Created, DateTimeOffset LastChange, DateTimeOffset LastMovedOnToBoardTime, DateTimeOffset? LastMovedOffTheBoardTime) : Task(Title, Description, Schedule, Created, LastChange, LastMovedOnToBoardTime, LastMovedOffTheBoardTime);
public record BoardAndTask(Board Board, IdentifiedTask Task);
public abstract record Schedule(string Type);
public record PeriodicScheduleFollowingCalendar(DateTimeOffset Start, TimeSpan Period) : Schedule("periodic-calendar");
public record PeriodicScheduleFollowingActivity(DateTimeOffset Start, TimeSpan Period) : Schedule("periodic-activity");
public record OneTimeSchedule(DateTimeOffset When) : Schedule("one-time");