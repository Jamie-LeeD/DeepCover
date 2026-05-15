# DeepCover — Complete Setup Guide

This guide explains how to install, configure, and run **DeepCover** from scratch on a new machine. DeepCover is a first-person narrative spy thriller built in **Unity 6** with **local AI dialogue** powered by **Ollama**.

**Related documentation:** [ollama-plan.md](ollama-plan.md) (technical architecture and prompt design)

---

## Table of contents

1. [Project requirements](#1-project-requirements)
2. [Software installation](#2-software-installation)
3. [Ollama setup](#3-ollama-setup)
4. [Recommended models](#4-recommended-models)
5. [Unity project setup](#5-unity-project-setup)
6. [Running the game](#6-running-the-game)
7. [Troubleshooting](#7-troubleshooting)
8. [Performance recommendations](#8-performance-recommendations)

---

## 1. Project requirements

### Operating system

| Platform | Supported |
|----------|-----------|
| **Windows 10/11** (64-bit) | Primary development target |
| **macOS** (Apple Silicon or Intel) | Supported for Unity + Ollama |
| **Linux** | Supported for Unity + Ollama |

Ollama and Unity must both run on the **same machine** during play (dialogue uses `http://localhost:11434`).

### Recommended hardware (comfortable development)

| Component | Recommendation |
|-----------|----------------|
| **CPU** | 6+ cores (modern Intel/AMD or Apple M-series) |
| **RAM** | **16 GB minimum**, **32 GB recommended** (Unity Editor + Ollama model weights) |
| **GPU** | Dedicated GPU with **8 GB+ VRAM** for Unity URP + local LLM inference |
| **Storage** | **20 GB+ free** (Unity Editor, Library cache, Ollama models, project assets) |

### Minimum hardware (possible, slower AI)

| Component | Minimum |
|-----------|---------|
| **RAM** | 16 GB |
| **GPU** | 6 GB VRAM or CPU-only Ollama (much slower dialogue) |
| **Storage** | 15 GB free |

### Notes

- **Unity Editor** and **Ollama** compete for GPU memory on the same PC. Close heavy GPU apps while testing dialogue.
- First Ollama request after idle may be slow (model load). See [Performance recommendations](#8-performance-recommendations).

---

## 2. Software installation

Install tools in this order: **Git (optional)** → **Unity Hub** → **Unity 6** → **Ollama**.

### 2.1 Unity Hub

1. Download **Unity Hub** from [https://unity.com/download](https://unity.com/download).
2. Run the installer and sign in with a Unity account (free Personal license is fine).
3. Open Unity Hub when installation finishes.

### 2.2 Unity 6 (matching this project)

This repository targets:

```text
Unity 6000.3.9f1
```

**Install via Unity Hub:**

1. Go to **Installs** → **Install Editor**.
2. Select **Unity 6** (6000.3.x) or add version **6000.3.9f1** if Hub offers it.
3. Include modules:
   - **Microsoft Visual Studio Community** (or JetBrains Rider) — C# IDE
   - **Windows Build Support (IL2CPP)** — if you build for Windows
4. Wait for the download and install to complete.

> **Tip:** If Hub does not list `6000.3.9f1` exactly, install the closest **6000.3.x** patch. Unity may upgrade the project slightly on first open; allow the migration if prompted.

### 2.3 Ollama

See [Section 3 — Ollama setup](#3-ollama-setup) for detailed steps.

Quick link: [https://ollama.com/download](https://ollama.com/download)

### 2.4 Required Unity packages (included in project)

Packages are declared in `Packages/manifest.json` and restore automatically when you open the project. Key dependencies:

| Package | Version (project) | Purpose |
|---------|-------------------|---------|
| `com.unity.inputsystem` | 1.18.0 | New Input System (WASD, mouse, Interact) |
| `com.unity.render-pipelines.universal` | 17.3.0 | URP rendering |
| `com.unity.ugui` | 2.0.0 | UI Canvas |
| TextMesh Pro | Bundled with uGUI | Dialogue and HUD text |

You do **not** need to manually install Input System or URP from Package Manager unless Unity reports a missing package error.

### 2.5 Git (optional)

To clone the repository:

```bash
git clone <your-repo-url> DeepCover
cd DeepCover
```

If you received a ZIP, extract it to a folder such as `Documents/Unity Project/DeepCover`.

---

## 3. Ollama setup

Ollama runs a local HTTP server that Unity calls for NPC dialogue.

### 3.1 Install Ollama

**Windows**

1. Download the installer from [https://ollama.com/download](https://ollama.com/download).
2. Run the installer and finish setup.
3. Ollama usually starts in the system tray (background service).

**macOS**

```bash
brew install ollama
```

Or use the macOS app from the website.

**Linux**

```bash
curl -fsSL https://ollama.com/install.sh | sh
```

### 3.2 Run Ollama

Ollama must be running **before** you press Play in Unity.

**Windows / macOS (app)**  
Launch **Ollama** from the Start menu or Applications. The tray/menu bar icon indicates it is running.

**Terminal (all platforms)**

```bash
ollama serve
```

Leave this window open if you start the server manually. On many installs, the background service already listens on port **11434**.

### 3.3 Pull required models

The project default model name is **`llama3`** (set on `NPCBrain` and in `SampleScene`).

Pull it once:

```bash
ollama pull llama3
```

This downloads several gigabytes. Wait until the command completes.

### 3.4 Example commands

| Task | Command |
|------|---------|
| List installed models | `ollama list` |
| Pull default project model | `ollama pull llama3` |
| Pull smaller/faster model | `ollama pull llama3.2:3b` |
| Quick test in terminal | `ollama run llama3 "Say hello in one sentence."` |
| Stop a running model in chat | `/bye` |
| Show model info | `ollama show llama3` |

### 3.5 Verify Ollama is running locally

**Option A — Browser**

Open:

```text
http://localhost:11434
```

You should see a simple response (often `Ollama is running` or similar).

**Option B — Terminal**

```bash
curl http://localhost:11434/api/tags
```

You should get JSON listing installed models.

**Option C — Unity scene**

The project may include an **OllamaTestRunner** object in `SampleScene` that logs a test prompt on Play. Check the **Console** for `[Ollama]` or `[Ollama Stream]` messages.

---

## 4. Recommended models

### 4.1 Default (project configuration)

| Model | Command | Use case |
|-------|---------|----------|
| **llama3** | `ollama pull llama3` | Default balance of quality and speed |

Match the name **exactly** in the Inspector (`NPCBrain` → **Ollama Model**). Tags matter (`llama3.2:3b` ≠ `llama3`).

### 4.2 Lightweight alternatives (slower GPUs / laptops)

| Model | Command | Notes |
|-------|---------|-------|
| **llama3.2:3b** | `ollama pull llama3.2:3b` | Smaller, faster, less VRAM |
| **mistral** | `ollama pull mistral` | Alternative style; test in your scene |
| **phi3** (if available in your Ollama catalog) | `ollama pull phi3` | Very small; quality varies |

After pulling, set **Ollama Model** on each `NPCBrain` to the same string shown by `ollama list`.

### 4.3 Performance considerations

- **Larger models** → better prose, higher latency and VRAM.
- **Smaller models** → worse reasoning, much better time-to-first-token.
- DeepCover caps replies to **three sentences** in prompts and code; huge models are usually unnecessary for this use case.
- Use **streaming** (enabled by default on `NPCBrain` → **Use Streaming Responses**) so text appears while the model generates.

---

## 5. Unity project setup

### 5.1 Open the project

1. Open **Unity Hub**.
2. Click **Add** → **Add project from disk**.
3. Select the **DeepCover** folder (the one containing `Assets`, `Packages`, and `ProjectSettings`).
4. Open the project with editor version **6000.3.9f1** (or closest 6000.3.x).
5. Wait for the first import (can take several minutes). The `Library` folder will be created.

### 5.2 Import packages / first compile

Unity reads `Packages/manifest.json` automatically. When the Editor finishes importing:

1. Check the **Console** for compile errors (red messages).
2. If prompted about **Input System**, choose **Yes** to enable the new backend (this project requires it).

### 5.3 Input System setup

This project uses the **Unity Input System** package.

**Player settings**

1. **Edit → Project Settings → Player**
2. **Other Settings → Active Input Handling**
3. Set to **Input System Package (New)** or **Both** (if you need legacy input elsewhere).

**Input Actions asset**

- Asset path: `Assets/InputSystem_Actions.inputactions`
- **PlayerInteractor** and **FirstPersonController** reference this asset for the **Player** action map (Move, Look, Interact, etc.).

If references are missing on the **Player** object, assign `InputSystem_Actions` in the Inspector.

### 5.4 TextMesh Pro (TMP) setup

TMP is required for dialogue and HUD.

On first open, Unity may show:

> **TMP Importer** — Import TMP Essentials

1. Click **Import TMP Essentials**.
2. Optionally import **TMP Examples & Extras** (not required to run).

If fonts look wrong:

- **Window → TextMeshPro → Font Asset Creator** (only if you add custom fonts)
- Default TMP settings: `Edit → Project Settings → TextMesh Pro`

### 5.5 Scene and UI wiring (important)

Main playable scene: **`Assets/Scenes/SampleScene.unity`**

**Recommended editor tools** (menu bar):

| Menu | Purpose |
|------|---------|
| **Tools → Create Spy Game UI** | Builds full HUD + dialogue stack (first-time setup) |
| **Tools → Add Dialogue Question Input To Canvas** | Adds typed-question panel to existing canvas |
| **Tools → Spy Game → Validate And Fix Safe Issues** | Finds missing references and wires NPCs |

**Typical first-time wiring checklist**

1. Open `SampleScene`.
2. Run **Tools → Spy Game → Validate And Fix Safe Issues**.
3. Save the scene (**Ctrl+S**).
4. Confirm in Hierarchy:
   - **DialogueManager** (under Managers)
   - **SpyGame_MainCanvas** (HUD + dialogue)
   - **Player** with `FirstPersonController`, `PlayerInteractor`, `DialoguePlayerLock`
   - **EventSystem** (exactly one)
   - NPCs with `NPCBrain` + collider

See [Trouhooting — missing references](#missing-serialized-references-in-the-scene).

---

## 6. Running the game

### 6.1 Start Ollama (every session)

```bash
ollama list
```

Confirm `llama3` (or your chosen model) appears. If not:

```bash
ollama pull llama3
```

Ensure the server is up:

```bash
curl http://localhost:11434/api/tags
```

### 6.2 Launch the Unity scene

1. Open **SampleScene** (`Assets/Scenes/SampleScene.unity`).
2. Press **Play**.
3. Use **WASD** + mouse to move (first-person).
4. Aim at an NPC until the interaction prompt appears.
5. Press **E** (Interact).

### 6.3 Testing NPC dialogue

**With typed questions enabled** (`NPCBrain` → **Require Typed Question Before Ai**):

1. Interact (**E**) → question panel opens.
2. Type a question → **Submit** or **Enter**.
3. Dialogue panel streams the AI reply (if **Use Streaming Responses** is on).
4. Press **Continue** or **ESC** to close.

**Without typed questions**

- Interact sends a default line (e.g. `"Hello."`) and waits for the full reply.

**Verify in Console**

- Success: NPC speaks; no red errors.
- Failure: warnings such as connection failed or HTTP 404 (wrong model name).

### 6.4 Build settings (optional)

**File → Build Settings** — `SampleScene` should be in **Scenes In Build**. It is the default build scene in this project.

---

## 7. Troubleshooting

### Ollama not responding

| Symptom | Fix |
|---------|-----|
| `Connection failed. Is Ollama running?` | Start Ollama app or `ollama serve` |
| Empty response | Run `curl http://localhost:11434/api/tags` |
| HTTP 404 | Model name mismatch — run `ollama list`, match **Ollama Model** on `NPCBrain` exactly |
| Firewall blocking | Allow Ollama on private networks (Windows Defender) |

Test outside Unity:

```bash
ollama run llama3 "Reply in one short sentence."
```

### Missing serialized references in the scene

| Symptom | Fix |
|---------|-----|
| No interaction prompt | Assign **Interaction Prompt UI** on `PlayerInteractor` |
| Dialogue does not open | **DialogueManager** → assign **Dialogue View** and **Player Lock** |
| Typed question never appears | Run **Tools → Add Dialogue Question Input To Canvas**, assign **Question Input View** on `NPCBrain` |
| NPC ignores custom questions | Enable **Require Typed Question Before Ai** only when question UI exists |

Run:

```text
Tools → Spy Game → Validate And Fix Safe Issues
```

### Slow AI responses

- Use a smaller model (`llama3.2:3b`).
- Close other GPU applications.
- Keep prompts short (project already limits memory and evidence size).
- Enable **Use Streaming Responses** on `NPCBrain` so text appears early.
- Warm up Ollama before testing: `ollama run llama3 "hi"` then exit.

### Input System errors

| Symptom | Fix |
|---------|-----|
| `InvalidOperationException` input backend | **Player Settings → Active Input Handling** → Input System Package |
| Interact does nothing | Assign `Assets/InputSystem_Actions.inputactions` on Player |
| Actions not found | Confirm **Player** action map contains **Interact** |

After changing input settings, restart Play mode.

### TMP issues

| Symptom | Fix |
|---------|-----|
| Pink/missing text | **Window → TextMeshPro → Import TMP Essential Resources** |
| Null font on UI | Re-run **Tools → Create Spy Game UI** or assign LiberationSans SDF / default TMP font |
| Input field invisible | Ensure **DialogueQuestionInputView** exists under canvas with EventSystem present |

### Duplicate EventSystem

Only **one** EventSystem should exist. Delete extras in the Hierarchy (UI buttons will double-fire or fail).

### Dialogue never ends / cannot interact again

- Press **ESC** to close dialogue.
- If stuck, stop Play mode — usually `requestInFlight` on `NPCBrain`; check **Console** for Ollama errors.

---

## 8. Performance recommendations

### 8.1 Reducing latency

1. **Use streaming** — `NPCBrain` → **Use Streaming Responses** (default ON).
2. **Smaller model** — e.g. `llama3.2:3b` on laptops.
3. **Warm Ollama** before playtest (one short `ollama run` in terminal).
4. **Keep Ollama running** between Play sessions to avoid model unload.
5. **Limit evidence collection spam** — huge inventories still summarize, but very long sessions add prompt size.

### 8.2 Model and memory settings (Inspector)

On each **NPCBrain**:

| Field | Suggestion |
|-------|------------|
| **Ollama Model** | `llama3.2:3b` for speed, `llama3` for quality |
| **Ollama Timeout Seconds** | 120 default; lower only if you want faster fail |
| **Max Memory Turns** | 16 default; lower for shorter prompts |
| **Use Streaming Responses** | ON for perceived speed |

### 8.3 RAM and VRAM budgeting (rule of thumb)

| Setup | Guidance |
|-------|----------|
| 16 GB RAM | Use `llama3.2:3b`, close browser tabs during play |
| 32 GB RAM | Comfortable with `llama3` |
| 8 GB VRAM GPU | Prefer small quantizations; monitor Task Manager |
| CPU-only Ollama | Usable for testing; not ideal for demo builds |

### 8.4 Unity Editor tips

- Use **Play Mode** with **Maximize on Play** only if you need it; Editor UI uses RAM.
- Delete `Library` only when fixing corruption (forces long reimport) — not a performance tune.

---

## Quick reference

| Item | Value |
|------|--------|
| Unity version | `6000.3.9f1` |
| Main scene | `Assets/Scenes/SampleScene.unity` |
| Ollama URL | `http://localhost:11434` |
| Default model | `llama3` |
| Input actions | `Assets/InputSystem_Actions.inputactions` |
| Architecture doc | [ollama-plan.md](ollama-plan.md) |

### Minimum daily workflow

```bash
# Terminal 1 — ensure Ollama is ready
ollama list
curl http://localhost:11434/api/tags
```

```text
# Unity
1. Open SampleScene
2. Press Play
3. Interact with NPC (E)
```

---

*DeepCover — local AI dialogue is for development and single-player use. Ship builds only with models and hardware targets you have tested.*
