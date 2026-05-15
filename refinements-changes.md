# DeepCover — Refinements & Changes Log

**Document type:** Continuous development log  
**Project:** DeepCover (Unity 6, first-person AI interrogation thriller)  
**Purpose:** Record scope evolution, technical decisions, gameplay refinements, and AI-assisted implementation choices  
**Companion docs:** [setup.md](setup.md), [ollama-plan.md](ollama-plan.md)

---

## How to read this document

Entries are organized **thematically**, not strictly chronological. Where dates are unknown, phases are described as *early*, *mid*, or *late* iteration. Decisions include **rationale** (why) and **outcome** (what shipped in code).

**Cursor** (AI-assisted IDE) is noted where it materially accelerated design, wiring, or documentation—not as a replacement for Unity Editor validation or playtesting.

---

## 1. Initial project scope

### 1.1 Original vision

Early concept documents and scene content pointed toward a **broad spy sandbox**:

- First-person exploration of retro-styled environments (imported recreation room, lab, hangar, and station prop packs)
- Multiple interactable prop types (doors, pickups, evidence, generic NPC hooks)
- HUD elements for objectives, suspicion, and evidence
- Narrative tone: covert investigation, social pressure, and environmental storytelling

### 1.2 Technical baseline chosen early

| Decision | Rationale |
|----------|-----------|
| **Unity 6** (`6000.3.9f1`) | Current LTS-track features, URP 17.x, stable Input System integration |
| **URP** | Achievable lighting for interior scenes; matches asset store packs |
| **New Input System** | Modern action maps; required for rebinding and future gamepad support |
| **TextMeshPro** | Sharp UI at 1080p reference resolution for dialogue-heavy screens |
| **C# gameplay layer** | Clear separation between data (`ScriptableObject` profiles, evidence) and runtime managers |

### 1.3 Cursor-assisted early work

Cursor was used to scaffold **boilerplate** (first-person controller, interaction interface, manager singletons) and to iterate on naming conventions (`DeepCover` namespaces implied by folder structure under `Assets/Scripts`). This reduced setup time but required manual verification of **Input System** and **physics layer** settings in the Editor.

---

## 2. Reduction of scope for feasibility

### 2.1 Problem

Full “spy sandbox” scope implied:

- Multiple mission types, stealth combat, inventory puzzles, and large authored content volume
- Online services or cloud LLM billing for dynamic dialogue
- Long-form QA for emergent AI behavior

As a **solo or small-team** project, parallel systems would not reach shippable quality.

### 2.2 Scope cut (approved direction)

**Retained (core loop):**

- Walk, look, interact (E)
- Collect evidence → inject into interrogation context
- Global suspicion meter influencing NPC tone
- One strong gameplay beat: **interrogate a suspect with AI-generated replies**

**Deferred or minimized:**

- Open-world mission structure
- Combat / takedown loops
- Multi-NPC simultaneous dialogue
- Cloud-hosted models and accounts

### 2.3 Outcome

`SampleScene` became the **vertical slice** target: one playable space, several `NPCBrain` suspects, evidence pickups, and a complete HUD/dialogue stack. Imported environment art remained; **design energy shifted to the interrogation loop**.

---

## 3. Transition to AI-driven dialogue

### 3.1 Motivation

Scripted dialogue (`DialogueAsset`, `NPCDialogue`, `DialogueSequence`) proved sufficient for **barks and tutorials** but too costly for:

- Repeated interrogation with player-typed questions
- Reactions that reference **current** evidence and suspicion
- Replayability in a thesis / prototype context

**Decision:** Treat LLM output as **character performance**, not as a quest state machine.

### 3.2 Hybrid dialogue policy

| Content type | Source |
|--------------|--------|
| Tutorial, game-over, critical plot gates | Scripted `DialogueManager` sequences |
| Suspect interrogation | `NPCBrain` + Ollama |
| UI prompts | Static TMP strings |

`NPCDialogue` and `NPCBrain` coexist: the former validates the interaction pipeline; the latter is the **production interrogation path**.

### 3.3 Gameplay refinement

- Player **trust** per NPC (`NPCBrain` + `NPCProfile.StartingTrust`) added nuance beyond global suspicion
- Post-turn **UnityEvents** (`onConversationTurnComplete`) reserved for emotional state / animation without hard-wiring `DialogueManager` to AI

**Cursor role:** Generated initial `IInteractable` flow documentation and helped draft `NPCBrain` XML summaries for designer-facing tooltips.

---

## 4. Ollama integration decisions

### 4.1 Why Ollama (local inference)

| Option | Rejected / deferred because |
|--------|----------------------------|
| Cloud APIs (OpenAI, etc.) | Ongoing cost, privacy, offline demo risk at showcases |
| Embedded small models in Unity | Immature C# inference story; heavy build size |
| **Ollama on localhost** | Simple HTTP, developer-controlled models, aligns with “spy laptop” fantasy |

