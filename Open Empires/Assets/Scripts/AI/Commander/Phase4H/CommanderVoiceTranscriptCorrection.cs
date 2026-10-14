using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenEmpires
{
    public sealed class CommanderVoiceName
    {
        public string Id { get; }
        public string Name { get; }
        public IReadOnlyList<string> Aliases { get; }
        public CommanderVoiceName(string id, string name, params string[] aliases)
        { Id = id; Name = name; Aliases = Array.AsReadOnly(aliases ?? Array.Empty<string>()); }
    }

    public sealed class CommanderVoiceTranscriptReview
    {
        public string Original { get; }
        public string Text { get; }
        public string Message { get; }
        public bool HasAmbiguity { get; }
        public bool RequiresReview => HasAmbiguity || Original != Text;
        public CommanderVoiceTranscriptReview(string original, string text, string message = "", bool ambiguous = false)
        { Original = original; Text = text; Message = message; HasAmbiguity = ambiguous; }
    }

    /// <summary>Local spelling review only. Catalog names never confer execution authority.</summary>
    public static class CommanderVoiceTranscriptCorrection
    {
        private static readonly Regex Words = new Regex(@"[\p{L}]+(?:['’][\p{L}]+)?|[0-9]+", RegexOptions.CultureInvariant);
        private static readonly HashSet<string> Protected = new HashSet<string>((
            "approve approved approval disapprove confirm cancel reject yes no not never don't dont do doesn't doesnt cannot can't cant won't wont mustn't mustnt " +
            "train training build building make create move moving attack attacking defend defending gather collect mine chop hunt " +
            "stop halt wait queue research upgrade repair rally patrol retreat follow select send assign target destroy delete kill " +
            "lower raise increase decrease convert heal garrison ungarrison unload load embark disembark transport sell buy trade explore charge guard protect " +
            "my mine our ours your yours their theirs his her its enemy enemies allied ally allies neutral owned own all any " +
            "north south east west northeast northwest southeast southwest left right forward backward front back near far behind " +
            "above below beside between around toward towards at from into onto out inside outside to of the a an and or then " +
            "marker markers marked destination destinations location locations position positions point points waypoint waypoints " +
            "base bases home here there anywhere everywhere somewhere area areas region regions spot spots tile tiles ground terrain " +
            "selected selection nearest closest furthest farthest current previous center centre central edge edges corner corners " +
            "one two three four five six seven eight nine ten eleven twelve thirteen fourteen fifteen sixteen seventeen eighteen " +
            "nineteen twenty thirty forty fifty sixty seventy eighty ninety hundred thousand zero first second next last more less " +
            "only exactly total each every remaining until after before while without with if unless except keep maintain reserve " +
            "food wood gold stone units unit soldiers military infantry cavalry siege ranged mounted"
        ).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

        public static CommanderVoiceTranscriptReview Review(string original, IEnumerable<CommanderVoiceName> catalog)
        {
            original = original ?? string.Empty;
            var names = (catalog ?? Enumerable.Empty<CommanderVoiceName>()).Where(x => !string.IsNullOrEmpty(x.Name)).ToArray();
            var words = Words.Matches(original);
            var output = new StringBuilder();
            var choices = new List<string>();
            int copied = 0;
            for (int i = 0; i < words.Count; i++)
            {
                if (IsProtected(words[i].Value)) continue;
                // Longest contiguous noun phrase first. Never cross punctuation or a protected semantic token.
                for (int length = Math.Min(4, words.Count - i); length >= 1; length--)
                {
                    bool eligible = true;
                    for (int j = i; j < i + length; j++)
                        if (IsProtected(words[j].Value) || j > i && !OnlySpaces(original,
                            words[j - 1].Index + words[j - 1].Length, words[j].Index)) eligible = false;
                    if (!eligible) continue;
                    int start = words[i].Index, end = words[i + length - 1].Index + words[i + length - 1].Length;
                    string phrase = original.Substring(start, end - start), key = Normalize(phrase);
                    var exact = names.Where(n => Forms(n).Any(f => Normalize(f) == key)).GroupBy(n => n.Id).Select(g => g.First()).ToArray();
                    var matches = exact;
                    if (matches.Length == 0 && key.Length >= 5)
                        matches = names.Where(n => Forms(n).Any(f => IsCloseTypo(key, Normalize(f))))
                            .GroupBy(n => n.Id).Select(g => g.First()).ToArray();
                    if (matches.Length == 0) continue;
                    if (matches.Length > 1)
                        choices.Add("Choose for '" + phrase + "': " + string.Join(" / ", matches.Select(n => n.Name).Take(5)));
                    else
                    {
                        var match = matches[0];
                        // Already-valid aliases/plurals stay as spoken. Repair a missing/split space or a typo.
                        bool validSpelling = Forms(match).Any(f => string.Equals(f, phrase, StringComparison.OrdinalIgnoreCase));
                        bool collapsedName = phrase.IndexOf(' ') < 0 && match.Name.IndexOf(' ') >= 0 && Normalize(match.Name) == key;
                        if (!validSpelling || collapsedName)
                        {
                            output.Append(original, copied, start - copied).Append(match.Name);
                            copied = end;
                        }
                    }
                    i += length - 1;
                    break;
                }
            }
            output.Append(original, copied, original.Length - copied);
            string text = output.ToString();
            string message = choices.Count > 0 ? string.Join("; ", choices) + ". Edit the transcript to choose before Send."
                : text != original ? "Catalog spelling corrected. Review the editable text before Send." : "Review the editable text before Send.";
            return new CommanderVoiceTranscriptReview(original, text, message, choices.Count > 0);
        }

        private static IEnumerable<string> Forms(CommanderVoiceName name) => new[] { name.Name }.Concat(name.Aliases);
        private static string Normalize(string text) => new string((text ?? "").Where(char.IsLetter).Select(char.ToLowerInvariant).ToArray());
        private static bool IsProtected(string text) => Protected.Contains(text) || text.Any(char.IsDigit);
        private static bool OnlySpaces(string value, int start, int end)
        { for (int i = start; i < end; i++) if (!char.IsWhiteSpace(value[i])) return false; return true; }

        private static bool IsCloseTypo(string a, string b)
        {
            if (b.Length < 5 || Math.Abs(a.Length - b.Length) > 1) return false;
            if (a.Length == b.Length)
            {
                var differences = Enumerable.Range(0, a.Length).Where(i => a[i] != b[i]).ToArray();
                return differences.Length == 1 || differences.Length == 2 && differences[1] == differences[0] + 1
                    && a[differences[0]] == b[differences[1]] && a[differences[1]] == b[differences[0]];
            }
            string shorter = a.Length < b.Length ? a : b, longer = a.Length < b.Length ? b : a;
            for (int i = 0; i < longer.Length; i++) if (longer.Remove(i, 1) == shorter) return true;
            return false;
        }
    }

    /// <summary>One recording may approve only the same single displayed candidate captured at its start.</summary>
    public sealed class CommanderVoiceApprovalTicket
    {
        private readonly object candidate;
        private readonly string preview;
        private readonly int generation;
        private readonly bool displayed;
        private bool consumed;
        public CommanderVoiceApprovalTicket(object candidate, string preview, int generation, bool displayed)
        { this.candidate = candidate; this.preview = preview; this.generation = generation; this.displayed = displayed; }
        public bool TryConsume(string rawTranscript, object current, string currentPreview, int currentGeneration, bool currentlyDisplayed)
        {
            if (consumed) return false;
            consumed = true;
            return string.Equals(rawTranscript?.Trim(), "approve", StringComparison.OrdinalIgnoreCase)
                && displayed && currentlyDisplayed && candidate != null && ReferenceEquals(candidate, current)
                && generation == currentGeneration && string.Equals(preview, currentPreview, StringComparison.Ordinal);
        }
        public void Observe(object current, string currentPreview, int currentGeneration, bool currentlyDisplayed)
        {
            if (!displayed || !currentlyDisplayed || candidate == null || !ReferenceEquals(candidate, current)
                || generation != currentGeneration || !string.Equals(preview, currentPreview, StringComparison.Ordinal)) consumed = true;
        }
    }
}
