using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Sigvardsson.Homban.Api.Services;

[SuppressMessage("ReSharper", "UnusedTypeParameter")]
[SuppressMessage("ReSharper", "UnusedMemberInSuper.Global")]
public interface IConfigurableJsonSerializer<TOptions> where TOptions : JsonSerializerSettings
{
    T? Deserialize<T>(Stream utf8Stream);
    T? Deserialize<T>(TextReader textReader);
    T? Deserialize<T>(JsonTextReader jsonTextReader);
    void Serialize(Stream utf8Stream, object o);
    void Serialize(TextWriter textWriter, object o);
    void Serialize(JsonTextWriter jsonTextWriter, object o);
}

public class ConfigurableJsonSerializer<TOptions> : IConfigurableJsonSerializer<TOptions> where TOptions : JsonSerializerSettings
{
    private readonly Encoding m_encoding;
    private readonly JsonSerializer m_serializer;

    public ConfigurableJsonSerializer(TOptions options, Encoding encoding)
    {
        m_encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        m_serializer = new JsonSerializer
        {
            Context = options.Context,
            Culture = options.Culture,
            Formatting = options.Formatting,
            ConstructorHandling = options.ConstructorHandling,
            ContractResolver = options.ContractResolver ?? new DefaultContractResolver(),
            EqualityComparer = options.EqualityComparer,
            MaxDepth = options.MaxDepth,
            SerializationBinder = options.SerializationBinder ?? new DefaultSerializationBinder(),
            TraceWriter = options.TraceWriter,
            CheckAdditionalContent = options.CheckAdditionalContent,
            DateFormatHandling = options.DateFormatHandling,
            DateTimeZoneHandling = options.DateTimeZoneHandling,
            DateParseHandling = options.DateParseHandling,
            FloatParseHandling = options.FloatParseHandling,
            FloatFormatHandling = options.FloatFormatHandling,
            StringEscapeHandling = options.StringEscapeHandling,
            DateFormatString = options.DateFormatString,
            DefaultValueHandling = options.DefaultValueHandling,
            MetadataPropertyHandling = options.MetadataPropertyHandling,
            MissingMemberHandling = options.MissingMemberHandling,
            NullValueHandling = options.NullValueHandling,
            ObjectCreationHandling = options.ObjectCreationHandling,
            PreserveReferencesHandling = options.PreserveReferencesHandling,
            ReferenceLoopHandling = options.ReferenceLoopHandling,
            TypeNameHandling = options.TypeNameHandling,
            TypeNameAssemblyFormatHandling = options.TypeNameAssemblyFormatHandling
        };

        m_serializer.Converters.Clear();
        foreach (var converter in options.Converters)
            m_serializer.Converters.Add(converter);
    }

    public T? Deserialize<T>(Stream utf8Stream)
    {
        using var reader = new StreamReader(utf8Stream, m_encoding, leaveOpen: true);
        return Deserialize<T>(reader);
    }

    public T? Deserialize<T>(TextReader textReader)
    {
        using var reader = new JsonTextReader(textReader);
        reader.CloseInput = false;
        return Deserialize<T>(reader);
    }

    public T? Deserialize<T>(JsonTextReader jsonTextReader)
    {
        return m_serializer.Deserialize<T>(jsonTextReader);
    }

    public void Serialize(Stream utf8Stream, object o)
    {
        using var writer = new StreamWriter(utf8Stream, m_encoding, leaveOpen: true);
        Serialize(writer, o);
        writer.Flush();
    }

    public void Serialize(TextWriter textWriter, object o)
    {
        using var jsonTextWriter = new JsonTextWriter(textWriter);
        jsonTextWriter.CloseOutput = false;
        Serialize(jsonTextWriter, o);
        jsonTextWriter.Flush();
    }

    public void Serialize(JsonTextWriter jsonTextWriter, object o)
    {
        m_serializer.Serialize(jsonTextWriter, o);
    }
}