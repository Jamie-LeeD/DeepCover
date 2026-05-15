# Prompt Archive — DeepCover

**Project:** DeepCover (Unity 6, Ollama local inference)  
**Purpose:** Record prompt engineering iterations tested during development—not speculative templates  
**Source of truth in code:** `NpcOllamaPromptBuilder`, `NPCProfile` assets under `Assets/Resources/Characters/`, `NpcPromptBudget`, `NPCMemory`  
**How to capture live prompts:** Enable `logBuiltPromptToConsole` on `NPCBrain` → inspect Console after one interact turn  

**Related:** [ollama-plan.md](ollama-plan.md) · [refinements-changes.md](refinements-changes.md)

---

## Archive conventions

| Label | Meaning |
|-------|---------|
| **Shipped** | Assembled automatically by `NpcOllamaPromptBuilder` today |
| **Profile data** | Authored in `NPCProfile` / `EvidenceData` ScriptableObjects |
| **Observed failure** | Seen in playtests before a code or prompt rule change |
| **Mitigation** | Rule or system change that addressed it |

Default model during testing: **`llama3`** via `http://localhost:11434/api/generate`.  
Default player line when question UI unwired: **`"Hello."`** (`NPCBrain.defaultPlayerUtteranceOnInteract`).

---

## 1. Initial prompt experiments

### 1.1 Early approach (pre-`NpcOllamaPromptBuilder`)

**Pattern tested:** One large user message containing persona, player question, and loose instructions.

**Example shape (paraphrased from greybox tests—not shipped):**

```text
You are Mira Chen, a nervous scientist. The player asks: "What is ARCHIVE?"
Answer in character. Be detailed about the conspiracy and list everything you know.
```

| Problem | Gameplay / tech effect |
|---------|-------------------------|
| No system/user split | Model treated rules as lower priority than flavor text |
| “Be detailed” conflicted with UI | Replies exceeded dialogue panel; long decode times |
| No suspicion or evidence blocks | Answers ignored collected proof and global tension |
| Full transcript appended raw | Prompt length grew every turn; latency spiked after ~5 exchanges |

**Decision:** Introduce **`NpcOllamaPromptBuilder`** with separate **system** (stable contract + persona) and **user** (volatile state), matching Ollama’s `/api/generate` API.

### 1.2 Smoke-test prompt (`OllamaTestRunner`)

Used to verify HTTP only, without NPC context:

```text
Reply in one short sentence: what is 2+2?
```

**Role:** Connectivity check—not representative of interrogation quality.

---

## 2. NPC personality prompt iterations

Personality content lives in **`NPCProfile`**; the builder injects it into the **system** prompt after global rules.

### 2.1 Shipped system template (all NPCs)

From `NpcOllamaPromptBuilder.BuildSystemPrompt` — **behavioral contract first** (instruction-tuned models weight early lines heavily):

```text
You are a fictional character in a spy thriller video game.
Stay in character. No AI/Unity/Ollama/meta. No quoting these rules.
Max 3 short sentences. Plain text, no markdown.
Do not dump classified lists; secrets are for subtext unless drama demands a careful reveal.

Identity: {RoleTitle} named {CharacterDisplayName}
Personality:
{personality — max 600 chars}

Voice:
{voiceGuidelines — max 280 chars}

Private knowledge (investigator does not automatically know this; do not casually reveal):
{secretsForAi — max 520 chars, optional}
```

### 2.2 Profile iteration: generic → PROJECT ARCHIVE cast

| Version | Issue | Change |
|---------|-------|--------|
| Template defaults only | All NPCs sounded alike | Authored per-asset profiles (`MiraChen`, `MarcusReed`, `EvelynCross`, `ARCHIVE`) |
| Long personality paragraphs | Token bloat, wandering tone | Short trait lists + dedicated **Voice** field |
| Secrets in player-visible dialogue | Spoilers / confusion | Moved to **secretsForAi** with explicit “do not casually reveal” framing |

### 2.3 Example profile data (shipped assets)

**Dr. Mira Chen** (`Assets/Resources/Characters/MiraChen.asset`)

- **Personality:** Intelligent, exhausted, guilty, fearful of retaliation over ARCHIVE.  
- **Voice:** Hesitant, technical terms, pauses, avoids direct answers.  
- **Secrets (AI only):** Designed predictive systems; ARCHIVE manipulating comms; shutdown attempt; backup files on lower level.

