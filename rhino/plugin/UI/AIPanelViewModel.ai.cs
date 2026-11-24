using System.IO;
using System.Net;
using System.Threading.Tasks;
using Rhino.UI;

using Eto.Forms;

using Rhino.Runtime;

namespace Rhino.AI.UI;

internal partial class AIPanelViewModel : IDisposable
{
    private const string PageResource = "Rhino.AI.panel.html";

    public Uri PageUrl => new(Listener.Prefixes.First());

    private sealed record ConversationSubscription(Conversation Conversation, ConversationFeed Feed, Action Handler);

    private ConversationSubscription? Subscription { get; set; }

    private ConversationFeed? Feed => Subscription?.Feed;

    private string? PinnedAgentName { get; set; }

    private Conversation? ActiveConversation =>
        Document is { } doc && AgentHost.TryFor(doc, out IAgentRunner agent) ? agent.Conversation : null;

    public void Attach()
    {
        RhinoApp.Idle += UpdateSelection;
        RhinoDoc.SelectObjects += OnSelectionChanged;
        RhinoDoc.DeselectObjects += OnSelectionChanged;
        RhinoDoc.DeselectAllObjects += OnSelectionChanged;

        ShowLive();
    }

    public void Detach()
    {
        RhinoApp.Idle -= UpdateSelection;
        RhinoDoc.SelectObjects -= OnSelectionChanged;
        RhinoDoc.DeselectObjects -= OnSelectionChanged;
        RhinoDoc.DeselectAllObjects -= OnSelectionChanged;
        
        Unsubscribe();
    }

    public void Dispose()
    {
        Detach();
        UnsubscribeProjects();
        Listener.Close();
    }

    private bool UpdatedSelected { get; set; } = false;

    private void UpdateSelection(object? sender, EventArgs e)
    {
        if (!UpdatedSelected) return;
        UpdatedSelected = false;
        SendContext();
    }
    
    private void OnSelectionChanged(object? sender, EventArgs e)
    {
        if (ChangedDocument(e)?.RuntimeSerialNumber == DocumentSerialNumber)
        {
            UpdatedSelected = true;            
        }
    }

    private static RhinoDoc? ChangedDocument(EventArgs e) => e switch
    {
        DocObjects.RhinoObjectSelectionEventArgs selection => selection.Document,
        DocObjects.RhinoDeselectAllObjectsEventArgs deselect => deselect.Document,
        _ => null,
    };

#region PAGE SERVER

    private void StartPageServer()
    {
        byte[] page = ReadPage();

        try
        {
            Listener.Start();
        }
        catch (HttpListenerException ex)
        {
            HostUtils.LogDebugEvent($"[rhino-ai] the AI panel could not start its page server: {ex.Message}.\n");
            return;
        }

        _ = ServePageAsync(page);
    }

    private static byte[] ReadPage()
    {
        using Stream? resource = typeof(AIPanelViewModel).Assembly.GetManifestResourceStream(PageResource);
        if (resource is null)
            throw new InvalidOperationException($"The panel page '{PageResource}' is missing from the plug-in.");

        using MemoryStream buffer = new();
        resource.CopyTo(buffer);
        return buffer.ToArray();
    }

    private async Task ServePageAsync(byte[] page)
    {
        while (Listener.IsListening)
        {
            HttpListenerContext context;
            try
            {
                context = await Listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpListenerException or ObjectDisposedException or InvalidOperationException)
            {
                return;
            }

            await RespondAsync(context, page).ConfigureAwait(false);
        }
    }

    private static async Task RespondAsync(HttpListenerContext context, byte[] page)
    {
        HttpListenerResponse response = context.Response;
        string route = context.Request.Url?.AbsolutePath ?? string.Empty;
        try
        {
            if (route is "/")
            {
                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = page.Length;
                await response.OutputStream.WriteAsync(page, 0, page.Length).ConfigureAwait(false);
            }
            else if (route.StartsWith(ServedImages.Route, StringComparison.Ordinal)
                && ServedImages.Resolve(route.Substring(ServedImages.Route.Length)) is { } file)
            {
                response.ContentType = ServedImages.MediaType(file);
                using FileStream bytes = File.OpenRead(file);
                response.ContentLength64 = bytes.Length;
                await bytes.CopyToAsync(response.OutputStream).ConfigureAwait(false);
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
            }
        }
        catch (Exception ex) when (ex is HttpListenerException or IOException or ObjectDisposedException or UnauthorizedAccessException)
        {
            HostUtils.LogDebugEvent($"[rhino-ai] the AI panel could not serve its page: {ex.Message}.\n");
        }
        finally
        {
            response.Close();
        }
    }

#endregion

#region FROM THE PANEL

