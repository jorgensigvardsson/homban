using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class InMemoryConfiguration : IConfiguration
{
    private readonly Dictionary<string, object?> m_data;

    public InMemoryConfiguration(Dictionary<string, object?> data)
    {
        m_data = data ?? throw new ArgumentNullException(nameof(data));
    }

    public IEnumerable<IConfigurationSection> GetChildren()
    {
        throw new NotImplementedException();
    }

    public IChangeToken GetReloadToken()
    {
        throw new NotImplementedException();
    }

    public IConfigurationSection GetSection(string key)
    {
        throw new NotImplementedException();
    }

    public string? this[string key]
    {
        get => m_data[key]?.ToString();
        set => m_data[key] = value;
    }
}