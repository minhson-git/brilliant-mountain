using System;
using System.Text.Json.Serialization;
using ZAnnotation.IOCore;

namespace IOApp.Features
{
    [DataSaver]
    public partial class AppData
    {
        public partial AppData() { }

        public string OutputPath { get; set => Set(ref field, value); } = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    [JsonSerializable(typeof(AppData))]
    partial class AppDataJsonContext : JsonSerializerContext { }
}