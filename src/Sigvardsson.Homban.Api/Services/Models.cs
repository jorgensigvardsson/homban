using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;

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
public record PeriodicScheduleFollowingCalendar(DateTimeOffset Start, Duration Period) : Schedule("periodic-calendar");
public record PeriodicScheduleFollowingActivity(DateTimeOffset Start, Duration Period) : Schedule("periodic-activity");
public record OneTimeSchedule(DateTimeOffset When) : Schedule("one-time");

public record Duration(int Years, int HalfYears, int Quarters, int Months, int Weeks, int Days, int Hours, int Minutes, int Seconds)
{
    public readonly int Years = Years;
    public readonly int HalfYears = HalfYears;
    public readonly int Quarters = Quarters;
    public readonly int Months = Months;
    public readonly int Weeks = Weeks;
    public readonly int Days = Days;
    public readonly int Hours = Hours;
    public readonly int Minutes = Minutes;
    public readonly int Seconds = Seconds;

    public DateTimeOffset AddToDate(DateTimeOffset reference) => reference.AddSeconds(Seconds)
                                                                          .AddMinutes(Minutes)
                                                                          .AddHours(Hours)
                                                                          .AddDays(Days + Weeks * 7)
                                                                          .AddMonths(Months)
                                                                          .AddYears(Years + Quarters / 4 + HalfYears / 2);

    public TimeSpan AsTimeSpan(DateTimeOffset reference) => AddToDate(reference) - reference;

    private static readonly Regex s_regex = new(@"^((?<years>\d+)\s*y(ear(s)?)?)?\s*((?<halfYears>\d+)\s*ha(lfyear(s)?)?)?\s*((?<quarters>\d+)\s*q(uarter(s)?)?)?\s*((?<months>\d+)\s*mo(nth(s)?)?)?\s*((?<weeks>\d+)\s*w(eek(s)?)?)?\s*((?<days>\d+)\s*d(ay(s)?)?)?\s*((?<hours>\d+)\s*h(our(s)?)?)?\s*((?<minutes>\d+)\s*m(inute(s)?)?)?\s*((?<seconds>\d+)\s*s(econd(s)?)?)?\s*$",
                                                RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);
    // //
    public static Duration Parse(string text)
    {
        if (!TryParse(text, out var duration))
            throw new FormatException($"'{text}' is not a valid duration.");

        return duration;
    }

    public static bool TryParse(string text, [NotNullWhen(true)] out Duration? value)
    {
        var m = s_regex.Match(text);
        if (!m.Success)
        {
            value = null;
            return false;
        }

        try
        {
            value = new Duration(
                Years: m.Groups["years"].Success ? int.Parse(m.Groups["years"].Value) : 0,
                HalfYears: m.Groups["halfYears"].Success ? int.Parse(m.Groups["halfYears"].Value) : 0,
                Quarters: m.Groups["quarters"].Success ? int.Parse(m.Groups["quarters"].Value) : 0,
                Months: m.Groups["months"].Success ? int.Parse(m.Groups["months"].Value) : 0,
                Weeks: m.Groups["weeks"].Success ? int.Parse(m.Groups["weeks"].Value) : 0,
                Days: m.Groups["days"].Success ? int.Parse(m.Groups["days"].Value) : 0,
                Hours: m.Groups["hours"].Success ? int.Parse(m.Groups["hours"].Value) : 0,
                Minutes: m.Groups["minutes"].Success ? int.Parse(m.Groups["minutes"].Value) : 0,
                Seconds: m.Groups["seconds"].Success ? int.Parse(m.Groups["seconds"].Value) : 0
            );
            return true;
        }
        catch (FormatException)
        {
            value = null;
            return false;
        }
    }

    private void AppendUnit(StringBuilder sb, int value, string unit)
    {
        if (value > 0)
        {
            if (sb.Length > 0)
                sb.Append(' ');
            sb.Append($"{value}{unit}");
        }
    }
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        AppendUnit(sb, Years, "y");
        AppendUnit(sb, HalfYears, "ha");
        AppendUnit(sb, Quarters, "q");
        AppendUnit(sb, Months, "mo");
        AppendUnit(sb, Weeks, "w");
        AppendUnit(sb, Days, "d");
        AppendUnit(sb, Hours, "h");
        AppendUnit(sb, Minutes, "m");
        AppendUnit(sb, Seconds, "s");
        return sb.ToString();
    }
}