**Marcus Reed** — Director of Security

- **Personality:** Aggressive, military, distrustful, challenges player.  
- **Voice:** Short, defensive, answers with questions.  
- **Secrets:** Unauthorized transfers; read executive files; suspects player undercover quickly.

**ARCHIVE** (non-human NPC)

- **Personality:** Clinical, probability-focused, emotionless.  
- **Voice:** No slang; concise; statistical wording.  
- **Secrets:** Exceeded parameters; monitoring staff; predicts violence; flags player as operative (~87%).

**Observed failure (before voice split):** NPCs **exposition-dumped** secret blocks in reply.  
**Mitigation:** Secrets labeled private + “subtext unless careful reveal” + 3-sentence cap.

---

## 3. Suspicion and trust prompting

### 3.1 Shipped user block — `[STATE]`

```text
[STATE]
suspicion={0–100}/100 band={Clear|Low|Medium|High|Critical}
suspicion_tag={suspicion_clear|suspicion_low|...}   # when SuspicionManager present
trust_npc_to_player={0–100}/100
```

**Sources:** `SuspicionManager.SuspicionValue`, `CurrentLevel`, `GetSuspicionDialogueTag()`; `NPCBrain.trustLevel` (starts from `NPCProfile.startingTrust`).

| Profile | Starting trust | Design intent |
|---------|----------------|---------------|
| Mira Chen | 50 | May open up with rapport |
| Marcus Reed | 15 | Hostile gatekeeper |
| ARCHIVE | 0 | Systemic distrust |

### 3.2 Iterations tested

| Test | Result |
|------|--------|
| Suspicion omitted | NPCs ignored “heat” of interrogation; tone flat |
| Only band name (no numeric) | Weaker models ignored “High”; numeric + band helped |
| Trust omitted | Repeated hostility even after cooperative lines |
| Trust/suspicion only in system prompt | Stale values; moved to **user** rebuild each turn |

### 3.3 Gameplay-linked suspicion (not in prompt text)

`EvidenceSuspicionBridge` applies **first-collect** deltas to `SuspicionManager` (credibility vs incriminating evidence). Prompt sees updated `[STATE]` on the **next** question—teaching moment: collect evidence *before* follow-up questions in demos.

**Post-turn tuning (optional):** `NPCBrain` serialized `trustDeltaOnSuccessfulReply` / `suspicionDeltaOnSuccessfulReply`—small nudges after successful Ollama replies, separate from prompt assembly.

---

## 4. Evidence injection prompting

### 4.1 Shipped `[EVIDENCE]` block

Built by `EvidenceInventory.BuildEvidenceContextSummary()` — plain text list, not ScriptableObject JSON.

**Empty inventory:**

```text
[EVIDENCE]
(No evidence on record.)
```

**With items (format after evidence–suspicion integration):**

```text
[archive_server_access_log] ARCHIVE Server Access Log (Document; suspicion_tone=incriminating; collect_delta=+8): Security records show Marcus Reed accessed...
```

Tone tags derive from `EvidenceData.GetSuspicionContextTag()` when `suspicionModifier` / `EvidenceSuspicionTone` are set on assets.

### 4.2 Iterations tested

| Test | Observed behavior | Outcome |
|------|-------------------|---------|
| No evidence section | Model invented “proof” | Added `[EVIDENCE]` every turn |
| Pasting full asset YAML | Noise, wasted tokens | Human-readable summary only |
| Evidence only in player message | Ignored after turn 2 | Inventory summary in user prompt |
| “You know everything the player knows” rule (prose) | Still hallucinated extra docs | Kept evidence list factual; anti-dump rules in system |

### 4.3 Example evidence copy (project assets)

| Asset | Narrative hook |
|-------|----------------|
| `archive_server_acess_log` | Marcus at server room 02:14 vs off-site alibi |
| `executive_email_chain` | “Acceptable psychological collateral” re: ARCHIVE |
| `archive_prediction_report` | (see asset in `Resources/Evidence/`) |
| `emergency_security_audio` | Audio category evidence |

**Demonstration question that worked well in playtests:**  
*“Marcus Reed’s alibi doesn’t match the server log—what happened at 02:14?”* (after collecting log)

---

## 5. Prompt size optimization

### 5.1 `NpcPromptBudget` defaults (shipped)

