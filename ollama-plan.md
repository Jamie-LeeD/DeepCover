# Ollama Integration Plan — DeepCover (Unity 6)

**Document type:** Technical architecture and operations plan  
**Project:** DeepCover — first-person narrative spy thriller  
**Stack:** Unity 6, C#, TextMeshPro, New Input System, local [Ollama](https://ollama.com) inference  
**Last aligned with codebase:** NPCBrain, NpcOllamaPromptBuilder, OllamaClient (streaming), DialogueManager, DialogueUIView

---

## 1. Overview

DeepCover uses **local large language model (LLM) inference** via Ollama to generate **dynamic NPC dialogue** during interrogation and social stealth gameplay. Unlike fully scripted visual-novel pipelines, suspect lines are assembled at runtime from:

- authored **NPC profiles** (personality, voice, secrets),
- live **suspicion** and **trust** state,
- the player’s **evidence inventory** summary,
- and a **bounded conversation memory** per NPC.

Ollama runs on the player’s machine (default `http://localhost:11434`), keeping dialogue private, offline-capable, and free of per-token cloud billing. The design treats the LLM as a **character performance engine**, not a general game logic authority: Unity retains authority over progression, evidence, suspicion, and UI flow.

**Primary gameplay functions powered by Ollama:**

| Function | Mechanism |
|----------|-----------|
| Interrogation dialogue | Player types or submits a question; model replies in character |
| Adaptive NPC responses | Trust, suspicion band, and transcript shape tone each turn |
| Suspicion-based behavior | `SuspicionManager` values injected into `[STATE]` user prompt block |
| Evidence reactions | `EvidenceInventory` summary injected into `[EVIDENCE]` block |
| Emotional / narrative hooks | `onConversationTurnComplete` events; designer-tunable trust/suspicion deltas |

The integration is **modular**: `OllamaClient` has no Unity scene dependencies; `NPCBrain` owns turn orchestration; `DialogueManager` owns presentation and player lock.

---

## 2. Model Choice

### 2.1 Selected model

The project default (scene and `NPCBrain` inspector) is **`llama3`**, resolved through Ollama’s tagged model registry (`ollama pull llama3`). Per-NPC overrides are supported via serialized `ollamaModel` on each `NPCBrain`.

`OllamaTestRunner` documents alternates such as **`llama3.2:3b`** and **Mistral** variants for machines where full `llama3` is too heavy.

### 2.2 Rationale for smaller / faster models

Interrogation is **real-time, repeated, and interruptible**. Design constraints favor:

- **Low time-to-first-token** — player must see a reaction quickly or immersion breaks.
- **Predictable VRAM/RAM** — target gaming PCs, not datacenter GPUs.
- **Instruction following at short output length** — replies are capped at **three sentences** (`NpcOllamaPromptBuilder.MaxReplySentences`, enforced again by `NpcDialogueResponseFormatter`).

Larger models (e.g. 70B class) improve prose quality but routinely violate latency budgets for first-person exploration pacing. Smaller quantizations (3B–8B class) trade occasional incoherence for **playable** response curves.

### 2.3 Performance considerations

| Factor | Project impact |
|--------|----------------|
| Context size | `NpcPromptBudget` caps system + user blocks (~3.2k user chars soft ceiling) |
| Output length | Hard narrative cap of 3 sentences reduces decode time |
| Concurrent requests | One active `requestInFlight` per `NPCBrain`; no parallel generate per NPC |
| HTTP overhead | Localhost keeps RTT negligible vs. cloud |
| GPU contention | Ollama shares GPU with Unity; frame stutter possible on low-end hardware |

### 2.4 Real-time gameplay requirements

- **Perceived** responsiveness matters more than full completion time (see §3 streaming).
- Failure must be graceful: `NPCProfile.fallbackLineOnAiFailure` and logging via `onAiRequestFailed`.
- Dialogue must be **cancellable** (ESC / close panel → `OllamaCancelToken` + `UnityWebRequest.Abort()`).