Default endpoint: `http://localhost:11434`, path `/api/generate`.

### 4.2 Client design: `OllamaClient`

**Decisions:**

- Plain C# class (not `MonoBehaviour`) — testable via `OllamaTestRunner`
- Coroutine-based `IEnumerator` API — fits Unity gameplay without async/await friction on older patterns
- Separate **system** and **user** prompts — matches Ollama API and instruction-tuning best practice
- Explicit error strings for connection failure, HTTP 404 (wrong model tag), and empty body

**Cursor role:** Implemented and extended `OllamaClient`; fixed TMP type mismatch in editor UI builder (`TMP_Text` vs `TextMeshProUGUI`).

### 4.3 Model choice iteration

| Iteration | Model | Observation |
|-----------|-------|-------------|
| Early default | `llama3` | Good instruction following; heavier VRAM |
| Documented fallback | `llama3.2:3b` | Faster on laptops; more hallucination |
| Inspector per-NPC override | `ollamaModel` on `NPCBrain` | Allows “lead suspect” vs background NPC quality tradeoff |

Documented in [ollama-plan.md](ollama-plan.md) and [setup.md](setup.md).

### 4.4 Streaming (late iteration)

**Problem:** Blocking `GenerateAsync` left multi-second “dead air” before `DialogueUIView` opened.

**Solution:**

- `OllamaStreamingDownloadHandler` (`DownloadHandlerScript`) parses NDJSON chunks on worker thread
- Main thread drains deltas each frame → `DialogueManager.AppendStreamingLineText`
- `OllamaCancelToken` + `UnityWebRequest.Abort()` on ESC / close

**Decision:** `NPCBrain.useStreamingResponses` default **ON**; non-streaming path retained for debugging.

---

## 5. NPC system refinements

### 5.1 `NPCProfile` (ScriptableObject)

Centralized **authoring** away from scene components:

- Identity, personality, voice guidelines
- `secretsForAi` (private knowledge block)
- `fallbackLineOnAiFailure` for connection errors

**Rationale:** Designers duplicate profiles per suspect without duplicating logic.

### 5.2 `NPCBrain` as orchestrator

Single component per suspect responsible for:

- `IInteractable` implementation
- `NPCMemory` (turn cap + summary notes)
- Prompt context assembly (`SuspicionManager`, `EvidenceInventory`)
- Ollama request lifecycle (`requestInFlight`, `waitingForManagedDialogueClose`)

**Refinement:** Runtime `FindFirstObjectByType` fallbacks for `DialoguePlayerLock` and `DialogueQuestionInputView` when scene wiring incomplete—reduces hard failures during greybox, hides missing Inspector assignments until validation.

### 5.3 Typed questions vs default line

| Setting | Behavior |
|---------|----------|
| `requireTypedQuestionBeforeAi` + `DialogueQuestionInputView` | Question UI → submit → AI |
| `requireTypedQuestionBeforeAi` without view | **Falls through** to `defaultPlayerUtteranceOnInteract` (`"Hello."`) — identified as configuration hazard |

**Cursor role:** Built `DialogueQuestionInputView`, editor menu **Tools → Add Dialogue Question Input To Canvas**, and `SpyGameSceneArchitectureValidator` after playtests showed null `questionInputView` on all NPCs in `SampleScene`.

### 5.4 Colliders and interaction

`PlayerInteractor` raycasts with `QueryTriggerInteraction.Ignore`. NPCs use **non-trigger** `CapsuleCollider` so detection is reliable.

---

## 6. Dialogue architecture changes

### 6.1 Layered UI architecture

**Decision:** Avoid circular dependencies.

```
DialogueManager  →  DialogueUIView, DialoguePlayerLock
NPCBrain         →  DialogueManager (public API only), DialogueQuestionInputView
DialogueQuestionInputView  ↛  NPCBrain (callbacks only)
```

`DialogueQuestionInputView` is a **sibling** under `SpyGame_MainCanvas`, not a child of `DialogueManager`.

### 6.2 `DialoguePlayerLock`

Extracted cursor + movement disable from dialogue UI:

- Disables `FirstPersonController` and `PlayerInteractor`
- Unlocks cursor during dialogue / question entry
- Used by both `DialogueManager` and `NPCBrain` question flow

**Problem encountered:** NPCs had `dialoguePlayerLock: null` in scene while typed-question flow expected early lock. **Fix:** Scene YAML wiring + validator auto-assign.

### 6.3 Streaming dialogue API on `DialogueManager`

New methods (additive, non-breaking):

