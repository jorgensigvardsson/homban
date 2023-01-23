using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Sigvardsson.Homban.Api.Services;

namespace Sigvardsson.Homban.Api.UnitTests.TestClasses.Services;

public class InMemoryBackingStoreService : BackingStoreService
{
    private readonly string m_jsonResource;
    private readonly ConfigurableJsonSerializer<StorageJsonSettings> m_jsonSerializer = new ConfigurableJsonSerializer<StorageJsonSettings>(new StorageJsonSettings(), Encoding.UTF8);
    
    public InMemoryBackingStoreService(string jsonResource,
                                       IConfigurableJsonSerializer<StorageJsonSettings> jsonSerializer,
                                       ILogger<BackingStoreService> logger) 
        : base(jsonSerializer, new InMemoryConfiguration(new Dictionary<string, object?> { ["BackingStore"] = "faux path" }), logger)
    {
        m_jsonResource = jsonResource ?? throw new ArgumentNullException(nameof(jsonResource));
    }

    protected override Stream OpenBackingStore()
    {
        return GetType().Assembly.GetManifestResourceStream(m_jsonResource) ?? throw new ApplicationException($"Could not find the resource {m_jsonResource}");
    }

    protected override bool BackingStoreExists() => true;
}