using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Sigvardsson.Homban.Api;

public class ApiJsonSettings : JsonSerializerSettings
{
    public ApiJsonSettings()
    {
        NullValueHandling = NullValueHandling.Ignore;
        ContractResolver = new CamelCasePropertyNamesContractResolver();
#if DEBUG
        Formatting = Formatting.Indented;
#endif
    }
}

public class StorageJsonSettings : JsonSerializerSettings
{
    public StorageJsonSettings()
    {
        NullValueHandling = NullValueHandling.Ignore;
        ContractResolver = new CamelCasePropertyNamesContractResolver();
        TypeNameHandling = TypeNameHandling.Auto;
        Formatting = Formatting.Indented;
    }
}

public static class JsonSerializerSettingsExtensions
{
    public static void CopyTo(this JsonSerializerSettings settings, MvcNewtonsoftJsonOptions o)
    {
        o.SerializerSettings.Context = settings.Context;
        o.SerializerSettings.Culture = settings.Culture;
        o.SerializerSettings.Formatting = settings.Formatting;
        o.SerializerSettings.ConstructorHandling = settings.ConstructorHandling;
        o.SerializerSettings.ContractResolver = settings.ContractResolver ?? new DefaultContractResolver();
        o.SerializerSettings.EqualityComparer = settings.EqualityComparer;
        o.SerializerSettings.MaxDepth = settings.MaxDepth;
        o.SerializerSettings.ReferenceResolverProvider = settings.ReferenceResolverProvider;
        o.SerializerSettings.SerializationBinder = settings.SerializationBinder ?? new DefaultSerializationBinder();
        o.SerializerSettings.TraceWriter = settings.TraceWriter;
        o.SerializerSettings.CheckAdditionalContent = settings.CheckAdditionalContent;
        o.SerializerSettings.DateFormatHandling = settings.DateFormatHandling;
        o.SerializerSettings.DateTimeZoneHandling = settings.DateTimeZoneHandling;
        o.SerializerSettings.DateParseHandling = settings.DateParseHandling;
        o.SerializerSettings.FloatParseHandling = settings.FloatParseHandling;
        o.SerializerSettings.FloatFormatHandling = settings.FloatFormatHandling;
        o.SerializerSettings.StringEscapeHandling = settings.StringEscapeHandling;
        o.SerializerSettings.DateFormatString = settings.DateFormatString;
        o.SerializerSettings.DefaultValueHandling = settings.DefaultValueHandling;
        o.SerializerSettings.MetadataPropertyHandling = settings.MetadataPropertyHandling;
        o.SerializerSettings.MissingMemberHandling = settings.MissingMemberHandling;
        o.SerializerSettings.NullValueHandling = settings.NullValueHandling;
        o.SerializerSettings.ObjectCreationHandling = settings.ObjectCreationHandling;
        o.SerializerSettings.PreserveReferencesHandling = settings.PreserveReferencesHandling;
        o.SerializerSettings.ReferenceLoopHandling = settings.ReferenceLoopHandling;
        o.SerializerSettings.TypeNameHandling = settings.TypeNameHandling;
        o.SerializerSettings.TypeNameAssemblyFormatHandling = settings.TypeNameAssemblyFormatHandling;
        o.SerializerSettings.Converters.Clear();
        foreach (var converter in settings.Converters)
            o.SerializerSettings.Converters.Add(converter);
    }
}