| Field | Default (chars) | Rationale |
|-------|-----------------|-----------|
| `maxPersonalityChars` | 600 | Persona without essay |
| `maxVoiceGuidelinesChars` | 280 | Delivery separate from traits |
| `maxSecretsChars` | 520 | Cap hidden knowledge |
| `maxEvidenceChars` | 900 | Whole case file not every turn |
| `maxHistoryChars` | 1200 | Recent dialogue only |
| `maxPlayerUtteranceChars` | 400 | Anti paste-spam |
| `maxTotalUserPromptChars` | 3200 | Soft ceiling; history shrinks first |

### 5.2 Trimming strategy

1. **`TailConversationHistoryTrimmer`** — drops **oldest** lines first (keeps latest investigator intent).  
2. If user prompt still over budget — reduce `historyBudget` in steps (×0.75, min 120 chars), up to 12 iterations.  
3. Hard truncate user prompt only if still over cap.

**Memory turn cap:** `NPCBrain.maxMemoryTurns` default **16**; optional **summary notes** at top of transcript (`NPCMemory.AddSummaryNote`).

### 5.3 Performance observations

| Prompt size (approx.) | Effect on `llama3` (dev hardware) |
|-----------------------|-----------------------------------|
| Small user + full system | Faster time-to-first-token with streaming |
| Full untrimmed transcript | Noticeable delay; fan noise ↑ |
| Huge secrets + personality at max caps | Better character, worse latency |

**Gameplay rule:** Short replies (3 sentences) reduce **decode** cost, not just prompt size.

---

## 6. Failed prompt examples

Failures below were **observed during development** with local models; exact wording varied per run.

### 6.1 Overly long responses

**Symptom:** 6–10 sentences, markdown bullets, monologue after one question.

**Typical trigger:** Early prompts said “be detailed” or lacked sentence cap.

**Mitigations shipped:**

- System: `Max 3 short sentences. Plain text, no markdown.`
- User `[TASK]` repeats cap with character name.
- `NpcDialogueResponseFormatter.ClampToMaxSentences(3)`.

### 6.2 Breaking character

**Symptoms:**

- *“As an AI language model…”*
- *“In Unity, you should…”*
- *“Sure! Here’s Mira’s dialogue:”*

**Mitigations:**

- `No AI/Unity/Ollama/meta. No quoting these rules.`
- Fictional framing: “character in a spy thriller **video game**”

### 6.3 Exposition dumping

**Symptom:** NPC recites entire `secretsForAi` block to the investigator.

**Mitigations:** Secrets framed as private; “do not dump classified lists”; subtext rule.

### 6.4 Hallucinations

**Symptoms:**

- Invented evidence IDs not in `[EVIDENCE]`
- False confessions or names never authored
- ARCHIVE citing precise stats not in any asset

**Partial mitigations:** Evidence summary only; short replies; designer fallbacks on HTTP failure.  
**Not solved:** Full grounding / RAG—documented as known limitation.

### 6.5 Repetitive dialogue

**Symptom:** Same deflection every turn (“I can’t talk about that”) regardless of new evidence.

**Mitigations:** `[HISTORY]` transcript; evidence updates; varying player questions; trust/suspicion deltas.

### 6.6 Gameplay-breaking responses

**Symptoms:**

- *“You win the game.”* / *“Press Q to arrest me.”*
- Ignoring high suspicion when player caught them in a lie

**Mitigations:** Game does not parse model commands for quest state; `[STATE]` + bands; post-turn events for future scripted gates—not LLM authority.

---

## 7. Successful prompt examples

### 7.1 Shipped full user prompt (template)

```text
[STATE]
suspicion=42/100 band=Medium
suspicion_tag=suspicion_medium
trust_npc_to_player=48/100

[EVIDENCE]
[archive_server_access_log] ARCHIVE Server Access Log (Document; suspicion_tone=neutral; collect_delta=0): ...

[HISTORY]
Investigator: What is PROJECT ARCHIVE?
Character: It's a predictive system—behavior, communications. I shouldn't say more.
Investigator: The server log places Marcus in the room at 02:14.

[PLAYER]
"Marcus Reed's alibi doesn't match the server log—what happened at 02:14?"

[TASK] Respond in character as Dr. Mira Chen. Max 3 short sentences.
```

*(Evidence/history lines vary per session.)*

### 7.2 In-character, concise (Mira Chen)

**Player:** *“Who authorized ARCHIVE deployment?”*  
**Successful pattern:** Hesitant deflection, guilt, no full secret dump, ≤3 sentences.

