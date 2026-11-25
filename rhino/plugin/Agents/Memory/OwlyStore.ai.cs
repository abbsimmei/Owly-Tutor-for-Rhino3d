using System.IO;
using System.Text.Json.Serialization;
using System.Threading;

namespace Rhino.AI;

// Plan 025 / ADR-0009: Owly's memory as plain JSON files under one root folder (%AppData%\Owly in
// production, a temp folder in tests). Rhino 8 and Rhino 9 can run at once and share the folder, so
// every read-modify-write holds a named mutex and re-reads the file inside it, and every write goes
// to a temp file that then replaces the target in one step. Nothing here throws into Rhino: a
// failure is logged and reported as false, and a file that will not parse reads as empty.
// Rhino-free, so Server.Tests links it.
internal sealed class OwlyStore
{
    public const string MutexName = @"Local\OwlyStore";

    // Plan 026: the Claude Code CLI's fixed working directory (%AppData%\Owly\cli). Claude Code finds a
    // session only from the directory it was started in, so --resume needs this to be the same for every
    // document. A running CLI holds it open, so "Delete all Owly memory" leaves the folder itself alone.
    public const string CliFolder = "cli";

    private static readonly TimeSpan LockTimeout = TimeSpan.FromSeconds(5);

    private static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        // Enums are stored by name, so reordering TurnEventKind can't corrupt saved conversations.
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Mutex _lock;
    private readonly Action<string> _log;

    public string Root { get; }

    public static string DefaultRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Owly");

    public static string CliDirectory(string root) => Path.Combine(root, CliFolder);

    public OwlyStore(string root, Action<string>? log = null)
    {
        Root = root;
        _log = log ?? (_ => { });
        try
        {
            _lock = new Mutex(false, MutexName);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or WaitHandleCannotBeOpenedException)
        {
            // An elevated Rhino may own a mutex this one cannot open. Atomic writes still keep every
            // file whole; only a change racing the other Rhino can be lost.
            _log($"[owly] could not open the shared Owly lock: {ex.Message}");
            _lock = new Mutex(false);
        }
    }

    // A missing, empty or corrupt file reads as a fresh T.
    public T ReadJson<T>(string name) where T : class, new()
    {
        bool locked = Enter();
        try
        {
            return Read<T>(name);
        }
        finally
        {
            if (locked) _lock.ReleaseMutex();
        }
    }

    public bool WriteJson<T>(string name, T value) where T : class
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {name} was not saved.");
            return false;
        }
        try
        {
            return Write(name, value);
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // Read-modify-write under the mutex. The file is read again inside the lock, so a change another
    // Rhino made since this one last looked is kept. change returns false to leave the file as it is.
    public bool Update<T>(string name, Func<T, bool> change) where T : class, new()
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {name} was not changed.");
            return false;
        }
        try
        {
            T current = Read<T>(name);
            return change(current) && Write(name, current);
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // Like Update, but the new content is built from what the file holds now (null leaves the file as it is).
    public bool Replace<T>(string name, Func<T, T?> next) where T : class, new()
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {name} was not changed.");
            return false;
        }
        try
        {
            return next(Read<T>(name)) is { } value && Write(name, value);
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // The relative names of the sub-folders of a folder. A missing folder lists as empty.
    public IReadOnlyList<string> ListFolders(string folder)
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {folder} was not listed.");
            return [];
        }
        try
        {
            string path = PathOf(folder);
            if (!Directory.Exists(path)) return [];
            return Directory.GetDirectories(path)
                .Select(directory => Path.Combine(folder, Path.GetFileName(directory)))
                .ToList();
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            _log($"[owly] could not list {folder}: {ex.Message}");
            return [];
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // The relative names of the *.json files in a folder (never the .tmp leftovers of an interrupted
    // write). A missing folder lists as empty.
    public IReadOnlyList<string> ListJson(string folder)
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {folder} was not listed.");
            return [];
        }
        try
        {
            string path = PathOf(folder);
            if (!Directory.Exists(path)) return [];
            return Directory.GetFiles(path, "*.json")
                .Where(file => file.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                .Select(file => Path.Combine(folder, Path.GetFileName(file)))
                .ToList();
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            _log($"[owly] could not list {folder}: {ex.Message}");
            return [];
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // True when the file is gone afterwards, including when it never existed.
    public bool Delete(string name)
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {name} was not deleted.");
            return false;
        }
        try
        {
            string path = PathOf(name);
            return !File.Exists(path) || TryDelete(() => File.Delete(path), path);
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // True when the folder is gone afterwards, including when it never existed.
    public bool DeleteFolder(string folder)
    {
        if (!Enter())
        {
            _log($"[owly] could not lock the Owly folder, so {folder} was not deleted.");
            return false;
        }
        try
        {
            string path = PathOf(folder);
            return !Directory.Exists(path) || TryDelete(() => Directory.Delete(path, recursive: true), path);
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    // "Delete all Owly memory": empties the root folder but keeps the folder itself (and the cli folder inside it).
    public bool DeleteAll()
    {
        if (!Enter())
        {
            _log("[owly] could not lock the Owly folder, so nothing was deleted.");
            return false;
        }
        try
        {
            if (!Directory.Exists(Root)) return true;

            bool clean = true;
            foreach (string directory in Directory.GetDirectories(Root))
            {
                if (string.Equals(Path.GetFileName(directory), CliFolder, StringComparison.OrdinalIgnoreCase)) continue;
                clean &= TryDelete(() => Directory.Delete(directory, recursive: true), directory);
            }
            foreach (string file in Directory.GetFiles(Root))
                clean &= TryDelete(() => File.Delete(file), file);
            return clean;
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            _log($"[owly] could not empty the Owly folder: {ex.Message}");
            return false;
        }
        finally
        {
            _lock.ReleaseMutex();
        }
    }

    private T Read<T>(string name) where T : class, new()
    {
        string path = PathOf(name);
        try
        {
            if (!File.Exists(path)) return new T();
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json)) return new T();
            return JsonSerializer.Deserialize<T>(json, Options) ?? new T();
        }
        catch (JsonException ex)
        {
            _log($"[owly] {name} could not be read and is treated as empty: {ex.Message}");
            return new T();
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            _log($"[owly] {name} could not be read and is treated as empty: {ex.Message}");
            return new T();
        }
    }

    // Temp file next to the target, then one replace. net48 has no File.Move overwrite overload, so
    // an existing target goes through File.Replace and a new one through File.Move.
    private bool Write<T>(string name, T value) where T : class
    {
        string path = PathOf(name);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
            if (File.Exists(path))
                File.Replace(temp, path, null);
            else
                File.Move(temp, path);
            return true;
        }
        catch (Exception ex) when (IsFileProblem(ex) || ex is NotSupportedException)
        {
            _log($"[owly] could not save {name}: {ex.Message}");
            TryDelete(() => File.Delete(temp), temp);
            return false;
        }
    }

    private bool Enter()
    {
        try
        {
            return _lock.WaitOne(LockTimeout);
        }
        catch (AbandonedMutexException)
        {
            // The other Rhino quit while holding it. The mutex is ours now, and every write is atomic,
            // so the files are still whole.
            return true;
        }
    }

    private bool TryDelete(Action delete, string path)
    {
        try
        {
            delete();
            return true;
        }
        catch (Exception ex) when (IsFileProblem(ex))
        {
            _log($"[owly] could not delete {path}: {ex.Message}");
            return false;
        }
    }

    private string PathOf(string name) => Path.Combine(Root, name);

    private static bool IsFileProblem(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or ArgumentException;
}
