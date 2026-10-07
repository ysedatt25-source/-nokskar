using System.Text.Json;

public sealed class AdminLoginGuard
{
    sealed class GuardState
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset? LockedUntilUtc { get; set; }
        public DateTimeOffset? LastFailureUtc { get; set; }
    }

    readonly object gate = new();
    readonly string file;
    GuardState state;
    public const int MaxFailures = 5;
    public static readonly TimeSpan LockDuration = TimeSpan.FromMinutes(15);

    public AdminLoginGuard(IWebHostEnvironment env, IConfiguration config)
    {
        var directory = Path.GetFullPath(config["Storage:Path"] ?? "App_Data", env.ContentRootPath);
        Directory.CreateDirectory(directory);
        file = Path.Combine(directory, "admin-login-guard.json");
        state = Load();
    }

    GuardState Load()
    {
        try
        {
            if (!File.Exists(file)) return new GuardState();
            return JsonSerializer.Deserialize<GuardState>(File.ReadAllText(file)) ?? new GuardState();
        }
        catch { return new GuardState(); }
    }

    void Persist()
    {
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, file, true);
    }

    public bool IsLocked(out TimeSpan remaining)
    {
        lock (gate)
        {
            if (state.LockedUntilUtc is { } until && until > DateTimeOffset.UtcNow)
            {
                remaining = until - DateTimeOffset.UtcNow;
                return true;
            }
            if (state.LockedUntilUtc != null)
            {
                state = new GuardState();
                Persist();
            }
            remaining = TimeSpan.Zero;
            return false;
        }
    }

    public void RegisterFailure()
    {
        lock (gate)
        {
            var now = DateTimeOffset.UtcNow;
            if (state.LastFailureUtc is { } last && now - last > TimeSpan.FromMinutes(30)) state.FailedAttempts = 0;
            state.FailedAttempts++;
            state.LastFailureUtc = now;
            if (state.FailedAttempts >= MaxFailures) state.LockedUntilUtc = now + LockDuration;
            Persist();
        }
    }

    public void RegisterSuccess()
    {
        lock (gate)
        {
            if (state.FailedAttempts == 0 && state.LockedUntilUtc == null) return;
            state = new GuardState();
            Persist();
        }
    }

    public object Status()
    {
        lock (gate)
        {
            var locked = state.LockedUntilUtc is { } until && until > DateTimeOffset.UtcNow;
            return new { failedAttempts = state.FailedAttempts, locked, lockedUntilUtc = locked ? state.LockedUntilUtc : null };
        }
    }
}
