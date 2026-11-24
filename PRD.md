# **OWLY — Product Requirements Document**

**Product:** Owly  
**Platform:** Rhinoceros 3D (Rhino)  
**Foundation:** McNeel RhinoAI  
**Development:** Claude Code  
**Target users:** Architecture students  
**Slogan:** Owly help, Only Help\!

---

# **1\. Product Overview**

Owly is an AI-powered educational plugin for Rhino, designed to help architecture students understand and use Rhino's tools and commands.

**Core principle: Only Help.**

Unlike AI modelling assistants, Owly must NOT generate geometry, design buildings or perform modelling tasks for users.

Instead, it teaches students how to accomplish their goals themselves.

Students describe what they want to achieve, and Owly recommends the appropriate Rhino tools or commands, explains how to use them and guides them directly through Rhino's interface.

The student always performs the modelling.

# **2\. Target & Scope**

* Primary users: Architecture students, particularly students who wants a deeper understanding of rhino, but can still support beginners.  
* Platform: Rhino.  
* Focus: Native Rhino tools, commands, settings and workflows.  
* Grasshopper is explicitly out of scope.

# **3\. Core Functionality**

## **3.1 AI Tutor**

* Accept natural-language questions.  
* Recommend appropriate Rhino tools and commands based on the user's intended outcome.  
* Explain why a particular tool is suitable.  
* Explain how to use tools, including relevant settings and options.  
* Provide keyboard shortcuts where available.  
* Give step-by-step instructions when appropriate.  
* Support continuous follow-up questions.  
* Allow users to request simpler, more detailed or alternative explanations.  
* Start with concise explanations and progressively reveal more detail when requested.

## **3.2 Rhino Tool Integration**

When recommending a Rhino toolbar tool:

* Highlight the relevant tool directly in Rhino.  
* Use a clear visual indication (e.g. blinking, colour change or outline).  
* Explain where the tool is located and how to use it.

(Phase 5 rework: the toolbar button's own image from the student's Rhino is shown on the command button and in a "where to find" popover over the transcript, plans 020-021.)

When recommending a Rhino command:

* Display the command in the chat.  
* Provide a button to copy the command, and highlight the command line.&nbsp;  
* Do NOT automatically execute the command.  
* Allow users to configure supported command parameters in the chat before insertion.

(Phase 5 rework, ADR-0008: each command is named once, in order. Under its step an options card lets the student set the command's options and click **Run**, which starts the command in Rhino with those options. Rhino then waits for the student's own picks. Blocked commands, such as script runners, Save, Delete, Undo and Options, have no Run button. Plans 019 and 022-024.)

The user must remain in control of all modelling operations.

## **3.3 <!-- gdoc_comment_begin: c1 -->Conversation Interface<!-- gdoc_comment_end: c1 -->**

* Accessible through an Owly owl icon in Rhino's interface.  
* Opens a chat panel, preferably on the right-hand side.  
* Displays the transcript of the current conversation and the AI's responses. Conversations are saved per project on the student's computer and reopened from a strip of owl buttons on the panel's right edge. Each project keeps short project notes, and an opt-in main memory spans all projects (ADR-0009, plans 025-028; this replaces ADR-0006).  
* Includes a text input field.  
* Supports suggested follow-up questions.

On first installation, display a welcome popup with the Owly logo and a short description.

## **3.4 Student Level**

* The student chooses a level (Beginner, Intermediate or Advanced) in the welcome popup and can change it in settings. Owly pitches its explanations to that level.  
* Now and then Owly asks whether its explanations are good, too easy or too hard, and offers a one-click level change. (Plans 029-030.)

# **4\. AI Behaviour & Restrictions**

Owly must behave like a patient, knowledgeable Rhino tutor.

**Mandatory restrictions:**

* Never generate geometry.  
* Never create or modify the user's model.  
* Never automatically perform modelling operations.  
* Never execute Rhino commands on behalf of the user. (Phase 5 rework, ADR-0008: Owly starts a command only when the student clicks Run, and never supplies the picks. The one exception is the guarded option probe, which starts and cancels a command to read its options.)  
* Never invent commands, shortcuts or settings.  
* Never claim to have performed an action it has not performed.  
* Focus exclusively on teaching Rhino.

These restrictions should be implemented through both AI instructions and technical tool permissions wherever possible.

**Teaching style:**

* Friendly, clear and encouraging.  
* Beginner-friendly by default.  
* Concise answers by default.  
* More detailed explanations when requested.  
* Explain concepts necessary for implementing the tool rather than simply listing instructions, but keep it simple and short. Then at the end encourage or ask if more detailed explanations are needed.

# **5\. Knowledge & Context**

## **5.1 Rhino Documentation**

Investigate how Owly can access official McNeel Rhino documentation.

Prefer verified documentation over the AI model's general knowledge.

Where possible:

* Retrieve relevant documentation when answering.  
* Provide links or references to sources.  
* Avoid unsupported claims.

## **5.2 Active Rhino Document**

Investigate whether Owly can access relevant information from the active Rhino document, such as selected objects and object properties.

This could improve contextual recommendations but is not mandatory for the initial MVP.

# **6\. Technical Requirements**

## **6.1 Existing RhinoAI Codebase**

Use McNeel's existing RhinoAI implementation as the foundation.

Before making changes, inspect the repository and identify:

