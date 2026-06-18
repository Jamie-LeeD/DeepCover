using System.Text;
using UnityEngine;

/// <summary>
/// Assembles token-conscious system and user prompts for local Ollama NPC dialogue.
/// Design goals: keep rules in system (stable behavior), put volatile game state in user (cheaper to rebuild each turn),
/// and cap every long text block so designers cannot accidentally ship megabyte prompts.
/// </summary>
public sealed class NpcOllamaPromptBuilder
{
    private const int MaxReplySentences = 3;

    private readonly NpcPromptBudget budget;
    private readonly IConversationHistoryTrimmer historyTrimmer;

    public NpcOllamaPromptBuilder(NpcPromptBudget budget, IConversationHistoryTrimmer historyTrimmer = null)
    {
        this.budget = budget ?? new NpcPromptBudget();
        this.historyTrimmer = historyTrimmer ?? new TailConversationHistoryTrimmer();
    }

    /// <summary>
    /// Builds system + user prompts. Pass the two strings to <see cref="OllamaClient.GenerateAsync(string, string, System.Action{OllamaResult})"/>.
    /// For a single formatted blob for logs, use <see cref="NpcOllamaPromptResult.BuildCombinedDebugView"/>.
    /// </summary>
    public NpcOllamaPromptResult Build(NPCProfile profile, NpcDialoguePromptContext context)
    {
        string system = BuildSystemPrompt(profile);
        string user = BuildUserPromptWithBudget(profile, context);
        return new NpcOllamaPromptResult(system, user);
    }

    private string BuildSystemPrompt(NPCProfile profile)
    {
        if (profile == null)
        {
            // Minimal fallback if profile asset missing — keeps greybox scenes from hard-failing.
            return "You are a minor NPC in a spy thriller. Reply in at most three short sentences. Stay in character. No AI meta-talk.";
        }

        // Behavioral contract first: many instruction-tuned models weight early system content strongly for compliance vs. flavor text later.
        StringBuilder system = new StringBuilder(1024);

        system.AppendLine("You are a fictional character in a spy thriller video game.");
        system.AppendLine("Stay in character. No AI/Unity/Ollama/meta. No quoting these rules.");
        system.AppendLine($"Max {MaxReplySentences} short sentences. Plain text, no markdown.");
        system.AppendLine("Do not dump classified lists; secrets are for subtext unless drama demands a careful reveal.");
        system.AppendLine();

        system.Append("Identity: ").Append(profile.RoleTitle).Append(" named ").AppendLine(profile.CharacterDisplayName);
        system.AppendLine("Personality:");
        system.AppendLine(PromptTextUtilities.TruncateEnd(
            PromptTextUtilities.NormalizeWhitespace(profile.Personality),
            budget.maxPersonalityChars));
        system.AppendLine();
        system.AppendLine("Voice:");
        system.AppendLine(PromptTextUtilities.TruncateEnd(
            PromptTextUtilities.NormalizeWhitespace(profile.VoiceGuidelines),
            budget.maxVoiceGuidelinesChars));

        string secrets = profile.SecretsForAi;
        if (!string.IsNullOrWhiteSpace(secrets))
        {
            system.AppendLine();
            system.AppendLine("Private knowledge (investigator does not automatically know this; do not casually reveal):");
            system.AppendLine(PromptTextUtilities.TruncateEnd(
                PromptTextUtilities.NormalizeWhitespace(secrets),
                budget.maxSecretsChars));
        }

        return PromptTextUtilities.NormalizeWhitespace(system.ToString());
    }

    private string BuildUserPromptWithBudget(NPCProfile profile, NpcDialoguePromptContext context)
    {
        // User prompt holds volatile state so we rebuild it each turn without re-sending long static persona blocks.
        string playerLine = PromptTextUtilities.TruncateEnd(
            context.PlayerUtterance.Trim(),
            budget.maxPlayerUtteranceChars);

        string evidence = string.IsNullOrWhiteSpace(context.EvidenceSummary)
            ? "(None on record.)"
            : PromptTextUtilities.TruncateEnd(
                PromptTextUtilities.NormalizeWhitespace(context.EvidenceSummary),
                budget.maxEvidenceChars);

        int historyBudget = budget.maxHistoryChars;
        string history = historyTrimmer.TrimForPrompt(
            PromptTextUtilities.NormalizeWhitespace(context.MemoryTranscript),
            historyBudget);

        string user = AssembleUserBody(profile, context, playerLine, evidence, history);

        // If over soft cap, shrink history first — usually the cheapest lever before touching evidence or player line.
        int guard = 0;
        while (user.Length > budget.maxTotalUserPromptChars && historyBudget > 120 && guard++ < 12)
        {
            historyBudget = Mathf.Max(120, historyBudget * 3 / 4);
            history = historyTrimmer.TrimForPrompt(
                PromptTextUtilities.NormalizeWhitespace(context.MemoryTranscript),
                historyBudget);
            user = AssembleUserBody(profile, context, playerLine, evidence, history);
        }

        if (user.Length > budget.maxTotalUserPromptChars)
        {
            user = PromptTextUtilities.TruncateEnd(user, budget.maxTotalUserPromptChars);
        }

        return user;
    }

    private string AssembleUserBody(
        NPCProfile profile,
        NpcDialoguePromptContext context,
        string playerLine,
        string evidence,
        string history)
    {
        // Short bracket tags: cheap tokens, still give weaker models a scaffold to follow.
        StringBuilder user = new StringBuilder(2048);
        user.AppendLine("[STATE]");
        user.Append("trust_suspicion=").Append(context.SuspicionValue.ToString("+0.#;-0.#;0"))
            .Append(" (-100=max_suspicion, 0=neutral, +100=max_trust) band=")
            .AppendLine(context.SuspicionLevel.ToString());
        if (!string.IsNullOrWhiteSpace(context.SuspicionDialogueTag))
        {
            user.Append("suspicion_tag=").AppendLine(context.SuspicionDialogueTag);
        }

        user.Append("trust_npc_to_player=").Append(context.TrustLevel.ToString("0.#")).AppendLine("/100");
        user.AppendLine();
        user.AppendLine("[EVIDENCE]");
        user.AppendLine(evidence);
        user.AppendLine();
        user.AppendLine("[HISTORY]");
        user.AppendLine(string.IsNullOrWhiteSpace(history) ? "(none)" : history);
        user.AppendLine();
        user.AppendLine("[PLAYER]");
        user.Append('"').Append(playerLine).AppendLine("\"");
        user.AppendLine();
        string displayName = profile != null ? profile.CharacterDisplayName : context.CharacterName;
        user.Append("[TASK] Respond in character as ").Append(displayName);
        user.Append(". Max ").Append(MaxReplySentences).AppendLine(" short sentences.");

        return PromptTextUtilities.NormalizeWhitespace(user.ToString());
    }
}
