# DeepCover

**A first-person AI-driven spy thriller** built in Unity 6. Investigate suspects, collect evidence, and interrogate NPCs whose replies are generated locally by **Ollama**—adaptive dialogue shaped by suspicion, trust, and what you have already found.

---

## Table of contents

- [Project overview](#project-overview)
- [Core gameplay features](#core-gameplay-features)
- [AI features](#ai-features)
- [Technologies used](#technologies-used)
- [Project architecture overview](#project-architecture-overview)
- [Installation instructions](#installation-instructions)
- [Running the project](#running-the-project)
- [Recommended Ollama models](#recommended-ollama-models)
- [Controls](#controls)
- [Folder structure](#folder-structure)
- [Documentation](#documentation)
- [Known limitations](#known-limitations)
- [Future improvements](#future-improvements)
- [Credits](#credits)
- [AI tools used](#ai-tools-used)
- [Academic and portfolio context](#academic-and-portfolio-context)

---

## Project overview

**DeepCover** began as a broader spy-sandbox concept and was refined into a **focused interrogation thriller vertical slice**: one playable scene, explorable space, collectible evidence, and AI-powered suspect dialogue.

The player acts as an investigator in first person. Interaction is grounded in Unity systems (physics raycasts, ScriptableObject profiles, UI managers). **Narrative lines are not fully pre-authored** for interrogation—the local LLM performs in-character replies while the game injects live state (suspicion level, evidence summary, conversation memory, per-NPC trust).

This design supports **repeatable playtests** and academic demonstration of **local inference** without cloud API keys or ongoing token costs.

| | |
|---|---|
| **Engine** | Unity 6 (`6000.3.9f1`) |
| **Rendering** | Universal Render Pipeline (URP) |
| **Main scene** | `Assets/Scenes/SampleScene.unity` |
| **AI runtime** | [Ollama](https://ollama.com) on `http://localhost:11434` |

---

## Core gameplay features

- **First-person exploration** — `FirstPersonController` with Unity **Input System** (move, look, sprint, jump).
- **Interaction system** — `PlayerInteractor` raycast focus + prompt UI; `IInteractable` on doors, evidence, and NPCs.
- **Evidence collection** — `EvidenceData` ScriptableObjects, inventory, on-screen notifications, journal UI.
- **Suspicion meter** — global tension via `SuspicionManager` and HUD bar; feeds AI prompt context.
- **NPC interrogation** — `NPCBrain` drives Ollama turns; optional typed questions via `DialogueQuestionInputView`.
- **Dialogue presentation** — `DialogueManager` + `DialogueUIView` (TextMeshPro, typewriter or **streaming** text).
- **Player lock during dialogue** — movement and look disabled; cursor unlocked for UI (`DialoguePlayerLock`).

---

## AI features

- **Local LLM inference** through Ollama HTTP API (`/api/generate`).
- **Structured prompts** — `NpcOllamaPromptBuilder` splits stable **system** persona/rules from volatile **user** state blocks (`[STATE]`, `[EVIDENCE]`, `[HISTORY]`, `[PLAYER]`).
- **Per-NPC profiles** — `NPCProfile` ScriptableObjects (personality, voice, secrets, fallback lines).
- **Conversation memory** — bounded turn history and optional summary notes (`NPCMemory`).
- **Context-aware replies** — suspicion band, trust, and evidence inventory summary each turn.
- **Streaming responses** — tokens appended live to dialogue UI (`OllamaStreamingDownloadHandler`, `GenerateStreamAsync`).
- **Cancellation** — ESC / close dialogue aborts in-flight requests (`OllamaCancelToken`).
- **Output control** — max three sentences in prompt + `NpcDialogueResponseFormatter` post-clamp.

---

## Technologies used

| Technology | Role in DeepCover |
|------------|-------------------|
| **Unity 6** | Editor, runtime, scene composition, URP |
| **C#** | Gameplay, AI orchestration, HTTP client |
| **Ollama** | Local model hosting and inference |
| **TextMeshPro** | Dialogue, HUD, question input UI |
| **Unity Input System** | `Assets/InputSystem_Actions.inputactions` |
| **Unity UI (uGUI)** | Canvas, buttons, input fields |
| **Universal RP** | Lighting and materials |
| **Cursor** | AI-assisted IDE for implementation, editor tools, and documentation |

---

## Project architecture overview

Modular layers avoid circular dependencies (e.g. `DialogueManager` does not reference `NPCBrain`).

```
Player Input (Input System)
    → PlayerInteractor
    → NPCBrain
        → NpcOllamaPromptBuilder (+ NpcPromptBudget, NPCMemory)
        → OllamaClient (stream | blocking)
        → NpcDialogueResponseFormatter
    → DialogueManager
        → DialogueUIView (TMP)
        → DialoguePlayerLock
```

**Global services:** `SuspicionManager`, `EvidenceInventory`, `DialogueManager` (singleton).

**Authoring:** `NPCProfile`, `EvidenceData`, `DialogueAsset` (scripted lines where needed).

For full technical detail, see **[ollama-plan.md](ollama-plan.md)**. For decision history, see **[refinements-changes.md](refinements-changes.md)**.

---

## Installation instructions

**Full step-by-step guide:** **[setup.md](setup.md)**

### Quick summary

1. Install **Unity Hub** and **Unity 6** (`6000.3.9f1` or closest 6000.3.x).
2. Install **Ollama** from [ollama.com/download](https://ollama.com/download).
3. Clone or download this repository and open the project folder in Unity Hub.
4. Pull the default model:

   ```bash
   ollama pull llama3
   ```

5. In Unity, open `SampleScene` and run **Tools → Spy Game → Validate And Fix Safe Issues** (recommended for correct UI/NPC wiring).
6. Ensure **Project Settings → Player → Active Input Handling** uses the **Input System Package**.

Import **TMP Essentials** if prompted on first open.

---

## Running the project

### 1. Start Ollama

```bash
ollama list
curl http://localhost:11434/api/tags
```

If the model is missing:

```bash
ollama pull llama3
```

### 2. Play in Unity

1. Open **`Assets/Scenes/SampleScene.unity`**.
2. Press **Play**.
3. Approach an NPC until the interaction prompt appears.
4. Press **Interact** (default **E**).
5. Type a question (if typed-question mode is enabled) or wait for the AI reply panel.
6. **Continue** or **ESC** to close dialogue.

### 3. Optional smoke test

A scene object with **`OllamaTestRunner`** can fire a test prompt on Start; check the Console for `[Ollama]` / `[Ollama Stream]` logs.

---

## Recommended Ollama models

| Model | Command | When to use |
|-------|---------|-------------|
| **llama3** (default) | `ollama pull llama3` | Project default; balanced quality |
| **llama3.2:3b** | `ollama pull llama3.2:3b` | Laptops / low VRAM; faster, less reliable |
| **mistral** | `ollama pull mistral` | Alternative style; test per scene |

Set **Ollama Model** on each `NPCBrain` to match `ollama list` **exactly** (tags matter).

---

## Controls

Defined in `Assets/InputSystem_Actions.inputactions` (**Player** action map). Defaults:

| Action | Binding (typical) |
|--------|-------------------|
| Move | WASD |
| Look | Mouse |
| Interact | **E** |
| Jump | Space |
| Sprint | Left Shift |
| Close dialogue | **ESC** (while dialogue active) |
| Submit question | **Enter** (question input field) |

Rebind via **Edit → Project Settings → Input System Package** or by editing the `.inputactions` asset.

---

## Folder structure

```text
DeepCover/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity          # Main playable vertical slice
│   ├── Scripts/
│   │   ├── Managers/                  # DialogueManager, SuspicionManager, EvidenceInventory
│   │   ├── Player/                    # FirstPersonController, PlayerInteractor, DialoguePlayerLock
│   │   ├── Systems/
│   │   │   ├── AiNpc/                 # NPCBrain, profiles, prompts, memory
│   │   │   ├── Ollama/                # OllamaClient, streaming, cancellation
│   │   │   ├── Dialogue/              # Scripted dialogue assets
│   │   │   ├── Evidence/              # EvidenceData, categories
│   │   │   ├── Interaction/           # IInteractable implementations
│   │   │   └── Suspicion/             # Suspicion levels, interfaces
│   │   └── UI/                        # DialogueUIView, HUD, question input
│   ├── Editor/                        # SpyGameUiBuilder, scene validators
│   ├── InputSystem_Actions.inputactions
│   └── Imports/                       # Third-party environment art packs
├── Packages/manifest.json             # Unity package dependencies
├── ProjectSettings/
├── setup.md                           # Install and run guide
├── ollama-plan.md                     # AI architecture and prompts
├── refinements-changes.md               # Development log
└── README.md                          # This file
```

---

## Documentation

| Document | Description |
|----------|-------------|
| [setup.md](setup.md) | Complete install, Ollama, Unity, troubleshooting |
| [ollama-plan.md](ollama-plan.md) | Ollama integration, data flow, prompts, risks |
| [refinements-changes.md](refinements-changes.md) | Scope evolution, decisions, problems solved |

---

## Known limitations

- **Local hardware bound** — Dialogue quality and latency depend on GPU/RAM and chosen model; CPU-only inference is slow.
- **Hallucinations** — The model may invent facts not in evidence or memory; guardrails are prompt-based, not retrieval-grounded.
- **Single-machine Ollama** — Build targets must run Ollama locally or be reconfigured; no cloud fallback in-repo.
- **Scene wiring sensitivity** — Typed questions require `DialogueQuestionInputView` in scene; use validation menu if interact does nothing unexpected.
- **English-first** — Prompts and UI assume English; localization not implemented.
- **Hybrid content** — Critical plot should remain scripted; AI is optimized for interrogation chatter, not quest logic.

---

## Future improvements

- Structured model output (emotion tags, JSON) for animation and gameplay flags
- Retrieval-augmented generation over collected evidence
- Async conversation summarization for long sessions
- Response caching for repeated questions
- Automated Ollama warm-up on scene load
- Branching dialogue UI wired to `IDialogueBranchingDialogue`
- Emotional state machine driven by `onConversationTurnComplete`
- Build pipeline documentation for shipping with bundled or remote inference policy

See **[refinements-changes.md](refinements-changes.md)** §12 for the current backlog.

---

## Credits

- **Project:** DeepCover — AI interrogation thriller prototype
- **Engine:** Unity Technologies (Unity 6)
- **Local inference:** [Ollama](https://ollama.com)
- **Environment and prop art:** Third-party Asset Store packs under `Assets/Imports/` (Cosmic Retro and related demo content); see individual pack licenses in import folders
- **TextMesh Pro:** Unity package
- **Input System:** Unity package

---

## AI tools used

| Tool | Use in this project |
|------|---------------------|
| **Cursor** | AI-assisted IDE: C# implementation (`NPCBrain`, `OllamaClient`, streaming), editor menus (`SpyGameUiBuilder`, `SpyGameSceneArchitectureValidator`), scene wiring analysis, and technical documentation (`setup.md`, `ollama-plan.md`, `refinements-changes.md`) |
| **Ollama** | Runtime dialogue generation (not an authoring replacement for core game code) |

Human review, Unity Play Mode testing, and Ollama model selection remain required for valid behavior. AI-generated code was integrated with modular architecture and validated against compile-time and scene-reference checks where possible.

---

## Academic and portfolio context

DeepCover is suitable for **academic submission**, **portfolio demonstration**, and **technical design reviews** in areas such as:

- Game programming and systems design (Unity, C#)
- Human–computer interaction (dialogue UI, streaming feedback, cancellation)
- Applied local LLMs in games (prompt engineering, latency, privacy)

The project demonstrates a deliberate **scope reduction** from open sandbox to **AI interrogation vertical slice**, with documented architecture, setup, and iteration history. Evaluators can reproduce results using **[setup.md](setup.md)** on a machine with Unity 6 and Ollama installed.

When citing or presenting:

1. State that dialogue is **model-generated** and may be incorrect or inconsistent.
2. Note **hardware dependency** for live demos.
3. Reference **[ollama-plan.md](ollama-plan.md)** for system design and **[refinements-changes.md](refinements-changes.md)** for process and decisions.

---

## License

License for this repository has not been specified in-project. Third-party assets under `Assets/Imports/` retain their original Asset Store or publisher terms. Add a `LICENSE` file before public distribution if required by your institution or publisher.

---

<p align="center">
  <strong>DeepCover</strong> — Investigate. Pressurize. Interrogate.
</p>
