using System.Collections.Generic;
using System.Text;

namespace LostAndFound
{
    /// <summary>
    /// Everything said in the current claim, in order: the claimants' lines, your questions and their answers. Claims
    /// go on the slip; this keeps the rest (above all the answers) where you can read it again beside the slip.
    /// </summary>
    public class Transcript
    {
        public struct Line
        {
            public string who;    // short name, or "You"
            public string text;   // rich text, claims highlighted as in the speech bubble
            public bool you;
        }

        readonly List<Line> lines = new();
        public IReadOnlyList<Line> Lines => lines;
        public int Count => lines.Count;
        /// <summary>Bumped on every change, so the card only re-lays itself out when it has to.</summary>
        public int Version { get; private set; }

        public void Clear()
        {
            lines.Clear();
            Version++;
        }

        public void Add(string who, string text, bool you = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            lines.Add(new Line { who = string.IsNullOrEmpty(who) ? "?" : who, text = text, you = you });
            Version++;
        }

        /// <summary>
        /// The card's text. <paramref name="skip"/> drops that many of the oldest lines after the first, for a claim too
        /// long to fit: the opening (what they've lost) always stays, then "…", then the newest lines.
        /// </summary>
        public string Format(int skip = 0)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i >= 1 && i <= skip)
                {
                    if (i == 1) sb.Append("<color=#00000060>…</color>\n");
                    continue;
                }
                var l = lines[i];
                if (l.you) sb.Append("<color=#5a4e48><i>You: ").Append(l.text).Append("</i></color>");
                else sb.Append("<b>").Append(l.who).Append(":</b> ").Append(l.text);
                sb.Append('\n');
            }
            return sb.ToString().TrimEnd('\n');
        }
    }
}