---

## 3. Inference Timing

### 3.1 Response latency challenges

Local inference latency is dominated by:

1. **Prefill** — processing system + user prompt (grows with evidence + history).
2. **Decode** — generating output tokens (mitigated by short reply cap).
3. **Cold start** — first request after Ollama idle or model unload.

Players experience “dead air” if the UI waits for full JSON completion before showing anything. DeepCover addresses this with **streaming** (§3.3).

### 3.2 Asynchronous requests

All Ollama traffic runs through **Unity coroutines** (`IEnumerator`), not blocking the main thread:

- **Non-streaming:** `OllamaClient.GenerateAsync` — single POST, `DownloadHandlerBuffer`, full parse on completion.
- **Streaming:** `OllamaClient.GenerateStreamAsync` — POST with `"stream": true`, incremental NDJSON via `OllamaStreamingDownloadHandler`.

`NPCBrain` sets `requestInFlight` for the turn duration so interaction raycasts do not stack duplicate prompts.

### 3.3 Streaming responses

When `NPCBrain.useStreamingResponses` is enabled (default):

1. `DialogueManager.BeginStreamingLine` opens the reply panel and unlocks the cursor **before** decode finishes.
2. Each Ollama NDJSON chunk’s `response` field is appended via `DialogueManager.AppendStreamingLineText` → `DialogueUIView.AppendStreamingText`.
3. On completion, `NpcDialogueResponseFormatter.ClampToMaxSentences` normalizes the full string; `CompleteStreamingLine` enables **Continue**.

Deltas are drained on the **main thread** once per frame (worker thread only enqueues in the download handler), preserving UI responsiveness.

### 3.4 Warm-up requests

Ollama does not currently implement an explicit warm-up coroutine in production code. Recommended practice:

- On scene load or main menu, fire a **one-line throwaway generate** (`OllamaTestRunner` pattern) to load weights into VRAM.
- Keep Ollama daemon running during development sessions.

Document as **operational procedure**, not yet automated in DeepCover.

### 3.5 Perceived responsiveness techniques

| Technique | Implementation |
|-----------|----------------|
| Stream tokens to UI | `GenerateStreamAsync` + streaming dialogue APIs |
| Open panel early | `BeginStreamingLine` before HTTP completes |
| Short outputs | System + task lines limit to 3 sentences |
| Player lock during reply | `DialoguePlayerLock` via `DialogueManager` |
| Typed question UI | `DialogueQuestionInputView` — player commits intent while model prepares |
| Fallback copy | Profile-level fallback string on timeout / connection failure |
| Optional typewriter (non-stream) | `DialogueUIView` character reveal for buffered replies |

---

## 4. Data Flow

### 4.1 Canonical runtime path (AI interrogation turn)

The following reflects the **implemented** call chain (not a hypothetical cloud pipeline):

```
Player Input (New Input System, "Interact")
    → PlayerInteractor (camera raycast, IInteractable)
    → NPCBrain.Interact()
        → [optional] DialogueQuestionInputView.BeginSession (typed question)
        → [optional] DialoguePlayerLock.SetDialogueActive(true)
    → NPCBrain.RunAiTurnCoroutine(playerUtterance)
        → NPCMemory.AddPlayerUtterance
        → NpcDialoguePromptContext built from:
              NPCProfile, SuspicionManager, EvidenceInventory, NPCMemory, trust
        → NpcOllamaPromptBuilder.Build → (systemPrompt, userPrompt)
        → OllamaClient.GenerateStreamAsync | GenerateAsync
              → HTTP POST /api/generate (localhost:11434)
              → [stream] OllamaStreamingDownloadHandler (NDJSON chunks)
        → NpcDialogueResponseFormatter.ClampToMaxSentences
        → NPCMemory.AddNpcReply
        → ApplyPostTurnSystems (trust/suspicion deltas, UnityEvents)
    → DialogueManager
        → [stream] BeginStreamingLine / AppendStreamingLineText / CompleteStreamingLine
        → [buffered] StartDialogue(DialogueSequence) → DialogueUIView.DisplayLine
    → DialogueUIView (TextMeshPro body, speaker, continue)
    → Player Continue / ESC
    → DialogueManager.CloseDialogue → DialoguePlayerLock restore
    → DialogueEnded → NPCBrain clears requestInFlight
```

