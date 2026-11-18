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

When recommending a Rhino command:

* Display the command in the chat.  
* Provide a button to copy the command, and highlight the command line.&nbsp;  
* Do NOT automatically execute the command.  
* Allow users to configure supported command parameters in the chat before insertion.

The user must remain in control of all modelling operations.

## **3.3 <!-- gdoc_comment_begin: c1 -->Conversation Interface<!-- gdoc_comment_end: c1 -->**

* Accessible through an Owly owl icon in Rhino's interface.  
* Opens a chat panel, preferably on the right-hand side.  
* Displays conversation history and AI responses.  
* Includes a text input field.  
* Supports suggested follow-up questions.

On first installation, display a welcome popup with the Owly logo and a short description.

# **4\. AI Behaviour & Restrictions**

Owly must behave like a patient, knowledgeable Rhino tutor.

**Mandatory restrictions:**

* Never generate geometry.  
* Never create or modify the user's model.  
* Never automatically perform modelling operations.  
* Never execute Rhino commands on behalf of the user.  
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

## **Phase 1 — Investigation**

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
* Implement conversation history and message input.  
* Add the welcome popup.

## **Phase 4 — Rhino Integration**

* Implement tool highlighting.  
* Implement command recommendations.  
* Implement command copy.  
* Implement command terminal highlight  
* Support configurable command parameters.

## **Phase 5 — Quality & Knowledge**

* Improve teaching behaviour.  
* Investigate official Rhino documentation integration.  
* Improve error handling.  
* Test AI restrictions and response accuracy.

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
4. Recommended toolbar tools and command terminal can be highlighted.  
5. Commands can be copied from the chat with configurable settings.  
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
