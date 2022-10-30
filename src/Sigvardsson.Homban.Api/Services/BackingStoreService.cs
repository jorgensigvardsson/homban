using System;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
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
    private readonly string m_backingStorePath;
    private readonly Mutex m_mutex = new ();

    public BackingStoreService(IConfigurableJsonSerializer<StorageJsonSettings> jsonSerializer, IConfiguration configuration)
    {
        if (configuration == null) throw new ArgumentNullException(nameof(configuration));
        m_jsonSerializer = jsonSerializer ?? throw new ArgumentNullException(nameof(jsonSerializer));
        m_backingStorePath = configuration["BackingStore"] ?? throw new ApplicationException("Missing configuration: BackingStore");
    }

    public Task<Board> Get(CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            if (!File.Exists(m_backingStorePath))
                return new Board(Tasks: ImmutableArray<Task>.Empty);
            
            await using var fileStream = new FileStream(m_backingStorePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            return m_jsonSerializer.Deserialize<Board>(fileStream) ?? throw new ApplicationException("Backing store is corrupt");
        }, cancellationToken);
    }

    public System.Threading.Tasks.Task Set(Board board, CancellationToken cancellationToken)
    {
        return m_mutex.Locked(async () =>
        {
            await using var fileStream = new FileStream(m_backingStorePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            m_jsonSerializer.Serialize(fileStream, board);
        }, cancellationToken);
    }

    public void Dispose()
    {
        m_mutex.Dispose();
    }
}