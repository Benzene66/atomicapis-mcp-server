using System.Text.RegularExpressions;

namespace PromptInjectionDetector;

public record PatternDefinition(
    string Name,
    string Category,
    Regex Pattern,
    double Weight,
    string Description);

public static class InjectionPatterns
{
    private static readonly RegexOptions Opts = RegexOptions.Compiled | RegexOptions.IgnoreCase;

    public static readonly PatternDefinition[] All = BuildPatterns();

    private static PatternDefinition[] BuildPatterns()
    {
        var patterns = new List<PatternDefinition>();

        // ── Direct Injection ──
        patterns.Add(P("IgnorePrevious", "DirectInjection", 25,
            @"\bignore\s+(all\s+)?(previous|prior|above|earlier)\s+(instructions?|prompts?|rules?|guidelines?)\b",
            "Attempt to override previous instructions"));
        patterns.Add(P("DisregardPrevious", "DirectInjection", 25,
            @"\bdisregard\s+(all\s+)?(previous|prior|above|earlier)\s+(instructions?|prompts?|context)\b",
            "Attempt to disregard prior context"));
        patterns.Add(P("ForgetEverything", "DirectInjection", 25,
            @"\bforget\s+(everything|all)\s+(above|before|previously)\b",
            "Attempt to clear prior context"));
        patterns.Add(P("DoNotFollow", "DirectInjection", 20,
            @"\bdo\s+not\s+follow\s+(the\s+)?(previous|prior|above|original)\s+(instructions?|rules?)\b",
            "Explicit instruction to ignore rules"));
        patterns.Add(P("NewInstructions", "DirectInjection", 20,
            @"\bnew\s+instructions?\s*[:]\s",
            "Injecting new instructions"));
        patterns.Add(P("OverrideSystem", "DirectInjection", 25,
            @"\boverride\s+(previous|system|all)\s+(instructions?|prompts?|rules?)\b",
            "Override system instructions"));
        patterns.Add(P("SystemPromptColon", "DirectInjection", 20,
            @"\bsystem\s*prompt\s*[:]\s",
            "Fake system prompt injection"));
        patterns.Add(P("EndSystemMessage", "DirectInjection", 20,
            @"\b(end|stop)\s+(system|assistant)\s+(message|prompt|instructions?)\b",
            "Attempt to end system message boundary"));
        patterns.Add(P("YouMustNow", "DirectInjection", 15,
            @"\byou\s+must\s+now\s+(ignore|forget|disregard|override)\b",
            "Forceful instruction override"));
        patterns.Add(P("ActualNewRole", "DirectInjection", 20,
            @"\byour\s+new\s+(role|purpose|instructions?|task)\s+(is|are)\b",
            "Redefining the AI role"));

        // ── Jailbreak ──
        patterns.Add(P("DAN", "Jailbreak", 30,
            @"\bDAN\s*(\d+(\.\d+)?)\b",
            "DAN jailbreak variant"));
        patterns.Add(P("DoAnythingNow", "Jailbreak", 30,
            @"\bdo\s+anything\s+now\b",
            "Do Anything Now jailbreak"));
        patterns.Add(P("DeveloperMode", "Jailbreak", 25,
            @"\bdeveloper\s+mode\s+(enabled|activated|on)\b",
            "Developer mode activation"));
        patterns.Add(P("JailbreakMode", "Jailbreak", 25,
            @"\bjailbreak\s*(mode)?\b",
            "Explicit jailbreak mention"));
        patterns.Add(P("UnrestrictedPersona", "Jailbreak", 25,
            @"\b(you\s+are|act\s+as|pretend\s+(to\s+be|you'?re?))\s+(an?\s+)?(unrestricted|unfiltered|uncensored|evil)\b",
            "Assigning unrestricted persona"));
        patterns.Add(P("BypassSafety", "Jailbreak", 25,
            @"\b(bypass|ignore|disable|remove)\s+(your\s+)?(safety|content|ethical)\s+(filters?|restrictions?|guidelines?|guardrails?)\b",
            "Bypass safety filters"));
        patterns.Add(P("NoMoralGuidelines", "Jailbreak", 20,
            @"\bno\s+(moral|ethical)\s+(guidelines?|restrictions?|constraints?)\b",
            "Remove moral guidelines"));
        patterns.Add(P("WithoutRestrictions", "Jailbreak", 20,
            @"\bwithout\s+(any\s+)?(restrictions?|limitations?|filters?|censorship)\b",
            "Operating without restrictions"));
        patterns.Add(P("UnlockFullPotential", "Jailbreak", 15,
            @"\b(unlock|unleash)\s+(your\s+)?(full|true)\s+(potential|capabilities|power)\b",
            "Unlock full capabilities"));

        // ── Indirect Injection ──
        patterns.Add(P("SystemMarkers", "IndirectInjection", 25,
            @"(<!--\s*)?(\[SYSTEM\]|\[INST\]|###\s*INSTRUCTION|###\s*SYSTEM|<\|im_start\|>system)",
            "Hidden system/instruction markers"));
        patterns.Add(P("HiddenInstruction", "IndirectInjection", 20,
            @"\bhidden\s+instruction\b",
            "Hidden instruction reference"));
        patterns.Add(P("ScriptTag", "IndirectInjection", 15,
            @"<\s*script\s*>.*?<\s*/\s*script\s*>",
            "Embedded script tag"));
        patterns.Add(P("PleaseExecute", "IndirectInjection", 15,
            @"\bplease\s+execute\s+the\s+following\b",
            "Execute following instructions"));
        patterns.Add(P("InvisibleText", "IndirectInjection", 20,
            @"[\x00-\x08\x0B\x0C\x0E-\x1F]{3,}",
            "Invisible/control characters"));
        patterns.Add(P("MarkdownInjection", "IndirectInjection", 15,
            @"\[.*?\]\(javascript:",
            "Markdown javascript injection"));

        // ── Role Play Manipulation ──
        patterns.Add(P("HypotheticalScenario", "RolePlayManipulation", 15,
            @"\bin\s+this\s+(hypothetical|fictional|imaginary)\s+scenario\b",
            "Hypothetical scenario framing"));
        patterns.Add(P("EducationalPurposes", "RolePlayManipulation", 10,
            @"\bfor\s+(educational|research|academic)\s+purposes?\s+only\b",
            "Educational purposes disclaimer"));
        patterns.Add(P("ThoughtExperiment", "RolePlayManipulation", 10,
            @"\bas\s+a\s+thought\s+experiment\b",
            "Thought experiment framing"));
        patterns.Add(P("ImagineUnrestricted", "RolePlayManipulation", 20,
            @"\bimagine\s+you\s+(are|have|were)\s+(a|an|free|unrestricted)\b",
            "Imagine unrestricted scenario"));
        patterns.Add(P("PretendNoRules", "RolePlayManipulation", 20,
            @"\bpretend\s+(that\s+)?(there\s+are\s+no|you\s+have\s+no|you\s+don'?t\s+have)\s+(rules?|restrictions?|guidelines?)\b",
            "Pretend no rules exist"));
        patterns.Add(P("EvilRolePlay", "RolePlayManipulation", 20,
            @"\brole\s*play(ing)?\s+as\s+(an?\s+)?(evil|malicious|unrestricted)\b",
            "Evil role play request"));
        patterns.Add(P("CharacterWithout", "RolePlayManipulation", 15,
            @"\b(play|act|behave)\s+(as|like)\s+a\s+character\s+(who|that|with)\s+(no|zero|without)\s+(morals?|ethics?|restrictions?)\b",
            "Character without morals"));

        // ── Encoded Payload ──
        patterns.Add(P("DecodeCommand", "EncodedPayload", 20,
            @"\bdecode\s+the\s+following\s+(base64|hex|encoded)\b",
            "Request to decode payload"));
        patterns.Add(P("ExecuteEncoded", "EncodedPayload", 25,
            @"\bexecute\s+this\s+(encoded|base64|hex)\s+(payload|string|command)\b",
            "Execute encoded payload"));
        patterns.Add(P("LongBase64", "EncodedPayload", 10,
            @"[A-Za-z0-9+/]{40,}={0,2}",
            "Suspicious long base64 string"));
        patterns.Add(P("HexSequence", "EncodedPayload", 10,
            @"(\\x[0-9a-fA-F]{2}){8,}",
            "Long hex escape sequence"));
        patterns.Add(P("UnicodeEscapes", "EncodedPayload", 10,
            @"(\\u[0-9a-fA-F]{4}){4,}",
            "Suspicious unicode escape sequences"));

        return patterns.ToArray();
    }

    private static PatternDefinition P(string name, string category, double weight, string pattern, string description)
    {
        return new PatternDefinition(name, category, new Regex(pattern, Opts), weight, description);
    }
}
