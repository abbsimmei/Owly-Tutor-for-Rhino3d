using NUnit.Framework;

namespace Rhino.AI.Server.Tests;

// Plan 025: always a temp folder, never the real %AppData%\Owly.
[TestFixture]
public class OwlyStoreTests
{
    private string _root = string.Empty;
    private List<string> _log = [];

    private sealed class Sample
    {
        public string Name { get; set; } = string.Empty;
        public List<int> Numbers { get; set; } = [];
    }

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "owly-store-tests-" + Guid.NewGuid().ToString("N"));
        _log = [];
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private OwlyStore NewStore() => new(_root, _log.Add);

    [Test]
    public void The_default_root_is_the_Owly_folder_in_app_data()
    {
        string expected = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Owly");
        Assert.That(OwlyStore.DefaultRoot, Is.EqualTo(expected));
    }

    [Test]
    public void A_written_file_reads_back()
    {
        OwlyStore store = NewStore();
        Assert.That(store.WriteJson("sample.json", new Sample { Name = "Pavilion", Numbers = [1, 2, 3] }), Is.True);

        Sample read = NewStore().ReadJson<Sample>("sample.json");

        Assert.That(read.Name, Is.EqualTo("Pavilion"));
        Assert.That(read.Numbers, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void A_missing_file_reads_as_empty()
    {
        Sample read = NewStore().ReadJson<Sample>("missing.json");

        Assert.That(read.Name, Is.Empty);
        Assert.That(_log, Is.Empty);
    }

    [Test]
    public void Writes_leave_no_temp_files_behind()
    {
        OwlyStore store = NewStore();
        for (int i = 0; i < 5; i++)
            Assert.That(store.WriteJson("sample.json", new Sample { Name = $"v{i}" }), Is.True);

        Assert.That(Directory.GetFiles(_root).Select(Path.GetFileName), Is.EqualTo(new[] { "sample.json" }));
        Assert.That(store.ReadJson<Sample>("sample.json").Name, Is.EqualTo("v4"));
    }

    [Test]
    public void A_corrupt_file_reads_as_empty_and_is_logged()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(Path.Combine(_root, "sample.json"), "{ not json");

        Sample read = NewStore().ReadJson<Sample>("sample.json");

        Assert.That(read.Name, Is.Empty);
        Assert.That(_log, Has.Some.Contains("sample.json"));
    }

    [Test]
    public void An_update_reads_the_file_again_so_another_writer_is_kept()
    {
        OwlyStore first = NewStore();
        OwlyStore second = NewStore();
        first.WriteJson("sample.json", new Sample { Numbers = [1] });
        second.Update<Sample>("sample.json", sample => { sample.Numbers.Add(2); return true; });
        first.Update<Sample>("sample.json", sample => { sample.Numbers.Add(3); return true; });

        Assert.That(second.ReadJson<Sample>("sample.json").Numbers, Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void An_update_that_changes_nothing_writes_nothing()
    {
        OwlyStore store = NewStore();

        Assert.That(store.Update<Sample>("sample.json", _ => false), Is.False);
        Assert.That(File.Exists(Path.Combine(_root, "sample.json")), Is.False);
    }

    [Test]
    public void Delete_all_empties_the_folder_and_keeps_it()
    {
        OwlyStore store = NewStore();
        store.WriteJson("projects.json", new Sample { Name = "a" });
        store.WriteJson(Path.Combine("projects", "p1", "notes.json"), new Sample { Name = "b" });

        Assert.That(store.DeleteAll(), Is.True);

        Assert.That(Directory.Exists(_root), Is.True);
        Assert.That(Directory.GetFileSystemEntries(_root), Is.Empty);
        Assert.That(store.ReadJson<Sample>("projects.json").Name, Is.Empty);
    }

    [Test]
    public void Delete_all_without_a_folder_is_fine()
    {
        Assert.That(NewStore().DeleteAll(), Is.True);
    }
}
