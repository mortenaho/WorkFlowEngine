using System.Text.Json;

namespace TaskFlow.Server;

public static class RequestExtensions
{
    public static string Actor(this HttpRequest req) => req.Headers["X-Actor-Id"].ToString();

    public static async Task<string> DecodeActor(this HttpRequest req)
    {
        var from = req.Actor();
        if (req.ContentLength is > 0)
        {
            var body = await req.ReadFromJsonAsync<ActorRequest>(JsonConfig.Options);
            if (body is not null && body.From.Length > 0)
                from = body.From;
        }
        return from;
    }

    public static async Task<T> ReadBodyOrEmpty<T>(this HttpRequest req) where T : class, new()
    {
        if (req.ContentLength is > 0)
            return await req.ReadFromJsonAsync<T>(JsonConfig.Options) ?? new T();
        return new T();
    }
}