    private void IncomingCommand(PanelCommand? command)
    {
        _ = command switch
        {
            ReadyCommand => Ready(),
            PromptCommand prompt => Prompt(prompt.Request),
            CancelCommand => CancelCurrentAgentTurn(),
            NewConversationCommand => NewConversation(),
            SelectAgentCommand select => SelectAgent(select.Name),
            LoginCommand => Login(),
            AnswerQuestionCommand answer => Answer(answer.Items),
            DismissQuestionCommand dismiss => Dismiss(dismiss.Ids),
            ToolChipCommand chip => RunToolChip(chip),
            PickAttachmentsCommand => PickAttachments(),
            OpenImageCommand open => OpenImage(open.Id),
            SaveImageCommand save => SaveImage(save.Id),
            SetZoomCommand zoom => SetZoom(zoom.Level),
            OpenSettingsCommand => OpenSettings(),
            OpenUrlCommand open => OpenUrl(open.Url),
            ClipboardCommand copy => CopyToClipboard(copy.Text),
            CopyCommandCommand chip => CopyCommand(chip.Name),
            FindToolbarCommand find => FindToolbar(find.Name, find.TurnId),
            ToolbarIconCommand icons => FindToolbarIcons(icons.Names),
            OpenMenuCommand menu => ShowMenu(menu),
            ChooseProjectCommand choose => ChooseProject(choose.Id),
            CreateProjectCommand create => CreateProject(create.Name),
            RenameProjectCommand rename => RenameProject(rename.Id, rename.Name),
            DeleteProjectCommand delete => DeleteProject(delete.Id),
            MoveProjectCommand move => MoveProject(move.Id),

            _ => false
        };
    }

    private bool Ready()
    {
        SendEnvironment();
        ShowLive();
        return true;
    }

    private bool Prompt(PromptRequest request)
    {
        string text = request.Text.Trim();
        List<Attachment> attachments = new(request.Attachments.Count);
        foreach (PanelAttachment sent in request.Attachments)
        {
            if (sent.ToAttachment() is { } attachment)
                attachments.Add(attachment);
            else
                Bridge.Post(new NoticeEvent("error", string.Format(Localization.LocalizeString("Could not attach {0}.", 3), sent.Name)));
        }

        if ((text.Length == 0 && attachments.Count == 0) || Document is not { } doc)
            return false;

        if (!AgentHost.TryFor(doc, out IAgentRunner _))
        {
            Bridge.Post(new NoticeEvent("error", Localization.LocalizeString("Owly isn't set up. Install Claude Code and sign in, or open Owly settings.", 4)));
            return false;
        }

        Resubscribe();
        AgentDispatch.PromptActive(doc, new UserMessage(text, attachments));

        // Plan 025: the question is sent either way; a file without a project gets the project card.
        if (ProjectId is null)
            SendProject();
        return true;
    }

    private bool CancelCurrentAgentTurn()
    {
        if (!AgentHost.TryFor(Document, out IAgentRunner running)) return false;
        running.Cancel();
        return true;
    }

    private bool NewConversation()
    {
        if (Document is { } doc)
        {
            AgentHost.Stop(doc);
            if (PinnedAgentName is { } pinned)
                AgentHost.SetActive(doc, pinned);
        }

        ShowLive();
        return true;
    }

    // Owly supports a single agent (ADR-0003); an old panel page may still send agent.select.
    private bool SelectAgent(string name)
    {
        HostUtils.LogDebugEvent($"[panel] agent.select '{name}' ignored: Owly has a single agent.\n");
        return false;
    }

    private bool Login()
    {
        if (!RhinoApp.IsInternetAccessAllowed) return false;
        
        if (Document is not { } doc)
            return false;
        if (!AgentHost.TryFor(doc, out IAgentRunner agent))
        {
            Bridge.Post(new NoticeEvent("error", Localization.LocalizeString("Owly isn't set up. Install Claude Code and sign in, or open Owly settings.", 8)));
            return false;
        }
        if (!AgentDispatch.TryEnsureListener(doc, out int port))
        {
            Bridge.Post(new NoticeEvent("error", Localization.LocalizeString("Owly couldn't start its connection to this document. Try reopening the panel.", 9)));
            return false;
        }

        string workingDirectory = !string.IsNullOrEmpty(doc.Path)
            ? Path.GetDirectoryName(doc.Path) ?? Path.GetTempPath()
            : Path.GetTempPath();
        _ = agent.LoginAsync($"http://localhost:{port}/agent", workingDirectory);
        return true;
    }

