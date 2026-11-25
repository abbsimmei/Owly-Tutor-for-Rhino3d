using NUnit.Framework;

using Rhino.AI.UI;

namespace Rhino.AI.Server.Tests;

// Plan 026: always a temp folder, never the real %AppData%\Owly.
[TestFixture]
public class ConversationStoreTests
{
    private string _root = string.Empty;
    private ConversationStore _store = null!;
    private OwlyStore _files = null!;
    private const string Project = "0123456789abcdef0123456789abcdef";
    private const string OtherProject = "fedcba9876543210fedcba9876543210";

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "owly-conversation-tests-" + Guid.NewGuid().ToString("N"));
        _files = new OwlyStore(_root);
        _store = new ConversationStore(_files);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private static Conversation Talk(string? projectId, params string[] prompts)
    {
        Conversation convo = new(Guid.NewGuid(), "claude", "pavilion.3dm") { ProjectId = projectId };
        foreach (string prompt in prompts)
        {
            convo.BeginTurn(prompt);
            convo.Record(TurnEventKind.AssistantText, "Use _Loft. ");
            convo.Record(TurnEventKind.ToolUse, "get_commands", "{\"name\":\"Loft\"}", "{}", "call-1");
            convo.Record(TurnEventKind.AssistantText, "It joins curves.");
            convo.CompleteTurn();
        }
        return convo;
    }

    private string FolderOf(string project) => Path.Combine(_root, "projects", project, "conversations");

    private List<string> Files(string project) =>
        Directory.Exists(FolderOf(project)) ? Directory.GetFiles(FolderOf(project)).Select(f => Path.GetFileName(f)).ToList() : [];

    [Test]
    public void A_saved_conversation_loads_back_with_its_turns_and_names_for_enums()
    {
        Conversation convo = Talk(Project, "How do I loft?");
        convo.Record(TurnEventKind.AssistantText, "ignored: no current turn");
        Assert.That(_store.Save(convo), Is.True);

        ConversationDto? dto = _store.Load(Project, convo.AgentSessionId.ToString());

        Assert.That(dto, Is.Not.Null);
        Assert.That(dto!.SessionId, Is.EqualTo(convo.AgentSessionId.ToString("D")));
        Assert.That(dto.ProjectId, Is.EqualTo(Project));
        Assert.That(dto.Title, Is.EqualTo("How do I loft?"));
        Assert.That(dto.Turns, Has.Count.EqualTo(1));
        Assert.That(dto.Turns[0].Events.Select(e => e.Kind), Is.EqualTo(new[] { TurnEventKind.AssistantText, TurnEventKind.ToolUse, TurnEventKind.AssistantText }));
        Assert.That(dto.Turns[0].Events[1].Id, Is.EqualTo("call-1"));
        string json = File.ReadAllText(Path.Combine(FolderOf(Project), convo.AgentSessionId.ToString("D") + ".json"));
        Assert.That(json, Does.Contain("\"ToolUse\"").Or.Contain("\"toolUse\""));
        Assert.That(json, Does.Not.Contain("\"kind\": 1"));
    }

    [Test]
    public void Attachments_and_usage_survive_the_round_trip()
    {
        Conversation convo = new(Guid.NewGuid(), "claude", "doc");
        convo.BeginTurn("", [new AttachmentInfo(AttachmentKind.Image, "view.png", "image/png", 1234)]);
        convo.RecordUsage(new TokenUsage(10, 20, 0.5m));
        convo.CompleteTurn();
        _store.Save(convo);

        ConversationDto dto = _store.Load(ProjectRegistry.UnsortedId, convo.AgentSessionId.ToString())!;

        Assert.That(dto.Turns[0].Attachments![0], Is.EqualTo(new AttachmentInfo(AttachmentKind.Image, "view.png", "image/png", 1234)));
        Assert.That(dto.Turns[0].Usage, Is.EqualTo(new TokenUsage(10, 20, 0.5m)));
        Assert.That(dto.Title, Is.EqualTo("Conversation"));
    }

    [Test]
    public void A_restored_conversation_has_the_saved_turns_id_and_title()
    {
        Conversation convo = Talk(Project, "How do I loft?", "And a sweep?");
        _store.Save(convo);

        Conversation restored = Conversation.Restore(_store.Load(Project, convo.AgentSessionId.ToString())!);

        Assert.That(restored.AgentSessionId, Is.EqualTo(convo.AgentSessionId));
        Assert.That(restored.Title, Is.EqualTo("How do I loft?"));
        Assert.That(restored.ProjectId, Is.EqualTo(Project));
        Assert.That(restored.Turns.Select(t => t.Prompt), Is.EqualTo(new[] { "How do I loft?", "And a sweep?" }));
        Assert.That(restored.Turns.All(t => t.Completed), Is.True);
        Assert.That(restored.SavedAs, Is.EqualTo((convo.AgentSessionId, Project)));
    }

    [Test]
    public void The_list_is_newest_first_and_skips_corrupt_and_temp_files()
    {
        Conversation older = Talk(Project, "older");
        Thread.Sleep(20);
        Conversation newer = Talk(Project, "newer");
        _store.Save(older);
        _store.Save(newer);
        Directory.CreateDirectory(FolderOf(Project));
        File.WriteAllText(Path.Combine(FolderOf(Project), Guid.NewGuid().ToString("D") + ".json"), "{ not json");
        File.WriteAllText(Path.Combine(FolderOf(Project), newer.AgentSessionId.ToString("D") + ".json.abc.tmp"), "{}");

        IReadOnlyList<ConversationSummary> list = _store.List(Project);

        Assert.That(list.Select(c => c.Title), Is.EqualTo(new[] { "newer", "older" }));
        Assert.That(list.Select(c => c.SessionId), Is.EqualTo(new[] { newer.AgentSessionId.ToString("D"), older.AgentSessionId.ToString("D") }));
    }

    [Test]
    public void The_list_is_per_project()
    {
        _store.Save(Talk(Project, "a"));
        _store.Save(Talk(OtherProject, "b"));

        Assert.That(_store.List(Project).Select(c => c.Title), Is.EqualTo(new[] { "a" }));
        Assert.That(_store.List(OtherProject).Select(c => c.Title), Is.EqualTo(new[] { "b" }));
        Assert.That(_store.List(ProjectRegistry.UnsortedId), Is.Empty);
    }

    [Test]
    public void A_conversation_with_no_turns_is_not_saved()
    {
        Conversation empty = new(Guid.NewGuid(), "claude", "doc") { ProjectId = Project };

        Assert.That(_store.Save(empty), Is.False);
        Assert.That(Directory.Exists(_root), Is.False);
    }

    [Test]
    public void A_conversation_without_a_project_is_filed_under_unsorted()
    {
        Conversation convo = Talk(null, "hello");
        Assert.That(_store.Save(convo), Is.True);

        Assert.That(_store.List(ProjectRegistry.UnsortedId), Has.Count.EqualTo(1));
        Assert.That(Files(ProjectRegistry.UnsortedId), Has.Count.EqualTo(1));
    }

    [Test]
    public void Moving_to_another_project_leaves_one_file()
    {
        Conversation convo = Talk(null, "hello");
        _store.Save(convo);

        convo.ProjectId = Project;
        Assert.That(_store.Save(convo), Is.True);

        Assert.That(Files(ProjectRegistry.UnsortedId), Is.Empty);
        Assert.That(Files(Project), Has.Count.EqualTo(1));
        Assert.That(_store.List(Project), Has.Count.EqualTo(1));
    }

    [Test]
    public void A_changed_session_id_leaves_one_file()
    {
        Conversation convo = Talk(Project, "hello");
        _store.Save(convo);
        Guid old = convo.AgentSessionId;

        convo.AdoptSessionId(Guid.NewGuid());
        Assert.That(_store.Save(convo), Is.True);

        Assert.That(Files(Project).ToList(), Is.EqualTo(new[] { convo.AgentSessionId.ToString("D") + ".json" }));
        Assert.That(_store.Load(Project, old.ToString()), Is.Null);
    }

    [Test]
    public void A_discarded_conversation_is_never_saved_again()
    {
        Conversation convo = Talk(Project, "hello");
        _store.Save(convo);

        _store.Discard(convo);
        convo.BeginTurn("another");
        convo.CompleteTurn();

        Assert.That(_store.Save(convo), Is.False);
        Assert.That(Files(Project), Is.Empty);
    }

    [Test]
    public void Rename_changes_the_title_and_checks_it()
    {
        Conversation convo = Talk(Project, "How do I loft?");
        _store.Save(convo);
        string id = convo.AgentSessionId.ToString();

        Assert.That(_store.Rename(Project, id, "  Loft questions  "), Is.True);
        Assert.That(_store.List(Project)[0].Title, Is.EqualTo("Loft questions"));

        Assert.That(_store.Rename(Project, id, "   "), Is.False);
        Assert.That(_store.Rename(Project, id, new string('x', 81)), Is.False);
        Assert.That(_store.Rename(Project, id, "two\nlines"), Is.False);
        Assert.That(_store.Rename(Project, id, new string('x', 80)), Is.True);
        Assert.That(_store.List(Project)[0].Title, Has.Length.EqualTo(80));
    }

    [Test]
    public void Renaming_something_that_is_not_saved_creates_nothing()
    {
        Assert.That(_store.Rename(Project, Guid.NewGuid().ToString(), "Name"), Is.False);
        Assert.That(Files(Project), Is.Empty);
    }

    [Test]
    public void The_title_is_the_first_line_of_the_first_question_cut_at_80()
    {
        Assert.That(Conversation.TitleFrom("\n  \n  How do I loft?  \nsecond line", "x"), Is.EqualTo("How do I loft?"));
        Assert.That(Conversation.TitleFrom(new string('a', 200), "x"), Has.Length.EqualTo(80));
        Assert.That(Conversation.TitleFrom("", "Fallback"), Is.EqualTo("Fallback"));
        Assert.That(Conversation.TitleFrom(new string('a', 79) + "\U0001F989", "x"), Has.Length.EqualTo(79));
    }

    [Test]
    public void Delete_removes_one_conversation()
    {
        Conversation keep = Talk(Project, "keep");
        Conversation drop = Talk(Project, "drop");
        _store.Save(keep);
        _store.Save(drop);

        Assert.That(_store.Delete(Project, drop.AgentSessionId.ToString()), Is.True);

        Assert.That(_store.List(Project).Select(c => c.Title), Is.EqualTo(new[] { "keep" }));
    }

    [Test]
    public void Delete_project_removes_its_folder_only()
    {
        _store.Save(Talk(Project, "a"));
        _store.Save(Talk(OtherProject, "b"));

        Assert.That(_store.DeleteProject(Project), Is.True);

        Assert.That(Directory.Exists(Path.Combine(_root, "projects", Project)), Is.False);
        Assert.That(_store.List(OtherProject), Has.Count.EqualTo(1));
    }

    [Test]
    public void Unsorted_is_never_deleted_as_a_project()
    {
        _store.Save(Talk(null, "a"));

        Assert.That(_store.DeleteProject(ProjectRegistry.UnsortedId), Is.False);
        Assert.That(_store.List(ProjectRegistry.UnsortedId), Has.Count.EqualTo(1));
    }

    [TestCase("..\\x")]
    [TestCase("../x")]
    [TestCase("")]
    [TestCase("not-a-guid")]
    [TestCase("0123456789ABCDEF0123456789ABCDEF")]
    [TestCase("0123456789abcdef")]
    [TestCase("0123456789abcdef0123456789abcdeg")]
    public void Bad_project_ids_are_refused_and_touch_nothing(string project)
    {
        string session = Guid.NewGuid().ToString();
        Conversation convo = Talk(project, "hello");

        Assert.That(_store.Save(convo), Is.False);
        Assert.That(_store.List(project), Is.Empty);
        Assert.That(_store.Load(project, session), Is.Null);
        Assert.That(_store.Rename(project, session, "x"), Is.False);
        Assert.That(_store.Delete(project, session), Is.False);
        Assert.That(_store.DeleteProject(project), Is.False);
        Assert.That(Directory.Exists(_root), Is.False);
    }

    [TestCase("..\\x")]
    [TestCase("../../projects")]
    [TestCase("")]
    [TestCase("not-a-guid")]
    [TestCase(null)]
    public void Bad_session_ids_are_refused(string? session)
    {
        _store.Save(Talk(Project, "a"));

        Assert.That(_store.Load(Project, session), Is.Null);
        Assert.That(_store.Rename(Project, session, "x"), Is.False);
        Assert.That(_store.Delete(Project, session), Is.False);
        Assert.That(_store.List(Project), Has.Count.EqualTo(1));
    }

    [Test]
    public void Delete_all_keeps_the_cli_folder()
    {
        string cli = OwlyStore.CliDirectory(_root);
        Directory.CreateDirectory(cli);
        File.WriteAllText(Path.Combine(cli, "held-open.txt"), "x");
        _store.Save(Talk(Project, "a"));
        _files.WriteJson("projects.json", new ProjectRegistry.ProjectsFile());

        Assert.That(_files.DeleteAll(), Is.True);

        Assert.That(Directory.Exists(cli), Is.True);
        Assert.That(Directory.Exists(Path.Combine(_root, "projects")), Is.False);
        Assert.That(File.Exists(Path.Combine(_root, "projects.json")), Is.False);
    }

    [Test]
    public void The_cli_directory_is_under_the_owly_root()
    {
        Assert.That(OwlyStore.CliDirectory(_root), Is.EqualTo(Path.Combine(_root, "cli")));
    }

    // The panel fixture (rhino/panel/tests/host-events.json) pins this line.
    [Test]
    public void The_conversations_event_has_the_json_the_panel_fixture_pins()
    {
        string json = PanelJson.Serialize(new ConversationsEvent("p-1",
        [
            new PanelConversationItem("3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b", "How do I loft between curves?", new DateTimeOffset(2026, 11, 24, 10, 15, 0, TimeSpan.Zero).ToString("O"), false),
            new PanelConversationItem("7d9e0a1b-2c3d-4e5f-8a9b-0c1d2e3f4a5b", "Join or Group?", new DateTimeOffset(2026, 11, 23, 14, 30, 0, TimeSpan.Zero).ToString("O"), true),
            new PanelConversationItem("a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d", "How do I make a wall with openings?", new DateTimeOffset(2026, 11, 20, 9, 5, 0, TimeSpan.Zero).ToString("O"), false),
        ]));

        Assert.That(json, Is.EqualTo("""{"type":"conversations","projectId":"p-1","items":[{"sessionId":"3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b","title":"How do I loft between curves?","startedAt":"2026-11-24T10:15:00.0000000\u002B00:00","current":false},{"sessionId":"7d9e0a1b-2c3d-4e5f-8a9b-0c1d2e3f4a5b","title":"Join or Group?","startedAt":"2026-11-23T14:30:00.0000000\u002B00:00","current":true},{"sessionId":"a1b2c3d4-e5f6-4a7b-8c9d-0e1f2a3b4c5d","title":"How do I make a wall with openings?","startedAt":"2026-11-20T09:05:00.0000000\u002B00:00","current":false}]}"""));
    }

    [TestCase("""{"type":"conversation.open","sessionId":"3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b"}""", typeof(OpenConversationCommand))]
    [TestCase("""{"type":"conversation.rename","sessionId":"3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b","title":"Lofts"}""", typeof(RenameConversationCommand))]
    [TestCase("""{"type":"conversation.delete","sessionId":"3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b"}""", typeof(DeleteConversationCommand))]
    public void The_conversation_commands_the_panel_sends_are_understood(string json, Type expected)
    {
        Assert.That(PanelJson.Deserialize(json), Is.TypeOf(expected));
    }

    [Test]
    public void A_rename_carries_the_id_and_the_title()
    {
        var rename = (RenameConversationCommand)PanelJson.Deserialize("""{"type":"conversation.rename","sessionId":"abc","title":"Lofts"}""")!;

        Assert.That(rename.SessionId, Is.EqualTo("abc"));
        Assert.That(rename.Title, Is.EqualTo("Lofts"));
    }

    // ---- Plan 027: notes bookkeeping lives on disk, never in the live Conversation.

    private string Id(Conversation convo) => convo.AgentSessionId.ToString("D");

    [Test]
    public void A_later_save_keeps_the_notes_bookkeeping_the_file_has()
    {
        Conversation convo = Talk(Project, "How do I loft?");
        _store.Save(convo);
        _store.MarkNotesPending(Project, Id(convo));
        _store.FinishNotes(Project, Id(convo), 1);

        convo.BeginTurn("And a sweep?");
        convo.CompleteTurn();
        Assert.That(_store.Save(convo), Is.True);

        ConversationDto dto = _store.Load(Project, Id(convo))!;
        Assert.That(dto.NotesPending, Is.True);
        Assert.That(dto.NotesThroughTurn, Is.EqualTo(1));
        Assert.That(dto.Turns, Has.Count.EqualTo(2));
    }

    [Test]
    public void A_project_move_carries_the_notes_bookkeeping_to_the_new_file()
    {
        Conversation convo = Talk(Project, "How do I loft?");
        _store.Save(convo);
        _store.MarkNotesPending(Project, Id(convo));
        _store.FinishNotes(Project, Id(convo), 1);

        convo.ProjectId = OtherProject;
        Assert.That(_store.Save(convo), Is.True);

        ConversationDto moved = _store.Load(OtherProject, Id(convo))!;
        Assert.That(moved.NotesPending, Is.True);
        Assert.That(moved.NotesThroughTurn, Is.EqualTo(1));
        Assert.That(_store.Load(Project, Id(convo)), Is.Null);
    }

    [Test]
    public void A_pending_job_is_claimed_once()
    {
        Conversation convo = Talk(Project, "a", "b");
        _store.Save(convo);
        Assert.That(_store.MarkNotesPending(Project, Id(convo)), Is.True);

        Assert.That(_store.TryClaimNotes(Project, Id(convo), out ConversationDto claimed), Is.True);
        Assert.That(claimed.Turns, Has.Count.EqualTo(2));
        Assert.That(_store.TryClaimNotes(Project, Id(convo), out _), Is.False);
        Assert.That(_store.ListPending(), Is.Empty);
    }

    [Test]
    public void A_released_job_can_be_claimed_again()
    {
        Conversation convo = Talk(Project, "a");
        _store.Save(convo);
        _store.MarkNotesPending(Project, Id(convo));
        _store.TryClaimNotes(Project, Id(convo), out _);

        Assert.That(_store.ReleaseNotes(Project, Id(convo)), Is.True);

        Assert.That(_store.TryClaimNotes(Project, Id(convo), out _), Is.True);
    }

    [Test]
    public void A_job_that_was_never_marked_is_not_claimed()
    {
        Conversation convo = Talk(Project, "a");
        _store.Save(convo);

        Assert.That(_store.TryClaimNotes(Project, Id(convo), out _), Is.False);
    }

    [Test]
    public void A_deleted_conversation_cannot_be_claimed_or_marked_and_leaves_no_file()
    {
        Conversation convo = Talk(Project, "a");
        _store.Save(convo);
        _store.MarkNotesPending(Project, Id(convo));
        _store.Delete(Project, Id(convo));

        Assert.That(_store.TryClaimNotes(Project, Id(convo), out _), Is.False);
        Assert.That(_store.MarkNotesPending(Project, Id(convo)), Is.False);
        Assert.That(Files(Project), Is.Empty);
    }

    [Test]
    public void Finishing_stores_how_many_turns_the_notes_cover_and_keeps_pending_as_it_is()
    {
        Conversation convo = Talk(Project, "a", "b", "c");
        _store.Save(convo);
        _store.MarkNotesPending(Project, Id(convo));

        Assert.That(_store.FinishNotes(Project, Id(convo), 3), Is.True);

        ConversationDto dto = _store.Load(Project, Id(convo))!;
        Assert.That(dto.NotesThroughTurn, Is.EqualTo(3));
        Assert.That(dto.NotesPending, Is.True);
    }

    [Test]
    public void The_pending_list_spans_projects_and_includes_unsorted()
    {
        Conversation inProject = Talk(Project, "a");
        Conversation inOther = Talk(OtherProject, "b");
        Conversation inUnsorted = Talk(null, "c");
        Conversation notPending = Talk(Project, "d");
        foreach (Conversation convo in new[] { inProject, inOther, inUnsorted, notPending })
            _store.Save(convo);
        _store.MarkNotesPending(Project, Id(inProject));
        _store.MarkNotesPending(OtherProject, Id(inOther));
        _store.MarkNotesPending(ProjectRegistry.UnsortedId, Id(inUnsorted));

        var pending = _store.ListPending();

        Assert.That(pending, Is.EquivalentTo(new[]
        {
            (Project, Id(inProject)),
            (OtherProject, Id(inOther)),
            (ProjectRegistry.UnsortedId, Id(inUnsorted)),
        }));
    }

    [Test]
    public void The_pending_list_is_empty_before_anything_is_saved()
    {
        Assert.That(_store.ListPending(), Is.Empty);
    }

    [TestCase(null, "3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b")]
    [TestCase("../x", "3f2b8c1e-5a4d-4e6f-9b7a-1c2d3e4f5a6b")]
    [TestCase("0123456789abcdef0123456789abcdef", "nope")]
    public void Bad_ids_are_refused_by_the_notes_methods(string? project, string session)
    {
        Assert.That(_store.MarkNotesPending(project, session), Is.False);
        Assert.That(_store.TryClaimNotes(project, session, out _), Is.False);
        Assert.That(_store.FinishNotes(project, session, 1), Is.False);
        Assert.That(_store.ReleaseNotes(project, session), Is.False);
    }
}
