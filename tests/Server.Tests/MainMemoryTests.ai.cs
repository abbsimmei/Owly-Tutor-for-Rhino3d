using NUnit.Framework;

namespace Rhino.AI.Server.Tests;

// Plan 028: always a temp folder, never the real %AppData%\Owly.
[TestFixture]
public class MainMemoryTests
{
    private string _root = string.Empty;
    private OwlyStore _files = null!;
    private MainMemory _memory = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "owly-main-tests-" + Guid.NewGuid().ToString("N"));
        _files = new OwlyStore(_root);
        _memory = new MainMemory(_files);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string ProfileFile => Path.Combine(_root, "profile.json");

    [Test]
    public void It_is_off_and_empty_when_there_is_no_file()
    {
        Assert.That(_memory.Enabled, Is.False);
        Assert.That(_memory.Items(), Is.Empty);
    }

    [Test]
    public void Merge_while_off_writes_nothing()
    {
        Assert.That(_memory.Merge(["Fillets"]), Is.False);

        Assert.That(File.Exists(ProfileFile), Is.False);
        Assert.That(_memory.Items(), Is.Empty);
    }

    [Test]
    public void Merge_after_the_switch_was_turned_off_writes_no_items()
    {
        _memory.SetEnabled(true);
        _memory.SetEnabled(false);

        Assert.That(_memory.Merge(["Fillets"]), Is.False);

        Assert.That(File.ReadAllText(ProfileFile), Does.Not.Contain("Fillets"));
        Assert.That(_memory.Items(), Is.Empty);
    }

    [Test]
    public void Merge_keeps_the_newest_first_and_removes_duplicates()
    {
        _memory.SetEnabled(true);
        _memory.Merge(["Fillets", "Booleans"]);
        _memory.Merge(["booleans ", "Trim"]);

        Assert.That(_memory.Items(), Is.EqualTo(new[] { "booleans", "Trim", "Fillets" }));
    }

    [Test]
    public void Merge_caps_at_ten_items_of_120_characters()
    {
        _memory.SetEnabled(true);
        _memory.Merge(Enumerable.Range(0, 3).Select(i => $"a{i}").ToArray());
        _memory.Merge(Enumerable.Range(0, 3).Select(i => $"b{i}").ToArray());
        _memory.Merge(Enumerable.Range(0, 3).Select(i => $"c{i}").ToArray());
        _memory.Merge([new string('x', 300), "d1", "d2"]);

        IReadOnlyList<string> items = _memory.Items();

        Assert.That(items, Has.Count.EqualTo(10));
        Assert.That(items[0], Has.Length.EqualTo(120));
        Assert.That(items, Does.Not.Contain("a2"));
    }

    [Test]
    public void The_file_has_a_version_the_switch_and_dated_items()
    {
        _memory.SetEnabled(true);
        _memory.Merge(["Fillets"]);

        string json = File.ReadAllText(ProfileFile);

        Assert.That(json, Does.Contain("\"version\": 1"));
        Assert.That(json, Does.Contain("\"enabled\": true"));
        Assert.That(json, Does.Contain("\"text\": \"Fillets\""));
        Assert.That(json, Does.Contain("\"updated\""));
    }

    [Test]
    public void Turning_it_off_clears_the_items()
    {
        _memory.SetEnabled(true);
        _memory.Merge(["Fillets"]);

        _memory.SetEnabled(false);

        Assert.That(_memory.Enabled, Is.False);
        Assert.That(_memory.Items(), Is.Empty);
        Assert.That(File.ReadAllText(ProfileFile), Does.Not.Contain("Fillets"));
    }

    [Test]
    public void Clear_empties_the_list_and_keeps_the_switch()
    {
        _memory.SetEnabled(true);
        _memory.Merge(["Fillets"]);

        _memory.Clear();

        Assert.That(_memory.Enabled, Is.True);
        Assert.That(_memory.Items(), Is.Empty);
    }

    [Test]
    public void A_corrupt_file_reads_as_off_and_empty()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(ProfileFile, "{ not json");

        Assert.That(_memory.Enabled, Is.False);
        Assert.That(_memory.Items(), Is.Empty);
        Assert.That(_memory.Merge(["Fillets"]), Is.False);
    }

    [Test]
    public void Delete_all_removes_the_profile_so_it_is_off_again()
    {
        _memory.SetEnabled(true);
        _memory.Merge(["Fillets"]);

        Assert.That(_files.DeleteAll(), Is.True);

        Assert.That(File.Exists(ProfileFile), Is.False);
        Assert.That(_memory.Enabled, Is.False);
        Assert.That(_memory.Items(), Is.Empty);
    }
}