    private bool Answer(IReadOnlyList<QuestionAnswer> items)
    {
        List<string> ids = [];
        foreach (QuestionAnswer item in items)
            ids.Add(item.Id);

        if (Feed is null || !Feed.TryResolveQuestions(ids, out IReadOnlyList<PendingQuestion> questions) || Document is not { } doc)
            return false;

        List<string> answers = [];
        bool anyPicked = false;
        foreach (QuestionAnswer item in items)
        {
            if (item.Answers.Count > 0) anyPicked = true;
            answers.Add(string.Join(", ", item.Answers));
        }

        if (!anyPicked)
            return Dismiss(ids);

        AgentDispatch.AnswerActive(doc, UserMessage.FromText(QuestionReply.Compose(questions, answers)));
        ActiveConversation?.ClearPendingQuestions(questions);
        return true;
    }

    private bool Dismiss(IReadOnlyList<string> ids)
    {
        if (Feed is null || !Feed.TryResolveQuestions(ids, out IReadOnlyList<PendingQuestion> questions))
            return false;

        ActiveConversation?.ClearPendingQuestions(questions);
        return true;
    }

    private bool RunToolChip(ToolChipCommand chip)
    {
        if (Feed is null || !Feed.IsCallRunning(chip.CallId) || Document is not { } doc)
            return false;

        return chip.ChipId switch
        {
            _ => false,
        };
    }

    private bool PickAttachments()
    {
        Application.Instance.AsyncInvoke(() =>
        {
            IReadOnlyList<PanelAttachment> picked =
                AttachmentPicker.Pick(View, problem => Bridge.Post(new NoticeEvent("error", problem)));
            if (picked.Count > 0)
                Bridge.Post(new AttachmentsAddEvent(picked));
        });
        return true;
    }

    private static bool OpenImage(string id)
    {
        if (ServedImages.Resolve(id) is not { } path)
            return false;

        Application.Instance.Open(AsLink(path));
        return true;
    }

