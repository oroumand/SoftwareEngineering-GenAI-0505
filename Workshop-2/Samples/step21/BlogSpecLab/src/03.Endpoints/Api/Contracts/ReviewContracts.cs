using System.Text.Json.Serialization;
namespace BlogSpecLab.Endpoints.Api.Contracts;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RequestChangesRequest(string? Reason);
