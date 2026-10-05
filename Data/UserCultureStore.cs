using System.Text.Json;

namespace CultureSandbox.Data;

public record UserRecord(string Id, string Culture, DateTimeOffset CreatedAt);

/// <summary>
/// Fake "database" of per-user cultures backed by App_Data/users.json.
/// Deliberately has no in-memory cache: every call re-reads the file, like a DB query would.
/// </summary>
public class UserCultureStore(IHostEnvironment environment, ILogger<UserCultureStore> logger)
{
    public const string DefaultCulture = "en";

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string filePath = Path.Combine(environment.ContentRootPath, "App_Data", "users.json");
    private readonly SemaphoreSlim fileLock = new(1, 1);

    public async Task<UserRecord> GetOrCreateAsync(string userId, string? requestPath = null)
    {
        await fileLock.WaitAsync();
        try
        {
            var users = await ReadAsync();
            var user = users.FirstOrDefault(u => string.Equals(u.Id, userId, StringComparison.OrdinalIgnoreCase));
            if (user is null)
            {
                user = new UserRecord(userId, DefaultCulture, DateTimeOffset.UtcNow);
                users.Add(user);
                await WriteAsync(users);
                logger.LogInformation("Created user {User} with culture {Culture}", userId, user.Culture);
            }

            logger.LogInformation("Loaded culture {Culture} for user {User} ({Path})", user.Culture, user.Id, requestPath);
            return user;
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task SetCultureAsync(string userId, string culture)
    {
        await fileLock.WaitAsync();
        try
        {
            var users = await ReadAsync();
            var index = users.FindIndex(u => string.Equals(u.Id, userId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                users.Add(new UserRecord(userId, culture, DateTimeOffset.UtcNow));
            }
            else
            {
                users[index] = users[index] with { Culture = culture };
            }

            await WriteAsync(users);
            logger.LogInformation("Saved culture {Culture} for user {User}", culture, userId);
        }
        finally
        {
            fileLock.Release();
        }
    }

    public async Task<IReadOnlyList<UserRecord>> GetAllAsync()
    {
        await fileLock.WaitAsync();
        try
        {
            return await ReadAsync();
        }
        finally
        {
            fileLock.Release();
        }
    }

    private async Task<List<UserRecord>> ReadAsync()
    {
        if (!File.Exists(filePath)) return [];

        await using var stream = File.OpenRead(filePath);
        return await JsonSerializer.DeserializeAsync<List<UserRecord>>(stream, JsonOptions) ?? [];
    }

    private async Task WriteAsync(List<UserRecord> users)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        await using var stream = File.Create(filePath);
        await JsonSerializer.SerializeAsync(stream, users, JsonOptions);
    }
}
