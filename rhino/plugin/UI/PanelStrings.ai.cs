using Rhino.UI;

namespace Rhino.AI.UI;

// The panel is TypeScript in a webview, which Rhino's string extractor cannot read, so its English lives here.
internal static class PanelStrings
{
    public static IReadOnlyDictionary<string, string> Localized() => new Dictionary<string, string>
    {
        ["header.switchAgent"] = Localization.LocalizeString("Owly", 60),
        ["header.noAgent"] = Localization.LocalizeString("Owly isn't set up", 61),
        ["header.nothingConfigured"] = Localization.LocalizeString("nothing configured", 62),
        ["header.history"] = Localization.LocalizeString("Conversation history", 63),
        ["header.newConversation"] = Localization.LocalizeString("New conversation  (Ctrl+Shift+N)", 64),
        ["header.settings"] = Localization.LocalizeString("Owly settings", 65),

        ["empty.ready"] = Localization.LocalizeString("{0} is ready", 66),
        ["empty.theAgent"] = Localization.LocalizeString("Owly", 67),
        ["empty.body"] = Localization.LocalizeString("Ask how to do something in Rhino. Owly explains which tools and commands to use. You do the modelling.", 68),
        ["empty.starterCommand"] = Localization.LocalizeString("How do I loft between curves?", 69),
        ["empty.starterTower"] = Localization.LocalizeString("What\'s the difference between Join and Group?", 70),
        ["empty.starterLayers"] = Localization.LocalizeString("How do I make a wall with openings?", 71),
        ["empty.noAgent"] = Localization.LocalizeString("Owly isn't set up yet", 72),
        ["empty.noAgentBody"] = Localization.LocalizeString("Owly uses Claude Code. Install it and sign in, then come back here.", 73),
        ["empty.openSettings"] = Localization.LocalizeString("Open Owly settings", 74),
        ["empty.setupGuide"] = Localization.LocalizeString("How to install Claude Code", 75),

        ["turn.copy"] = Localization.LocalizeString("Copy the reply", 77),
        ["turn.stopped"] = Localization.LocalizeString("stopped", 81),
        ["transcript.newOutput"] = Localization.LocalizeString("New output", 82),
        ["chip.removeNamed"] = Localization.LocalizeString("Remove {0}", 83),


        ["image.generated"] = Localization.LocalizeString("Generated image", 89),
        ["image.openNamed"] = Localization.LocalizeString("Open {0} in your image viewer", 90),
        ["image.saveNamed"] = Localization.LocalizeString("Save {0}", 91),
        ["image.save"] = Localization.LocalizeString("Save", 92),


        ["status.workingForScreenReader"] = Localization.LocalizeString("Working", 98),

        ["busy.mulling"] = Localization.LocalizeString("Mulling", 99),
        ["busy.grazing"] = Localization.LocalizeString("Grazing", 100),
        ["busy.ruminating"] = Localization.LocalizeString("Ruminating", 101),
        ["busy.chewing"] = Localization.LocalizeString("Chewing", 102),
        ["busy.foraging"] = Localization.LocalizeString("Foraging", 103),
        ["busy.rummaging"] = Localization.LocalizeString("Rummaging", 104),
        ["busy.scouting"] = Localization.LocalizeString("Scouting", 105),
        ["busy.tracking"] = Localization.LocalizeString("Tracking", 106),
        ["busy.ferreting"] = Localization.LocalizeString("Ferreting", 107),
        ["busy.nosing"] = Localization.LocalizeString("Nosing", 108),
        ["busy.digging"] = Localization.LocalizeString("Digging", 109),
        ["busy.burrowing"] = Localization.LocalizeString("Burrowing", 110),
        ["busy.wading"] = Localization.LocalizeString("Wading", 111),
        ["busy.trekking"] = Localization.LocalizeString("Trekking", 112),
        ["busy.roaming"] = Localization.LocalizeString("Roaming", 113),
        ["busy.ambling"] = Localization.LocalizeString("Ambling", 114),
        ["busy.plodding"] = Localization.LocalizeString("Plodding", 115),
        ["busy.trotting"] = Localization.LocalizeString("Trotting", 116),
        ["busy.galloping"] = Localization.LocalizeString("Galloping", 117),
        ["busy.thundering"] = Localization.LocalizeString("Thundering", 118),
        ["busy.herding"] = Localization.LocalizeString("Herding", 119),
        ["busy.gathering"] = Localization.LocalizeString("Gathering", 120),
        ["busy.mustering"] = Localization.LocalizeString("Mustering", 121),
        ["busy.corralling"] = Localization.LocalizeString("Corralling", 122),
        ["busy.beavering"] = Localization.LocalizeString("Beavering", 123),
        ["busy.honing"] = Localization.LocalizeString("Honing", 124),
        ["busy.whirring"] = Localization.LocalizeString("Whirring", 125),

        ["review.readOnlyAtTime"] = Localization.LocalizeString("Read-only · {0}", 126),
        ["review.backToLive"] = Localization.LocalizeString("Back to live", 127),
        ["review.resumeTitle"] = Localization.LocalizeString("Continue this conversation", 128),
        ["review.resume"] = Localization.LocalizeString("Resume", 129),

        ["history.title"] = Localization.LocalizeString("Conversations", 130),
        ["history.close"] = Localization.LocalizeString("Close", 131),
        ["history.search"] = Localization.LocalizeString("Search conversations…", 132),
        ["history.empty"] = Localization.LocalizeString("No saved conversations yet", 133),
        ["history.noMatch"] = Localization.LocalizeString("Nothing matches", 134),
        ["history.turnCountOne"] = Localization.LocalizeString("{0} turn", 135),
        ["history.turnCountMany"] = Localization.LocalizeString("{0} turns", 136),
        ["history.tokenCount"] = Localization.LocalizeString("{0} tok", 137),

        ["agent.menuHead"] = Localization.LocalizeString("Agent", 138),
        ["agent.settings"] = Localization.LocalizeString("Owly settings…", 139),
        ["agent.useNamed"] = Localization.LocalizeString("Use {0}", 140),
        ["agent.reasonDisabled"] = Localization.LocalizeString("turned off in settings", 141),
        ["agent.reasonMissing"] = Localization.LocalizeString("not installed", 142),
        ["agent.reasonSignin"] = Localization.LocalizeString("needs sign-in", 143),

        ["composer.placeholderIdle"] = Localization.LocalizeString("Ask Owly how to do something in Rhino…", 144),
        ["composer.placeholderBusy"] = Localization.LocalizeString("Type your next message…", 145),
        ["composer.ariaMessage"] = Localization.LocalizeString("Message", 146),
        ["composer.attachFile"] = Localization.LocalizeString("Attach a file", 147),
        ["composer.commands"] = Localization.LocalizeString("Commands", 149),
        ["composer.hintStop"] = Localization.LocalizeString("Esc to stop", 150),
        ["composer.hintSend"] = Localization.LocalizeString("Enter to send", 151),
        ["composer.stop"] = Localization.LocalizeString("Stop  (Esc)", 152),
        ["composer.send"] = Localization.LocalizeString("Send  (Enter)", 153),
        ["composer.overAttachmentLimit"] = Localization.LocalizeString("{0} is {1}, over the {2} attachment limit.", 154),

        ["cmd.newHint"] = Localization.LocalizeString("Start a fresh conversation", 155),
        ["cmd.historyHint"] = Localization.LocalizeString("Browse past conversations", 156),
        ["cmd.stopHint"] = Localization.LocalizeString("Stop Owly's answer", 159),
        ["cmd.settingsHint"] = Localization.LocalizeString("Open Owly settings", 160),

        ["ask.dontKnow"] = Localization.LocalizeString("I don't know", 161),
        ["ask.back"] = Localization.LocalizeString("Back", 162),
        ["ask.next"] = Localization.LocalizeString("Next", 163),
        ["ask.answer"] = Localization.LocalizeString("Answer", 164),
        ["ask.answerAll"] = Localization.LocalizeString("Answer all", 165),
        ["ask.cancel"] = Localization.LocalizeString("Cancel", 166),
        ["ask.somethingElse"] = Localization.LocalizeString("Something else…", 167),
        ["ask.yourAnswer"] = Localization.LocalizeString("Your answer…", 168),
        ["ask.progress"] = Localization.LocalizeString("Question {0} of {1}", 169),

        ["notice.dismiss"] = Localization.LocalizeString("Dismiss", 170),

        ["format.justNow"] = Localization.LocalizeString("just now", 171),
        ["format.minutesAgo"] = Localization.LocalizeString("{0}m ago", 172),
        ["format.hoursAgo"] = Localization.LocalizeString("{0}h ago", 173),
        ["format.daysAgo"] = Localization.LocalizeString("{0}d ago", 174),

        ["unsupported.title"] = "This panel needs a newer system WebView",
        ["unsupported.body"] = "The panel renders in the WebView that comes with your operating system, so updating the OS (macOS ships WebKit with Safari) is what fixes it.",
        ["unsupported.macos"] = "Every Apple Silicon Mac can run a recent macOS, so this usually just means pending system updates.",
        ["unsupported.classic"] = "Until then, you can still use Rhino's Help (F1) and the command line.",
        ["unsupported.showAnyway"] = "Show it anyway",
    };

    public static string LanguageTag() => Localization.CurrentLanguageId switch
    {
        1028 => "zh-TW",
        1029 => "cs-CZ",
        1031 => "de-DE",
        1034 => "es-ES",
        1036 => "fr-FR",
        1040 => "it-IT",
        1041 => "ja-JP",
        1042 => "ko-KR",
        1045 => "pl-PL",
        1049 => "ru-RU",
        1055 => "tr-TR",
        2052 => "zh-CN",
        2070 => "pt-PT",
        _ => "en-US",
    };
}