- `BeginStreamingLine`
- `AppendStreamingLineText`
- `CompleteStreamingLine`
- `CancelActiveStream`

Scripted `StartDialogue` unchanged for authored content.

### 6.4 `DialogueEnded` subscription timing

**Problem:** `NPCBrain.OnEnable` subscribed to `DialogueManager.DialogueEnded` only if `Instance` already existed → `requestInFlight` could stick **true**.

**Fix:** Also subscribe in `Start()`; cancel active stream on end.

---

## 7. Performance optimization decisions

### 7.1 Prompt size caps (`NpcPromptBudget`)

**Decision:** Character budgets per section (personality, evidence, history, total user prompt).

**Rationale:** Local inference latency scales with context length; unbounded `NPCMemory` destroyed frame times on long sessions.

**Mechanism:** `TailConversationHistoryTrimmer` shrinks history first when over budget.

### 7.2 Output length caps

- Prompt: “Max 3 short sentences”
- `NpcDialogueResponseFormatter.ClampToMaxSentences` post-process

**Rationale:** Decode time and UI layout both favor short replies.

### 7.3 One in-flight request per `NPCBrain`

Prevents double-interact spam and overlapping Ollama calls.

### 7.4 UI responsiveness

Streaming + `ForceMeshUpdate` on append keeps main thread work **O(chunks per frame)** rather than one large stall at end.

---

## 8. UI and interaction improvements

### 8.1 `SpyGameUiBuilder` (Editor)

**Decision:** One-click construction of:

- `SpyGame_MainCanvas` (1920×1080 scaler)
- HUD: crosshair, objective, suspicion meter, interaction prompt, evidence toast
- `Dialogue_UI` panel with evidence strip
- Optional prefab save under `Assets/Prefabs/UI/`

**Rationale:** Repeatable setup for new team members; reduces manual RectTransform work.

**Cursor role:** Authored and extended builder; integrated `DialogueQuestionInputViewBuilder` into full UI menu.

### 8.2 `DialogueQuestionInputView`

Minimalist spy aesthetic panel:

- `RootGroup` (CanvasGroup), `NPCLabel`, `QuestionInputField`, Submit/Cancel
- Enter submits; auto-focus on open
- Editor **Auto Wire Children**

### 8.3 Scene validation tooling

`SpyGameSceneArchitectureValidator`:

- **Tools → Spy Game → Validate Scene Architecture**
- **Validate And Fix Safe Issues** — wires `dialoguePlayerLock`, creates question UI if missing, assigns `NPCBrain.questionInputView`

**Rationale:** YAML scene drift was causing silent broken loops; validator encodes institutional knowledge from debugging sessions.

### 8.4 Input System

`Assets/InputSystem_Actions.inputactions` shared by player movement and interact. `PlayerInteractor` auto-finds asset in Editor `OnValidate` when unassigned.

---

## 9. Evidence system refinements

### 9.1 Data model

`EvidenceData` ScriptableObjects with category enum, icon, description.

`EvidenceInventory` singleton builds **text summary** for prompts via `IEvidenceContextProvider` — not raw object graphs to the LLM.

### 9.2 Gameplay linkage

`EvidencePickupInteractable` → inventory → `[EVIDENCE]` block in `NpcOllamaPromptBuilder`.

**Design intent:** Presenting proof in UI (`DialogueUIView` evidence strip) can be extended; prompt already receives inventory summary each turn.

### 9.3 UI feedback

`EvidenceNotificationUI` toast on collect; `EvidenceInventoryUI` for journal-style review.

**Refinement deferred:** Per-piece “show evidence to NPC” gesture not required for vertical slice if summary lists collected items.

---

## 10. AI prompt engineering iterations

### 10.1 System vs user split

| Block | Content | Stability |
|-------|---------|-----------|
| **System** | Rules, persona, voice, secrets | Changes rarely |
| **User** | Suspicion, trust, evidence, history, player line | Rebuilt every turn |

**Rationale:** Mirrors instruction-tuned model behavior; minimizes redundant tokens.

### 10.2 Bracket tags in user prompt

`[STATE]`, `[EVIDENCE]`, `[HISTORY]`, `[PLAYER]`, `[TASK]` — cheap scaffolding for smaller models.

### 10.3 Anti-meta and anti-spoiler rules

- No AI/Unity/Ollama mention
- Secrets labeled as not automatically known to investigator
- No markdown in replies

**Iteration:** Early tests produced markdown lists; explicit “plain text” rule reduced formatting leaks.

### 10.4 Suspicion integration

`SuspicionManager` exposes numeric value, band enum, and `GetSuspicionDialogueTag()` via `ISuspicionDialogueContext`.

**Gameplay reasoning:** Global tension affects all suspects; trust remains per-NPC for relationship arc.

