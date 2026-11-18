using Rhino.Display;
using Rhino.DocObjects;
using Rhino.Geometry;

using Rhino.AI.Resources;

namespace Rhino.AI.Tools;

// One-shot grounding snapshot: selection + active viewport + doc summary in a single round-trip, so the agent can orient before acting without
// chaining get_selection / list_objects / view calls. Pull-only, read-only.
[McpServerToolType]
internal static partial class GetContextTool
{
    public sealed record SelectedObject(string Id, string Name, string Layer, string Type);

    public sealed record CameraSummary(
        double[] Location,
        double[] Target,
        double[] Up,
        double LensLength,
        string Projection);

    public sealed record ViewportSummary(string Name, string DisplayMode, CameraSummary Camera);

    public sealed record DocSummary(int ObjectCount, int LayerCount);

    public sealed record ContextSnapshot(
        IEnumerable<SelectedObject> Selection,
        ViewportSummary? ActiveViewport,
        DocSummary Document,
        // Per-section failures, so one throwing section never nukes the snapshot.
        string[]? Warnings);

    [McpServerTool("get_context", "Get Context Snapshot", true, false)]
    [Description("One round-trip grounding snapshot of current state: the active-doc selection (ids/types/layers), the active viewport (name + camera summary), and a doc summary (object/layer counts). Read-only; pulls everything you need to orient in one call.")]
    public static IToolResult GetContext(RhinoDoc doc)
    {
        List<string> warnings = [];

        SelectedObject[] selection = Try(() => SelectionOf(doc), [], "selection", warnings);
        ViewportSummary? viewport = Try(() => SummarizeViewport(doc), null, "viewport", warnings);
        DocSummary document = Try(() => SummarizeDocument(doc), new DocSummary(0, 0), "document", warnings);

        ContextSnapshot snapshot = new(
            selection,
            viewport,
            document,
            warnings.Count == 0 ? null : [.. warnings]);

        return Success(snapshot);
    }

    // Shared selection projection: also the source of truth for GetSelectionTool's
    // wire shape. Guards the layer lookup per-object so one stale layer index can't
    // throw away the whole selection.
    public static SelectedObject[] SelectionOf(RhinoDoc doc) => doc.Objects
        .GetSelectedObjects(includeLights: false, includeGrips: false)
        .Select(o => new SelectedObject(
            o.Id.ToString(),
            o.Name ?? string.Empty,
            LayerPath(doc, o.Attributes.LayerIndex),
            o.Geometry?.GetType().Name ?? "Unknown"))
        .ToArray();

    private static string LayerPath(RhinoDoc doc, int layerIndex) =>
        layerIndex >= 0 && layerIndex < doc.Layers.Count
            ? doc.Layers[layerIndex].FullPath
            : string.Empty;

    // Shared viewport-to-summary projection.
    public static ViewportSummary? SummarizeViewport(RhinoDoc doc)
    {
        RhinoView? view = doc.Views.ActiveView;
        return view is null ? null : SummarizeViewport(view.ActiveViewport);
    }

    public static ViewportSummary SummarizeViewport(RhinoViewport vp)
    {
        CameraSummary camera = new(
            XYZ(vp.CameraLocation),
            XYZ(vp.CameraTarget),
            XYZ((Point3d)vp.CameraUp),
            vp.Camera35mmLensLength,
            vp.IsPerspectiveProjection ? "perspective"
                : vp.IsParallelProjection ? "parallel"
                : "two-point-perspective");

        return new ViewportSummary(vp.Name ?? string.Empty, vp.DisplayMode?.EnglishName ?? string.Empty, camera);
    }

    private static T Try<T>(Func<T> section, T fallback, string name, List<string> warnings)
    {
        try
        {
            return section();
        }
        catch (Exception ex)
        {
            warnings.Add($"{name}: {ex.Message}");
            return fallback;
        }
    }

    private static DocSummary SummarizeDocument(RhinoDoc doc)
    {
        ObjectEnumeratorSettings settings = new()
        {
            ActiveObjects = true,
            HiddenObjects = true,
            LockedObjects = true,
            DeletedObjects = false,
            IncludeLights = true,
            IncludeGrips = false,
        };

        int objectCount = doc.Objects.GetObjectList(settings).Count();
        return new DocSummary(objectCount, doc.Layers.Count);
    }

    public static double[] XYZ(Point3d p) => [p.X, p.Y, p.Z];
}
