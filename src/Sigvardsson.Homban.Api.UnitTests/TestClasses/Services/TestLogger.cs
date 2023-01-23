using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper m_testOutputHelper;

    public TestLogger(ITestOutputHelper testOutputHelper)
    {
        m_testOutputHelper = testOutputHelper;
    }
    
    private class NoopDisposable : IDisposable { public void Dispose() { } }
    
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return new NoopDisposable();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        m_testOutputHelper.WriteLine($"[{logLevel:G}: {formatter(state, exception)}");
    }
}