### 4.2 Component responsibilities

| Component | Role |
|-----------|------|
| `PlayerInteractor` | Detection, prompt UI, interact action |
| `DialogueQuestionInputView` | Optional typed question overlay (decoupled from `DialogueManager`) |
| `NPCBrain` | Turn state, memory, prompt context assembly, Ollama orchestration |
| `NPCProfile` (ScriptableObject) | Static persona, secrets, fallback line |
| `NpcOllamaPromptBuilder` | System/user prompt assembly, budgets, history trim |
| `OllamaClient` | HTTP transport, streaming parser, cancellation |
| `NpcDialogueResponseFormatter` | Post-decode sentence clamp |
| `DialogueManager` | Global dialogue lifecycle, player lock delegation |
| `DialogueUIView` | TMP rendering, streaming append, typewriter (non-stream) |
| `DialoguePlayerLock` | Disables `FirstPersonController` / `PlayerInteractor`, cursor lock |
| `SuspicionManager` | Global suspicion value + dialogue tag for prompts |
| `EvidenceInventory` | Evidence summary string for prompts |

### 4.3 Separation of concerns

- **NPCBrain** never references TMP or Canvas; it only calls `DialogueManager` public APIs.
- **DialogueManager** never references Ollama or `NPCProfile`.
- **OllamaClient** is pure C# + `UnityWebRequest` (testable in isolation via `OllamaTestRunner`).

This avoids circular dependencies and keeps UI swappable (e.g. alternate question UI or bark widgets).

---

## 5. Prompt Structure

Prompts follow Ollama’s **`/api/generate`** contract: a stable **system** string (behavior + persona) and a volatile **user** string (game state + player line).

### 5.1 System prompt (personality and rules)

Built in `NpcOllamaPromptBuilder.BuildSystemPrompt`:

- **Behavioral contract** (first lines weighted heavily by instruction-tuned models):
  - In-character only; no AI / Unity / Ollama meta.
  - Max **3 short sentences**; plain text, no markdown.
  - Do not dump classified lists; secrets are for subtext.
- **Identity:** `RoleTitle`, `CharacterDisplayName` from `NPCProfile`.
- **Personality** — truncated to `NpcPromptBudget.maxPersonalityChars` (default 600).
- **Voice guidelines** — `maxVoiceGuidelinesChars` (default 280).
- **Private knowledge** — `secretsForAi`, capped at `maxSecretsChars` (default 520); explicitly marked as not known to the investigator by default.

### 5.2 User prompt (volatile state)

Assembled in `AssembleUserBody` with bracket sections:

```
[STATE]
suspicion=<0–100> band=<Clear|Low|Medium|High|Critical>
suspicion_tag=<designer tag from SuspicionManager>
trust_npc_to_player=<0–100>

[EVIDENCE]
<EvidenceInventory.BuildEvidenceContextSummary() or "(None on record.)">

[HISTORY]
<NPCMemory.BuildTranscript() after TailConversationHistoryTrimmer>

[PLAYER]
"<player question>"

[TASK] Respond in character as <name>. Max 3 short sentences.
```

### 5.3 Suspicion context

`NPCBrain.BuildPromptContext` reads:

- `SuspicionManager.SuspicionValue`, `CurrentLevel`
- `ISuspicionDialogueContext.GetSuspicionDialogueTag()` when implemented

Suspicion affects **wording pressure** in the model, not Unity physics directly. Gameplay systems (alarms, combat) remain code-driven.

### 5.4 Evidence injection

