using NUnit.Framework;

using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Plan 025: always a temp folder, never the real %AppData%\Owly.
[TestFixture]
public class ProjectRegistryTests
{
    private string _root = string.Empty;
    private ProjectRegistry _registry = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "owly-registry-tests-" + Guid.NewGuid().ToString("N"));
        _registry = new ProjectRegistry(new OwlyStore(Path.Combine(_root, "store")));
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Design(params string[] parts) => Path.Combine([_root, "designs", .. parts]);

    [Test]
    public void An_unknown_file_resolves_to_nothing()
    {
        Assert.That(_registry.Resolve(Design("pavilion_v01.3dm")), Is.Null);
        Assert.That(_registry.Resolve(""), Is.Null);
        Assert.That(_registry.Resolve(null), Is.Null);
    }

    [Test]
    public void An_assigned_file_resolves_to_its_project()
    {
        OwlyProject pavilion = _registry.Create("Pavilion")!;
        Assert.That(_registry.Assign(Design("pavilion_v01.3dm"), pavilion.Id), Is.True);

        Assert.That(_registry.Resolve(Design("pavilion_v01.3dm")), Is.EqualTo(pavilion.Id));
        Assert.That(new ProjectRegistry(new OwlyStore(Path.Combine(_root, "store"))).Resolve(Design("pavilion_v01.3dm")), Is.EqualTo(pavilion.Id));
    }

    [Test]
    public void Paths_are_compared_without_case()
    {
        OwlyProject pavilion = _registry.Create("Pavilion")!;
        _registry.Assign(Design("Pavilion_V01.3dm"), pavilion.Id);

        Assert.That(_registry.Resolve(Design("pavilion_v01.3DM").ToUpperInvariant()), Is.EqualTo(pavilion.Id));
    }

    [Test]
    public void The_suggestion_is_the_project_of_other_files_in_the_same_folder()
    {
        OwlyProject pavilion = _registry.Create("Pavilion")!;
        OwlyProject studio = _registry.Create("Studio")!;
        _registry.Assign(Design("pavilion_v01.3dm"), pavilion.Id);
        _registry.Assign(Design("pavilion_site.3dm"), pavilion.Id);
        _registry.Assign(Design("sketch.3dm"), studio.Id);
        _registry.Assign(Design("other", "tower.3dm"), studio.Id);

        Assert.That(_registry.SuggestFor(Design("pavilion_v02.3dm")), Is.EqualTo(pavilion.Id));
        Assert.That(_registry.SuggestFor(Design("other", "tower_v02.3dm")), Is.EqualTo(studio.Id));
        Assert.That(_registry.SuggestFor(Design("empty", "new.3dm")), Is.Null);
    }

    [Test]
    public void Unsorted_is_never_suggested()
    {
        _registry.Assign(Design("a.3dm"), ProjectRegistry.UnsortedId);

        Assert.That(_registry.SuggestFor(Design("b.3dm")), Is.Null);
    }

    [Test]
    public void A_project_can_be_renamed()
    {
        OwlyProject pavilion = _registry.Create("Pavilion")!;

        Assert.That(_registry.Rename(pavilion.Id, "  Summer pavilion "), Is.True);
        Assert.That(_registry.Find(pavilion.Id)!.Name, Is.EqualTo("Summer pavilion"));
        Assert.That(_registry.Rename("no-such-project", "Name"), Is.False);
    }

    [Test]
    public void Deleting_a_project_forgets_its_files()
    {
        OwlyProject pavilion = _registry.Create("Pavilion")!;
        OwlyProject studio = _registry.Create("Studio")!;
        _registry.Assign(Design("pavilion_v01.3dm"), pavilion.Id);
        _registry.Assign(Design("sketch.3dm"), studio.Id);

        Assert.That(_registry.Delete(pavilion.Id), Is.True);

        Assert.That(_registry.Find(pavilion.Id), Is.Null);
        Assert.That(_registry.Resolve(Design("pavilion_v01.3dm")), Is.Null);
        Assert.That(_registry.Resolve(Design("sketch.3dm")), Is.EqualTo(studio.Id));
        Assert.That(_registry.Assign(Design("x.3dm"), pavilion.Id), Is.False);
    }

    [Test]
    public void Unsorted_is_built_in_and_protected()
    {
        Assert.That(_registry.List().Select(p => p.Id), Is.EqualTo(new[] { ProjectRegistry.UnsortedId }));
        Assert.That(_registry.Find(ProjectRegistry.UnsortedId)!.Builtin, Is.True);
        Assert.That(_registry.Rename(ProjectRegistry.UnsortedId, "Mine"), Is.False);
        Assert.That(_registry.Delete(ProjectRegistry.UnsortedId), Is.False);
        Assert.That(_registry.Find(ProjectRegistry.UnsortedId)!.Name, Is.EqualTo("Unsorted"));

        Assert.That(_registry.Assign(Design("a.3dm"), ProjectRegistry.UnsortedId), Is.True);
        Assert.That(_registry.Resolve(Design("a.3dm")), Is.EqualTo(ProjectRegistry.UnsortedId));
    }

    [Test]
    public void Projects_are_listed_by_name_with_Unsorted_last()
    {
        _registry.Create("studio");
        _registry.Create("Pavilion");

        Assert.That(_registry.List().Select(p => p.Name), Is.EqualTo(new[] { "Pavilion", "studio", "Unsorted" }));
    }

    [TestCase("Pavilion", "Pavilion")]
    [TestCase("  Pavilion  ", "Pavilion")]
    [TestCase("", null)]
    [TestCase("   ", null)]
    [TestCase(null, null)]
    [TestCase("Two\nlines", null)]
    [TestCase("Tab\tname", null)]
    public void Names_are_trimmed_and_checked(string? name, string? expected)
    {
        Assert.That(ProjectRegistry.CleanName(name), Is.EqualTo(expected));
    }

    [Test]
    public void Names_are_at_most_sixty_characters()
    {
        Assert.That(ProjectRegistry.CleanName(new string('a', 60)), Is.Not.Null);
        Assert.That(ProjectRegistry.CleanName(new string('a', 61)), Is.Null);
        Assert.That(_registry.Create(new string('a', 61)), Is.Null);
    }

    [Test]
    public void A_corrupt_projects_file_reads_as_no_projects()
    {
        string store = Path.Combine(_root, "store");
        Directory.CreateDirectory(store);
        File.WriteAllText(Path.Combine(store, ProjectRegistry.FileName), "[1,2");

        Assert.That(_registry.List().Select(p => p.Id), Is.EqualTo(new[] { ProjectRegistry.UnsortedId }));
        Assert.That(_registry.Create("Pavilion"), Is.Not.Null);
    }

    [Test]
    public void The_file_has_the_documented_shape()
    {
        OwlyProject pavilion = _registry.Create("Pavilion")!;
        _registry.Assign(Design("pavilion_v01.3dm"), pavilion.Id);

        using JsonDocument json = JsonDocument.Parse(File.ReadAllText(Path.Combine(_root, "store", ProjectRegistry.FileName)));
        JsonElement root = json.RootElement;

        Assert.That(root.GetProperty("version").GetInt32(), Is.EqualTo(1));
        JsonElement project = root.GetProperty("projects")[0];
        Assert.That(project.GetProperty("id").GetString(), Is.EqualTo(pavilion.Id));
        Assert.That(project.GetProperty("name").GetString(), Is.EqualTo("Pavilion"));
        Assert.That(project.GetProperty("created").GetString(), Is.Not.Empty);
        Assert.That(root.GetProperty("files").GetProperty(Path.GetFullPath(Design("pavilion_v01.3dm"))).GetString(), Is.EqualTo(pavilion.Id));
    }

    // The panel fixture (rhino/panel/tests/host-events.json) pins this line.
    [Test]
    public void The_project_event_has_the_json_the_panel_fixture_pins()
    {
        string json = PanelJson.Serialize(new ProjectEvent(null, null, true, "p-1",
            new[] { new PanelProject("p-1", "Pavilion", false), new PanelProject("unsorted", "Unsorted", true) }));

        Assert.That(json, Is.EqualTo("""{"type":"project","pending":true,"suggestion":"p-1","projects":[{"id":"p-1","name":"Pavilion","builtin":false},{"id":"unsorted","name":"Unsorted","builtin":true}]}"""));
    }

    [TestCase("""{"type":"project.choose","id":"p-1"}""", typeof(ChooseProjectCommand))]
    [TestCase("""{"type":"project.create","name":"Pavilion"}""", typeof(CreateProjectCommand))]
    [TestCase("""{"type":"project.rename","id":"p-1","name":"Pavilion"}""", typeof(RenameProjectCommand))]
    [TestCase("""{"type":"project.delete","id":"p-1"}""", typeof(DeleteProjectCommand))]
    [TestCase("""{"type":"project.move","id":"p-1"}""", typeof(MoveProjectCommand))]
    public void The_project_commands_the_panel_sends_are_understood(string json, Type expected)
    {
        Assert.That(PanelJson.Deserialize(json), Is.TypeOf(expected));
    }

    [Test]
    public void A_rename_carries_both_fields()
    {
        var rename = (RenameProjectCommand)PanelJson.Deserialize("""{"type":"project.rename","id":"p-1","name":"Pavilion"}""")!;

        Assert.That(rename.Id, Is.EqualTo("p-1"));
        Assert.That(rename.Name, Is.EqualTo("Pavilion"));
    }
}
