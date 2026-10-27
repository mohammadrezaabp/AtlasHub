using System.Text.Json;
namespace Hub.Application.Common;

public static class JsonDefaults { public static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web); }