    private bool SaveImage(string id)
    {
        if (ServedImages.Resolve(id) is not { } path)
            return false;

        Application.Instance.AsyncInvoke(() =>
        {
            string name = Path.GetFileName(path);
            Eto.Forms.SaveFileDialog dialog = new() { Title = "Save image", FileName = name };
            dialog.Filters.Add(new FileFilter(Path.GetExtension(path).TrimStart('.').ToUpperInvariant(), Path.GetExtension(path)));

            if (dialog.ShowDialog(View) != DialogResult.Ok)
                return;

            try
            {
                File.Copy(path, dialog.FileName, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
            {
                Bridge.Post(new NoticeEvent("error", $"Could not save {name}: {ex.Message}"));
            }
        });
        return true;
    }

    private static bool SetZoom(double level)
    {
        AISettings.ZoomLevel = (int)Math.Round(level * 100);
        return true;
    }

    private bool OpenSettings()
    {
        AISettingsDialog dialog = new();
        dialog.ShowModal(View);
        SendAgents();
        SendContext();
        return true;
    }

    private static bool OpenUrl(string url)
    {
        Application.Instance.Open(AsLink(url));
        return true;
    }

    // A bare local path is not a URL and only the Windows shell will take one, so both platforms are handed a file URI.
    private static string AsLink(string url)
    {
        if (!Path.IsPathRooted(url))
            return url;

        try
        {
            return new Uri(url).AbsoluteUri;
        }
        catch (UriFormatException)
        {
            return url;
        }
    }

    private static bool CopyToClipboard(string text)
    {
        Clipboard.Instance.Text = text;
        return true;
    }

    // Plan 015 / ADR-0007: the student asked where a command is on the toolbars. Read-only lookup; plan 016 may add a flash here.
    private bool FindToolbar(string name, string turnId)
    {
        if (!CommandName.IsValid(name)) return false;
        Bridge.Post(new ToolbarFoundEvent(name, turnId, ToolbarLocator.Find(name)));
        return true;
    }

    // Plan 021 / ADR-0007: the panel shows chips for these commands. Read-only lookup of each button image. One answer
    // per valid name, even when nothing was found, so the panel never waits.
    private bool FindToolbarIcons(IReadOnlyList<string> names)
    {
        foreach (var name in names.Distinct(StringComparer.Ordinal).Take(20))
        {
            if (!CommandName.IsValid(name)) continue;
            var (light, dark) = ToolbarIcons.TryGet(name);
            Bridge.Post(new ToolbarIconFoundEvent(name, ToBase64(light), ToBase64(dark)));
        }
        return true;

        static string? ToBase64(string? svg) => svg is null ? null : Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(svg));
    }

    // Plan 014 / ADR-0007: the student clicked a command chip. Copy it and hand focus to Rhino so they can paste
    // it on the command line. Nothing is typed into Rhino and nothing runs.
    private bool CopyCommand(string name)
    {
        if (!CommandName.IsValid(name))
        {
            HostUtils.LogDebugEvent($"[owly] command.copy ignored a value that is not a command name.\n");
            return false;
        }

        CopyToClipboard(name);
        try
        {
            if (Document is { } doc) RhinoApp.SetFocusToMainWindow(doc);
            else RhinoApp.SetFocusToMainWindow();
        }
        catch (Exception ex)
        {
            HostUtils.LogDebugEvent($"[owly] could not focus Rhino after copying a command: {ex.Message}\n");
        }
        return true;
    }

    private bool ShowMenu(OpenMenuCommand menu)
    {
        PanelMenu.Show(View, menu, Bridge.Post, View.Reload);
        return true;
    }

#endregion

#region TO THE PANEL

    private void SendEnvironment()
    {
        Bridge.Post(new HelloEvent(
            new PanelHost(
                "Rhinoceros",
                RhinoApp.Version.ToString(),
                OperatingSystem.IsWindows() ? "windows" : "macos",
                Document is { } doc ? DocTitle(doc) : "Untitled",
                new PanelCapabilities(Attachments: true, ViewportCapture: true, UndoTurn: false, Grasshopper: false, PluginCommands: false)),
            PanelStrings.LanguageTag(),
            PanelStrings.Localized()));

        Bridge.Post(new ZoomEvent("set", AISettings.ZoomLevel / 100.0));

        SendAgents();
        SendContext();
        SendProject();
    }

    private void SendContext()
    {
        if (Document is null) return;
        Bridge.Post(new ContextEvent(PanelContextSource.For(Document)));
    }

    private void SendAgents()
    {
        List<PanelAgent> agents = new();
        foreach (AgentDefinition definition in AgentRegistry.Instance.AllDefinitions)
        {
            bool enabled = AISettings.IsEnabled(definition);
            string availability = !enabled ? "disabled" : !definition.Available ? "missing" : "ready";
            string model = AISettings.EffectiveModel(definition) is { Length: > 0 } chosen ? chosen : "default";
            agents.Add(new PanelAgent(
                definition.Name,
                definition.Name,
                model,
                definition.Models.FirstOrDefault(spec => spec.Id == model)?.Label() ?? model,
                availability,
                null));
        }

        string? active = Document is { } doc && AgentHost.TryFor(doc, out IAgentRunner agent)
            ? agent.Name
            : AgentRegistry.Instance.AllDefinitions.FirstOrDefault(r => r.Available && AISettings.IsEnabled(r))?.Name;

        if (active is { Length: > 0 })
            PinnedAgentName = active;

        Bridge.Post(new AgentsEvent(agents, active));
    }

    private static string DocTitle(RhinoDoc doc) =>
        string.IsNullOrEmpty(doc.Path) ? "Untitled" : Path.GetFileName(doc.Path);

#endregion

#region CONVERSATION

    private void ShowLive()
    {
        Resubscribe();
        Feed?.Replay();
    }

    private void Resubscribe()
    {
        if (ActiveConversation is not { } convo)
        {
            Unsubscribe();
            return;
        }

        // Keeping the feed matters: a replacement resets its high-water marks and re-emits the whole transcript.
        if (Subscription is { } live && ReferenceEquals(live.Conversation, convo))
            return;

        Unsubscribe();

        Action handler = () => RhinoApp.InvokeOnUiThread(new Action(PumpIfLive));
        convo.Changed += handler;
        Subscription = new ConversationSubscription(convo, new ConversationFeed(convo, Bridge.Post), handler);
    }

    private void Unsubscribe()
    {
        if (Subscription is { } live)
            live.Conversation.Changed -= live.Handler;
        Subscription = null;
    }

    private void PumpIfLive()
    {
        if (View.Loaded)
            Feed?.Pump();
    }

#endregion

}