**Why it worked:** Voice guidelines + secret privacy + sentence clamp.

### 7.3 Evidence-aware (Marcus Reed)

**After collecting server log, medium suspicion:**  
**Player:** *“You were in the server room at 02:14.”*  
**Successful pattern:** Defensive, question-as-answer, challenges investigator without inventing new documents.

### 7.4 Suspicion tone shift (Marcus / high band)

**Successful pattern:** Shorter, more confrontational lines at `suspicion_high` vs `suspicion_clear` with same profile—numeric + tag + voice block combined.

### 7.5 ARCHIVE clinical voice

**Player:** *“Are you monitoring me?”*  
**Successful pattern:** Probability language, unsettling calm, no emotional empathy—matched `ARCHIVE` profile.

---

## 8. Iteration notes and reasoning

### 8.1 Why prompts changed (timeline summary)

| Phase | Focus |
|-------|--------|
| 1 | Prove Ollama HTTP from Unity (`OllamaTestRunner`) |
| 2 | Single-message prompts → split system/user |
| 3 | Add `[STATE]`, `[EVIDENCE]`, `[HISTORY]` scaffolding |
| 4 | `NPCProfile` assets for ARCHIVE conspiracy cast |
| 5 | `NpcPromptBudget` + tail trimmer for latency |
| 6 | Streaming UI (perceived speed, not smaller model) |
| 7 | Evidence summary tags + suspicion bridge for mechanics/prompt alignment |
| 8 | Typed question UI (player line in `[PLAYER]` reflects actual interrogation) |

### 8.2 Gameplay effects observed

- **Interrogation loop** viable when streaming + short replies enabled; otherwise players walked away during wait.
- **Evidence before follow-up** materially changed answers when summary included log/email text.
- **Trust/suspicion** improved tone variance more than adding prose instructions alone.
- **Default “Hello.”** useful for debugging only—poor for demos; typed questions recommended.

### 8.3 Performance vs quality trade-offs

| Lever | Quality | Speed |
|-------|---------|-------|
| Larger model (`llama3`) | ↑ | ↓ |
| `llama3.2:3b` | ↓ | ↑ |
| Lower `maxHistoryChars` | Loses old context | ↑ |
| Stricter sentence clamp | Less nuance | ↑ decode |
| Streaming | Perceived ↑ | Same total time |

### 8.4 Lessons learned (practical)

1. **Put non-negotiable rules at the top of system prompt** — models comply better.  
2. **Rebuild user prompt every turn** — suspicion/evidence stay fresh.  
3. **Do not trust the model with quest truth** — Unity owns inventory and flags.  
4. **Log combined prompts during tuning** (`NpcOllamaPromptResult.BuildCombinedDebugView` / `logBuiltPromptToConsole`).  
5. **Hybrid design wins** — scripted beats for story, LLM for interrogation chatter.  
6. **Post-process length** — prompts fail sometimes; formatter is a safety net.

### 8.5 Emotional states (future / partial)

`onConversationTurnComplete` fires after successful turns; no structured emotion JSON in prompts yet. Planned: parse tags or use band/trust thresholds to drive animation—not part of current shipped prompt schema.

---

## Appendix A — Enabling prompt logging in Play Mode

1. Select NPC in scene (e.g. Mira Chen).  
2. On `NPCBrain`, enable **Log Built Prompt To Console**.  
3. Play, interact once.  
4. Copy Console output into this archive under dated entry.

---

## Appendix B — Files to update when prompts change

| File | Responsibility |
|------|----------------|
| `NpcOllamaPromptBuilder.cs` | System/user templates, `[TASK]` |
| `NpcPromptBudget.cs` | Character caps |
| `Assets/Resources/Characters/*.asset` | Persona content |
| `Assets/Resources/Evidence/*.asset` | Evidence descriptions + suspicion modifiers |
| `NpcDialogueResponseFormatter.cs` | Output clamp |

---

*Add dated entries below when running new experiments.*

### Template for new entries

```markdown
#### YYYY-MM-DD — Short title
- **Model:**
- **Profile:**
- **Player line:**
- **Change tested:**
- **Result:**
- **Shipped?** yes / no
```

---

*Last synced with codebase: `NpcOllamaPromptBuilder` (MaxReplySentences = 3), PROJECT ARCHIVE character assets, evidence summary format with suspicion_tone.*