Evidence is summarized to text, not passed as raw ScriptableObject graphs. Budget: `maxEvidenceChars` (default 900). Prevents a single turn from listing the entire case file.

### 5.5 Memory trimming

Two layers:

1. **Turn cap** — `NPCMemory` stores at most `maxMemoryTurns` (default 16) alternating player/NPC lines.
2. **Prompt trim** — `TailConversationHistoryTrimmer` + `maxHistoryChars` (default 1200); if user prompt exceeds `maxTotalUserPromptChars`, history budget shrinks iteratively before hard truncation.

Optional **summary notes** (`AddSummaryNote`) persist high-value facts above the rolling transcript.

### 5.6 Response limitations

| Layer | Limit |
|-------|-------|
| Prompt task line | 3 sentences |
| `NpcDialogueResponseFormatter` | Clamps at sentence boundaries (`.`, `!`, `?`) |
| `maxPlayerUtteranceChars` | 400 (anti-spam / paste) |

### 5.7 Anti-hallucination rules

The project uses **soft guardrails**, not hard retrieval grounding:

- Secrets labeled as private knowledge the investigator does not automatically know.
- Evidence block sourced only from inventory summary (no free-form “omniscient” case file in prompt unless collected).
- Explicit ban on meta references and markdown formatting.
- Short outputs reduce rambling confabulation.

**Known gap:** the model can still invent facts not in `[EVIDENCE]` or `[HISTORY]`. Mitigation strategies are listed in §6 and §7.

---

## 6. Risks and Limitations

### 6.1 Hallucinations

Suspects may fabricate alibis, names, or evidence connections. ScriptableObject profiles and evidence summaries reduce but do not eliminate this. High-stakes story beats should remain **authored** or validated by game logic flags.

### 6.2 Response delay

Even local models may take multiple seconds on CPU-only inference. Streaming improves **perceived** delay; total time may still exceed player patience on long prompts.

### 6.3 Memory growth

Unbounded transcript storage would inflate prompts and latency. Current caps (16 turns, 1200 history chars, 8 summary notes) trade continuity for stability.

### 6.4 Hardware requirements

Minimum practical spec depends on chosen Ollama model quantization. `llama3` full weights may be uncomfortable on 8 GB VRAM systems; `llama3.2:3b` is the documented fallback. QA should profile on target min-spec hardware.

### 6.5 Prompt instability

Small wording changes in `NPCProfile` or suspicion tags can shift tone dramatically between builds. Version control profiles and log prompts (`NPCBrain.logBuiltPromptToConsole`) during tuning.

### 6.6 Immersion risks

- Out-of-character AI disclaimers (mitigated by system rules).
- Over-long replies if formatter fails on unorthodox punctuation.
- Network errors surfacing as generic fallback `"…"` — consider in-world error copy per profile.
- Player movement during non-streaming Ollama wait (only streaming + question flows lock early today).

---

## 7. Optimization Strategies

### 7.1 Small prompts

- Aggressive `NpcPromptBudget` defaults (~4 chars ≈ 1 token rule of thumb in tooltips).
- Static persona in **system** (cached by Ollama server where applicable); rebuild only **user** each turn.

### 7.2 Limited memory

- Tail trimmer drops oldest turns first.
- Summary notes for critical facts instead of full verbatim logs.

### 7.3 Response caching

**Not implemented.** Future option: hash `(npcId, suspicionBand, evidenceSetHash, playerQuestionNormalized)` → cache reply for repeat questions in the same scene state.

### 7.4 Hybrid dialogue systems

Recommended production pattern:

| Beat type | Source |
|-----------|--------|
| Intro / arrest / major plot | Scripted `DialogueSequence` assets |
| Interrogation chatter | Ollama via `NPCBrain` |
| Barks / patrol | Short scripted pools |

`DialogueManager` already supports scripted sequences; AI replies use runtime `DialogueLineData.CreateRuntime`.

### 7.5 Short responses

Three-sentence cap is the single largest decode savings. Enforce in prompt, post-process, and UI layout (panel sized for short copy).

