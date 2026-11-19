// English fallback for when no host has pushed a table: the dev server, the mock host and the tests.
export const EN = {
  'header.switchAgent': 'Owly',
  'header.noAgent': "Owly isn't set up",
  'header.nothingConfigured': 'nothing configured',
  'header.newConversation': 'New conversation (clears this one)  (Ctrl+Shift+N)',
  'header.settings': 'Owly settings',

  'empty.ready': '{0} is ready',
  'empty.theAgent': 'Owly',
  'empty.body':
    'Ask how to do something in Rhino. Owly explains which tools and commands to use. You do the modelling.',
  'empty.starterCommand': 'How do I loft between curves?',
  'empty.starterTower': "What's the difference between Join and Group?",
  'empty.starterLayers': 'How do I make a wall with openings?',
  'empty.noAgent': "Owly isn't set up yet",
  'empty.noAgentBody': 'Owly uses Claude Code. Install it and sign in, then come back here.',
  'empty.openSettings': 'Open Owly settings',
  'empty.setupGuide': 'How to install Claude Code',

  'turn.copy': 'Copy the reply',
  'command.copyNamed': 'Copy {0}',
  'command.copied': 'Copied {0}. Paste it on the command line with Ctrl+V, then press Enter.',
  'command.copiedMac': 'Copied {0}. Paste it on the command line with ⌘V, then press Enter.',
  'turn.stopped': 'stopped',
  'turn.agentName': 'Owly',
  'transcript.newOutput': 'New output',
  'chip.removeNamed': 'Remove {0}',


  'image.generated': 'Generated image',
  'image.openNamed': 'Open {0} in your image viewer',
  'image.saveNamed': 'Save {0}',
  'image.save': 'Save',


  'status.workingForScreenReader': 'Working',

  'busy.mulling': 'Mulling',
  'busy.grazing': 'Grazing',
  'busy.ruminating': 'Ruminating',
  'busy.chewing': 'Chewing',
  'busy.foraging': 'Foraging',
  'busy.rummaging': 'Rummaging',
  'busy.scouting': 'Scouting',
  'busy.tracking': 'Tracking',
  'busy.ferreting': 'Ferreting',
  'busy.nosing': 'Nosing',
  'busy.digging': 'Digging',
  'busy.burrowing': 'Burrowing',
  'busy.wading': 'Wading',
  'busy.trekking': 'Trekking',
  'busy.roaming': 'Roaming',
  'busy.ambling': 'Ambling',
  'busy.plodding': 'Plodding',
  'busy.trotting': 'Trotting',
  'busy.galloping': 'Galloping',
  'busy.thundering': 'Thundering',
  'busy.herding': 'Herding',
  'busy.gathering': 'Gathering',
  'busy.mustering': 'Mustering',
  'busy.corralling': 'Corralling',
  'busy.beavering': 'Beavering',
  'busy.honing': 'Honing',
  'busy.whirring': 'Whirring',


  'turn.tokenCount': '{0} tok',

  'agent.menuHead': 'Agent',
  'agent.settings': 'Owly settings…',
  'agent.useNamed': 'Use {0}',
  'agent.reasonDisabled': 'turned off in settings',
  'agent.reasonMissing': 'not installed',
  'agent.reasonSignin': 'needs sign-in',

  'composer.placeholderIdle': 'Ask Owly how to do something in Rhino…',
  'composer.placeholderBusy': 'Type your next message…',
  'composer.ariaMessage': 'Message',
  'composer.attachFile': 'Attach a file',
  'composer.commands': 'Commands',
  'composer.hintStop': 'Esc to stop',
  'composer.hintSend': 'Enter to send',
  'composer.stop': 'Stop  (Esc)',
  'composer.send': 'Send  (Enter)',
  'composer.overAttachmentLimit': '{0} is {1}, over the {2} attachment limit.',

  'cmd.newHint': 'Start a fresh conversation',
  'cmd.stopHint': "Stop Owly's answer",
  'cmd.settingsHint': 'Open Owly settings',

  'ask.dontKnow': "I don't know",
  'ask.back': 'Back',
  'ask.next': 'Next',
  'ask.answer': 'Answer',
  'ask.answerAll': 'Answer all',
  'ask.cancel': 'Cancel',
  'ask.somethingElse': 'Something else…',
  'ask.yourAnswer': 'Your answer…',
  'ask.progress': 'Question {0} of {1}',

  'notice.dismiss': 'Dismiss',

  'format.justNow': 'just now',
  'format.minutesAgo': '{0}m ago',
  'format.hoursAgo': '{0}h ago',
  'format.daysAgo': '{0}d ago',

  'unsupported.title': 'This panel needs a newer system WebView',
  'unsupported.body':
    'The panel renders in the WebView that comes with your operating system, so updating the OS (macOS ships WebKit with Safari) is what fixes it.',
  'unsupported.macos':
    'Every Apple Silicon Mac can run a recent macOS, so this usually just means pending system updates.',
  'unsupported.classic': "Until then, you can still use Rhino's Help (F1) and the command line.",
  'unsupported.showAnyway': 'Show it anyway',
} as const;

export type StringKey = keyof typeof EN;
