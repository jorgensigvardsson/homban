using System;

namespace Sigvardsson.Homban.Api.Services;

public interface IClock
{
    DateTimeOffset Now { get; }
}

public class Clock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}