### 7.6 Additional engineering optimizations

- Per-NPC model downgrade (e.g. `llama3.2:3b` for crowd NPCs, larger model for primary suspect).
- Disable streaming only on low-end quality settings (toggle `useStreamingResponses`).
- Pre-warm Ollama on scene load.
- Cancel in-flight requests when player walks away (hook `DialogueEnded` — partially implemented).

---

## 8. Future Improvements

| Area | Proposal |
|------|----------|
| **Structured output** | JSON schema for `{ "line": "...", "emotion": "defensive" }` parsed by dedicated `ResponseParser` |
| **RAG / grounding** | Embed collected evidence; retrieve top-k facts into `[EVIDENCE]` instead of full inventory dump |
| **Conversation summarization** | Async summarizer replaces raw `[HISTORY]` over N turns (`IConversationHistoryTrimmer` extension point exists) |
| **Emotional state machine** | Drive animator/blendshapes from `onConversationTurnComplete` + parsed tags |
| **Branching choices** | `IDialogueBranchingDialogue` on `NPCBrain` for structured choices before generate |
| **Cloud fallback** | Optional remote API for min-spec machines (privacy policy required) |
| **Analytics** | Local log of prompt hash, latency, token estimate for tuning |
| **Automated warm-up** | Scene bootstrap service calling minimal generate on load |
| **Question UI default** | Scene tooling ensures `DialogueQuestionInputView` wired when `requireTypedQuestionBeforeAi` is set |
| **Model routing** | ScriptableObject `OllamaModelProfile` per act / location |

---

## Appendix A — Configuration reference

| Setting | Location | Default |
|---------|----------|---------|
| Ollama URL | `NPCBrain.ollamaBaseUrl` | `http://localhost:11434` |
| Model | `NPCBrain.ollamaModel` | `llama3` |
| Timeout | `NPCBrain.ollamaTimeoutSeconds` | 120 |
| Streaming | `NPCBrain.useStreamingResponses` | `true` |
| Memory turns | `NPCBrain.maxMemoryTurns` | 16 |
| Open reply UI | `NPCBrain.openDialogueWithReply` | `true` |

## Appendix B — Key source files

| File | Purpose |
|------|---------|
| `Assets/Scripts/Systems/Ollama/OllamaClient.cs` | HTTP generate + stream |
| `Assets/Scripts/Systems/Ollama/OllamaStreamingDownloadHandler.cs` | NDJSON chunk parser |
| `Assets/Scripts/Systems/Ollama/OllamaCancelToken.cs` | Cancellation |
| `Assets/Scripts/Systems/AiNpc/NPCBrain.cs` | Gameplay orchestration |
| `Assets/Scripts/Systems/AiNpc/NpcOllamaPromptBuilder.cs` | Prompt assembly |
| `Assets/Scripts/Systems/AiNpc/NpcPromptBudget.cs` | Character budgets |
| `Assets/Scripts/Systems/AiNpc/NPCMemory.cs` | Per-NPC transcript |
| `Assets/Scripts/Systems/AiNpc/NpcDialogueResponseFormatter.cs` | Sentence clamp |
| `Assets/Scripts/Managers/DialogueManager.cs` | Dialogue + streaming UI control |
| `Assets/Scripts/UI/DialogueUIView.cs` | TMP presentation |
| `Assets/Scripts/Systems/AiNpc/NPCProfile.cs` | Authoring |

## Appendix C — Operational checklist

1. Install and start Ollama; verify `ollama list` includes configured model tag.
2. Run DeepCover scene; confirm single `EventSystem` and HUD canvas.
3. Run **Tools → Spy Game → Validate And Fix Safe Issues** if question UI is required.
4. Interact with NPC — confirm stream append or fallback line on failure.
5. Press ESC during stream — confirm request abort and control restore.

---

*This document describes the DeepCover Ollama integration as implemented in the Unity project. Update when model defaults, budgets, or dialogue flow components change.*
