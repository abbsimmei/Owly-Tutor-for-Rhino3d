using System.IO;

namespace Rhino.AI;

// A project as the panel and the registry see it. Unsorted is built in: it is never stored, and it
// can't be renamed or deleted.
internal sealed record OwlyProject(string Id, string Name, string Created)
{
    public bool Builtin => Id == ProjectRegistry.UnsortedId;
}

// Plan 025 / ADR-0009: the projects the student named, and which .3dm paths belong to which one.
// Kept in projects.json in the Owly folder; nothing is written into the .3dm. Every call reads the
// file again, because another Rhino may have changed it. Rhino-free, so Server.Tests links it.
internal sealed class ProjectRegistry
{
    public const string FileName = "projects.json";
    public const string UnsortedId = "unsorted";
    public const int MaxNameLength = 60;

    private readonly OwlyStore _store;
    private readonly string _unsortedName;

    public ProjectRegistry(OwlyStore store, string unsortedName = "Unsorted")
    {
        _store = store;
        _unsortedName = unsortedName;
    }

    public OwlyProject Unsorted => new(UnsortedId, _unsortedName, string.Empty);

    // The student's projects by name, then Unsorted.
    public IReadOnlyList<OwlyProject> List()
    {
        List<OwlyProject> projects = Load().Projects
            .Where(p => p.Id.Length > 0 && p.Id != UnsortedId)
            .Select(p => new OwlyProject(p.Id, p.Name, p.Created))
            .OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        projects.Add(Unsorted);
        return projects;
    }

    public OwlyProject? Find(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return List().FirstOrDefault(p => p.Id == id);
    }

    // The project this file belongs to, or null when Owly doesn't know the file.
    public string? Resolve(string? path)
    {
        if (Key(path) is not { } key) return null;
        ProjectsFile file = Load();
        return file.Files.TryGetValue(key, out string? id) && Exists(file, id) ? id : null;
    }

    // The project most other known files in the same folder belong to. Unsorted is never suggested.
    public string? SuggestFor(string? path)
    {
        if (Key(path) is not { } key || Folder(key) is not { } folder) return null;
        ProjectsFile file = Load();
        return file.Files
            .Where(entry => !string.Equals(entry.Key, key, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Folder(entry.Key), folder, StringComparison.OrdinalIgnoreCase)
                && entry.Value != UnsortedId
                && Exists(file, entry.Value))
            .GroupBy(entry => entry.Value)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();
    }

    public OwlyProject? Create(string? name)
    {
        if (CleanName(name) is not { } clean) return null;
        OwlyProject project = new(Guid.NewGuid().ToString("N"), clean, DateTime.UtcNow.ToString("o"));
        bool saved = Change(file =>
        {
            file.Projects.Add(new ProjectEntry { Id = project.Id, Name = project.Name, Created = project.Created });
            return true;
        });
        return saved ? project : null;
    }

    public bool Rename(string? id, string? name)
    {
        if (id is null or UnsortedId || CleanName(name) is not { } clean) return false;
        return Change(file =>
        {
            ProjectEntry? entry = file.Projects.FirstOrDefault(p => p.Id == id);
            if (entry is null) return false;
            entry.Name = clean;
            return true;
        });
    }

    // Removes the project and forgets which files belonged to it, so they are unknown again.
    public bool Delete(string? id)
    {
        if (id is null or UnsortedId) return false;
        return Change(file =>
        {
            if (file.Projects.RemoveAll(p => p.Id == id) == 0) return false;
            foreach (string path in file.Files.Where(entry => entry.Value == id).Select(entry => entry.Key).ToList())
                file.Files.Remove(path);
            return true;
        });
    }

    public bool Assign(string? path, string? id)
    {
        if (Key(path) is not { } key || string.IsNullOrEmpty(id)) return false;
        return Change(file =>
        {
            if (!Exists(file, id!)) return false;
            file.Files[key] = id!;
            return true;
        });
    }

    // Trimmed, 1-60 characters, no control characters; null when the name can't be used.
    public static string? CleanName(string? name)
    {
        string trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length is 0 or > MaxNameLength) return null;
        return trimmed.Any(char.IsControl) ? null : trimmed;
    }

    private ProjectsFile Load() => Normalised(_store.ReadJson<ProjectsFile>(FileName));

    // Read again under the store's lock, tidied, changed, written back.
    private bool Change(Func<ProjectsFile, bool> change) =>
        _store.Update<ProjectsFile>(FileName, file => change(Normalised(file)));

    private static bool Exists(ProjectsFile file, string id) =>
        id == UnsortedId || file.Projects.Any(p => p.Id == id);

    // A full path, so "pavilion_v01.3dm" and ".\pavilion_v01.3dm" are one file. The dictionary
    // compares it case-insensitively.
    private static string? Key(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException or System.Security.SecurityException)
        {
            return null;
        }
    }

    private static string? Folder(string path)
    {
        try
        {
            return Path.GetDirectoryName(path);
        }
        catch (Exception ex) when (ex is ArgumentException or PathTooLongException)
        {
            return null;
        }
    }

    // System.Text.Json builds an ordinary dictionary and may leave lists null, so the file is tidied
    // before anyone reads it: case-insensitive paths, no empty entries.
    private static ProjectsFile Normalised(ProjectsFile file)
    {
        file.Projects = (file.Projects ?? []).Where(p => p is not null && !string.IsNullOrEmpty(p.Id)).ToList();
        foreach (ProjectEntry project in file.Projects)
        {
            project.Name ??= string.Empty;
            project.Created ??= string.Empty;
        }

        Dictionary<string, string> files = new(StringComparer.OrdinalIgnoreCase);
        foreach (KeyValuePair<string, string> entry in file.Files ?? new Dictionary<string, string>())
        {
            if (!string.IsNullOrEmpty(entry.Key) && !string.IsNullOrEmpty(entry.Value))
                files[entry.Key] = entry.Value;
        }
        file.Files = files;
        return file;
    }

    // The on-disk shape of projects.json.
    internal sealed class ProjectsFile
    {
        public int Version { get; set; } = 1;
        public List<ProjectEntry> Projects { get; set; } = [];
        public Dictionary<string, string> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class ProjectEntry
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Created { get; set; } = string.Empty;
    }
}
