using NUnit.Framework;

using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Plan 027: always a temp folder, never the real %AppData%\Owly.
[TestFixture]
public class ProjectNotesTests
{
    private const string Project = "0123456789abcdef0123456789abcdef";
    private const string Session = "3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b";

    private string _root = string.Empty;
    private OwlyStore _files = null!;
    private ProjectNotes _notes = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "owly-notes-tests-" + Guid.NewGuid().ToString("N"));
        _files = new OwlyStore(_root);
        _notes = new ProjectNotes(_files);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string NotesFile(string project) => Path.Combine(_root, "projects", project, "notes.json");

    [Test]
    public void Notes_are_listed_newest_first()
    {
        _notes.Add(Project, Session, ["first", "second"]);
        _notes.Add(Project, Session, ["third"]);

        Assert.That(_notes.List(Project).Select(n => n.Text), Is.EqualTo(new[] { "third", "first", "second" }));
    }

    [Test]
    public void A_note_has_a_guid_id_an_iso_date_and_its_session()
    {
        _notes.Add(Project, Session, ["a note"]);

        ProjectNote note = _notes.List(Project).Single();

        Assert.That(ProjectNotes.IsNoteId(note.Id), Is.True);
        Assert.That(DateTimeOffset.TryParse(note.Created, out _), Is.True);
        Assert.That(note.FromSession, Is.EqualTo(Session));
    }

    [Test]
    public void The_file_has_a_version_and_items()
    {
        _notes.Add(Project, Session, ["a note"]);

        string json = File.ReadAllText(NotesFile(Project));

        Assert.That(json, Does.Contain("\"version\": 1"));
        Assert.That(json, Does.Contain("\"items\""));
    }

    [Test]
    public void Only_the_newest_fifteen_are_kept()
    {
        for (int i = 0; i < 20; i++)
            _notes.Add(Project, Session, [$"note {i}"]);

        IReadOnlyList<ProjectNote> kept = _notes.List(Project);

        Assert.That(kept, Has.Count.EqualTo(15));
        Assert.That(kept[0].Text, Is.EqualTo("note 19"));
        Assert.That(kept[^1].Text, Is.EqualTo("note 5"));
    }

    [Test]
    public void A_note_can_be_deleted_by_id()
    {
        _notes.Add(Project, Session, ["keep", "drop"]);
        string drop = _notes.List(Project).Single(n => n.Text == "drop").Id;

        Assert.That(_notes.Delete(Project, drop), Is.True);

        Assert.That(_notes.List(Project).Select(n => n.Text), Is.EqualTo(new[] { "keep" }));
        Assert.That(_notes.Delete(Project, drop), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("..")]
    [TestCase("../evil")]
    [TestCase("ABCDEF0123456789ABCDEF0123456789")]
    [TestCase("short")]
    public void A_bad_project_id_is_refused_and_nothing_touches_the_disk(string? project)
    {
        Assert.That(_notes.Add(project, Session, ["x"]), Is.False);
        Assert.That(_notes.List(project), Is.Empty);
        Assert.That(_notes.Delete(project, Guid.NewGuid().ToString("N")), Is.False);
        Assert.That(Directory.Exists(Path.Combine(_root, "projects")), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not-an-id")]
    [TestCase("..\\..\\x")]
    public void A_bad_note_id_deletes_nothing(string? id)
    {
        _notes.Add(Project, Session, ["a"]);

        Assert.That(_notes.Delete(Project, id), Is.False);
        Assert.That(_notes.List(Project), Has.Count.EqualTo(1));
    }

    [Test]
    public void A_bad_session_id_is_refused()
    {
        Assert.That(_notes.Add(Project, "nope", ["x"]), Is.False);
        Assert.That(_notes.List(Project), Is.Empty);
    }

    [Test]
    public void A_corrupt_file_reads_as_no_notes_and_is_replaced_by_the_next_add()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(NotesFile(Project))!);
        File.WriteAllText(NotesFile(Project), "{ not json");

        Assert.That(_notes.List(Project), Is.Empty);
        Assert.That(_notes.Add(Project, Session, ["fresh"]), Is.True);
        Assert.That(_notes.List(Project).Select(n => n.Text), Is.EqualTo(new[] { "fresh" }));
    }

    [Test]
    public void Unsorted_has_notes_like_any_project()
    {
        _notes.Add(ProjectRegistry.UnsortedId, Session, ["unsorted note"]);

        Assert.That(_notes.List(ProjectRegistry.UnsortedId), Has.Count.EqualTo(1));
    }

    [Test]
    public void Deleting_a_project_removes_its_notes_file()
    {
        _notes.Add(Project, Session, ["a"]);

        Assert.That(new ConversationStore(_files).DeleteProject(Project), Is.True);

        Assert.That(File.Exists(NotesFile(Project)), Is.False);
        Assert.That(_notes.List(Project), Is.Empty);
    }

    [Test]
    public void Delete_all_removes_every_notes_file()
    {
        _notes.Add(Project, Session, ["a"]);
        _notes.Add(ProjectRegistry.UnsortedId, Session, ["b"]);

        Assert.That(_files.DeleteAll(), Is.True);

        Assert.That(File.Exists(NotesFile(Project)), Is.False);
        Assert.That(_notes.List(ProjectRegistry.UnsortedId), Is.Empty);
    }

    // The panel fixture (rhino/panel/tests/host-events.json) pins this line.
    [Test]
    public void The_notes_event_has_the_json_the_panel_fixture_pins()
    {
        string json = PanelJson.Serialize(new NotesEvent("p-1",
        [
            new PanelNote("0123456789abcdef0123456789abcdef", "Building a pavilion roof.", "2026-11-24T10:20:00.0000000+00:00"),
            new PanelNote("fedcba9876543210fedcba9876543210", "Explained _Loft.", "2026-11-23T14:40:00.0000000+00:00"),
        ]));

        Assert.That(json, Is.EqualTo("""{"type":"notes","projectId":"p-1","items":[{"id":"0123456789abcdef0123456789abcdef","text":"Building a pavilion roof.","created":"2026-11-24T10:20:00.0000000\u002B00:00"},{"id":"fedcba9876543210fedcba9876543210","text":"Explained _Loft.","created":"2026-11-23T14:40:00.0000000\u002B00:00"}]}"""));
    }

    [Test]
    public void The_delete_note_command_the_panel_sends_is_understood()
    {
        var command = (DeleteNoteCommand)PanelJson.Deserialize("""{"type":"notes.delete","id":"0123456789abcdef0123456789abcdef"}""")!;

        Assert.That(command.Id, Is.EqualTo("0123456789abcdef0123456789abcdef"));
    }
}