* Existing architecture and dependencies.  
* AI communication and prompt systems.  
* Rhino integration and available APIs.  
* Existing tool execution capabilities.  
* UI components.  
* Privacy and data handling.  
* Functionality that can be reused, modified or removed.

Document the findings before implementing changes.

## **6.2 Modification Strategy**

* Reuse existing functionality wherever practical.  
* Remove unnecessary features, especially geometry generation and automated modelling.  
* Add Owly-specific AI instructions and restrictions.  
* Implement tool highlighting and command insertion.  
* Avoid unnecessary architectural changes.  
* Preserve existing functionality required for the plugin to work.

Do not rewrite working systems without a clear technical reason.

# **7\. Privacy & Security**

Review existing RhinoAI privacy mechanisms.

Ensure that:

* Only necessary user data is collected and transmitted.  
* Users are informed about relevant data processing.  
* Conversation and document data are handled securely.  
* Data retention is minimised.  
* Applicable GDPR requirements are respected.

Document any unresolved privacy or security concerns.

# **8\. Error Handling & Reliability**

* Handle AI communication failures gracefully.  
* Never present uncertain information as fact.  
* If a user reports that an instruction does not work, reassess the previous answer.  
* Allow the AI to reconsider its reasoning without relying on potentially incorrect previous assumptions.  
* Prefer verified documentation when correcting errors.  
* Clearly communicate limitations and uncertainty.

# **9\. Development Priorities**

## **Phase 1 — Investigation - (Completed)**

* Inspect the existing RhinoAI repository.  
* Understand its architecture and capabilities.  
* Identify reusable components and unnecessary functionality.  
* Document technical limitations and implementation recommendations.

## **Phase 2 — Owly Foundation**

* Modify the existing AI behaviour.  
* Implement Owly's identity and system instructions.  
* Restrict geometry generation and automated modelling.  
* Simplify the existing interface.

## **Phase 3 — Basic Interface**

* Implement Owly's icon.&nbsp;  
* Create the chat panel.  
* Implement the conversation transcript and message input (no saved history, ADR-0006).  
* Add the welcome popup.

## **Phase 4 — Rhino Integration**

* Implement tool highlighting (a location card in the panel; an on-screen flash only where Rhino allows it, ADR-0007; on-screen flash not possible with Rhino's public API, see docs/owly/toolbar-flash-spike.md). (Phase 5 rework: the location card is now a "where to find" popover over the transcript, plan 020.)  
* Implement command recommendations.  
* Implement command copy.  
* Implement command terminal highlight (copy, then Rhino gets focus for pasting; nothing is typed into Rhino, ADR-0007)  
* Support configurable command parameters. (Moved to Phase 5: no Rhino API lists a command's options before it runs, so they wait for verified documentation.)

## **Phase 5 — Quality & Knowledge**

* Improve teaching behaviour.  
* Investigate official Rhino documentation integration.  
* Improve error handling.  
* Test AI restrictions and response accuracy.
* Configurable command options, based on verified documentation (moved from Phase 4).

## **Phase 5 rework (product owner, 2026-11-24; plans 018-030)**

* Commands and tools (first): remove the lookup lines; name each command once, in order; "where to find" popover; real button images; Run button; options card from help pages with Owly's suggested values; option probe test.  
* Memory (second): projects; saved conversations per project with the owl strip; project notes; opt-in main memory.  
* Student level (last): level choice in the welcome popup; level check-in.

**Status** (details in `plans/README.md`; manual Rhino checks still open for the done plans):

* Done: 018 lookup lines, 019 commands once/in order, 020 "where to find" popover, 025 projects.  
* To do, in order: 021 button images (needs a Rhino 9 check first) → 022 Run → 023 options card → 024 option probe test → 026 saved conversations → 027 project notes → 028 main memory → 029 level → 030 level check-in.

## **Phase 6 — Testing**

* Test common Rhino workflows.  
* Verify tool recommendations.  
* Verify command insertion.  
* Verify that no geometry is generated or modified.  
* Test plugin stability and privacy.

# **10\. MVP Acceptance Criteria**

The initial version is considered functional when:

1. Users can open and interact with Owly inside Rhino.  
2. Users can ask natural-language questions about Rhino.  
3. Owly recommends appropriate tools and commands.  
4. Recommended toolbar tools and command terminal can be highlighted. (see ADR-0007)  
5. Commands can be copied from the chat with configurable settings. (configurable settings from Phase 5)  
6. Owly explains how to use recommended tools and commands.  
7. Users can ask follow-up questions and request clarification.  
8. Owly does not generate geometry or modify the user's model.  
9. The plugin works reliably within its supported Rhino environment.

---

# **11\. Implementation Instructions for Claude Code**

* First inspect the complete existing codebase.  
* Do not begin implementation before understanding the existing architecture.  
* Prioritise minimal, targeted modifications.  
* Do not introduce unnecessary dependencies.  
* Preserve existing working functionality.  
* Follow the requirements and restrictions in this PRD.  
* If a requirement is technically infeasible, explain why and propose an alternative.  
* Do not silently make major product decisions.  
* Document significant architectural changes.  
* Ask for clarification when a decision could fundamentally affect the architecture or product behaviour.

**Fundamental principle: Owly teaches students how to use Rhino. It never does their modelling for them.**

**ONLY HELP.**

&nbsp;