### 10.5 Memory notes

`NPCMemory.AddSummaryNote` for durable facts; transcript still tail-trimmed.

**Cursor role:** Helped extend `NPCMemory` constructor and transcript layout without rewriting `NPCBrain` storage.

---

## 11. Problems encountered

| # | Problem | Impact | Solution |
|---|---------|--------|----------|
| 1 | Ollama not running | Immediate fail; fallback line | `OllamaTestRunner`, setup doc, clear `OllamaResult` errors |
| 2 | Wrong model tag (HTTP 404) | Silent confusion | Parse Ollama error JSON; hint to run `ollama pull` |
| 3 | `questionInputView` null with typed questions ON | Default `"Hello."` sent; no UI | Validator + builder menu; `FindFirstObjectByType` fallback |
| 4 | Missing `DialogueQuestionInputView` in scene | Same as #3 | **Validate And Fix Safe Issues** creates panel |
| 5 | `dialoguePlayerLock` unassigned on NPCs | No movement lock during question phase | Scene wire `fileID`; validator auto-fill |
| 6 | Blocking Ollama wait | Poor immersion | Streaming pipeline |
| 7 | `DialogueEnded` not subscribed | NPC stuck non-interactable | `Start()` subscription + stream cancel |
| 8 | Duplicate `EventSystem` | UI input bugs | Builder reuses existing; validation warns |
| 9 | LLM hallucinated evidence | Breaks fairness | Evidence only from inventory summary; short replies |
| 10 | Long prompts / slow GPU | Frame hitches | `NpcPromptBudget`, smaller model docs |
| 11 | CS1503 TMP font apply in editor builder | Compile error | Pattern match `TextMeshProUGUI` for `textComponent` |
| 12 | `DownloadHandler` thread safety | Potential UI corruption | Drain deltas on main thread only |

---

## 12. Final architectural decisions

### 12.1 Authority model

| System | Authority |
|--------|-----------|
| Win/lose, scene flow | Unity gameplay code |
| What suspect *says* | Ollama (constrained by prompts) |
| What suspect *knows* in fiction | `NPCProfile.secretsForAi` + memory transcript |
| What player has proven | `EvidenceInventory` only |

### 12.2 Modular boundaries (shipping intent)

```
Interaction layer     →  IInteractable, PlayerInteractor
AI orchestration      →  NPCBrain, NPCMemory, NpcOllamaPromptBuilder
Transport             →  OllamaClient (+ streaming handler, cancel token)
Presentation          →  DialogueManager, DialogueUIView, DialogueQuestionInputView
Global state          →  SuspicionManager, EvidenceInventory
Authoring             →  NPCProfile, EvidenceData, DialogueAsset
Editor / ops          →  SpyGameUiBuilder, SpyGameSceneArchitectureValidator
```

### 12.3 Default player experience (vertical slice)

1. Explore `SampleScene`
2. Collect evidence
3. Interact with suspect (E)
4. Type question (when UI wired + flag enabled)
5. Watch streamed reply in dialogue panel
6. Continue / ESC — control restored

### 12.4 Documentation deliverables (Cursor-assisted)

| File | Role |
|------|------|
| [setup.md](setup.md) | Onboarding from zero install |
| [ollama-plan.md](ollama-plan.md) | Technical AI architecture |
| **refinements-changes.md** (this file) | Decision history and iteration log |

### 12.5 Known remaining gaps (honest backlog)

- Emotional state not fully driven by parsed model output (events only)
- No response caching or RAG over evidence database
- No automated Ollama warm-up on scene load
- `NPCDialogue` vs `NPCBrain` duplication may confuse designers without a single “interrogation” component guideline
- Hybrid scripted + AI beats need a content style guide in-repo

---

## Appendix: Cursor-assisted development — practices that worked

1. **Scene inspection via grep/YAML** — Cursor identified null `questionInputView` across all NPCs faster than manual Hierarchy walk.
2. **Targeted editor tools** — Menu items (`Tools → Spy Game → …`) generated repeatable UI hierarchy instead of one-off manual RectTransforms.
3. **Architecture validation before playtest** — Reduced “it compiles but does nothing” cycles.
4. **Documentation in parallel** — `setup.md` and `ollama-plan.md` aligned with actual class names, reducing onboarding drift.
5. **Limits of AI assistance** — Play mode, Ollama latency, and model personality still require human tuning; Cursor did not replace `ollama pull` or GPU reality checks.

---

## Revision log (document meta)

| Version | Notes |
|---------|-------|
| 1.0 | Initial refinements log: scope reduction through streaming dialogue, validation tooling, Cursor workflow appendix |

---

*Update this file when making significant scope, architecture, or AI integration changes so the project history remains auditable for development reviews and academic submission.*
