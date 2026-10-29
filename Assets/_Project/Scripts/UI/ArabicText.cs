using System.Collections.Generic;
using System.Text;

namespace RoadSafety.UI
{
    /// <summary>
    /// Unity's text components draw Arabic letters one by one, unconnected and left to right.
    /// ArabicText.Fix() returns the same text with the joined letter shapes (Unicode
    /// presentation forms) in display order, so it reads correctly from right to left:
    /// <code>
    ///     label.text = ArabicText.Fix("ضع حزام الأمان!");
    ///     label.text = ArabicText.Fix(longText, 24);   // wraps lines at ~24 characters
    /// </code>
    /// Use a font that has the Arabic presentation forms (Baloo Bhaijaan RS, in Resources/Fonts) and keep
    /// the Text component's horizontal overflow on "Overflow": let Fix() do the line breaks,
    /// otherwise Unity wraps the reversed line in the wrong place.
    /// </summary>
    public static class ArabicText
    {
        private struct Forms
        {
            public char Isolated, Final, Initial, Medial;
        }

        private enum Direction { RightToLeft, LeftToRight, Neutral }

        private static readonly Dictionary<char, Forms> Table = BuildTable();

        private static Dictionary<char, Forms> BuildTable()
        {
            // Letters U+0621..U+064A in the order of the Presentation Forms-B block (U+FE80...),
            // with how many shapes each has: 1 = never joins (hamza), 2 = joins only to the
            // letter before it (alef, dal, reh, waw...), 4 = joins on both sides.
            var letters = new (char letter, int shapes)[]
            {
                ('ء', 1), ('آ', 2), ('أ', 2), ('ؤ', 2), ('إ', 2), ('ئ', 4),
                ('ا', 2), ('ب', 4), ('ة', 2), ('ت', 4), ('ث', 4), ('ج', 4),
                ('ح', 4), ('خ', 4), ('د', 2), ('ذ', 2), ('ر', 2), ('ز', 2),
                ('س', 4), ('ش', 4), ('ص', 4), ('ض', 4), ('ط', 4), ('ظ', 4),
                ('ع', 4), ('غ', 4), ('ف', 4), ('ق', 4), ('ك', 4), ('ل', 4),
                ('م', 4), ('ن', 4), ('ه', 4), ('و', 2), ('ى', 2), ('ي', 4),
            };

            var table = new Dictionary<char, Forms>();
            int code = 0xFE80;
            foreach (var (letter, shapes) in letters)
            {
                // The plain letter already has the isolated shape, and every Arabic font has it.
                var forms = new Forms { Isolated = letter };
                if (shapes >= 2)
                    forms.Final = (char)(code + 1);
                if (shapes == 4)
                {
                    forms.Initial = (char)(code + 2);
                    forms.Medial = (char)(code + 3);
                }
                table[letter] = forms;
                code += shapes;
            }
            return table;
        }

        /// <summary>Shapes and reorders Arabic text for display. maxCharsPerLine = 0 means no wrapping.</summary>
        public static string Fix(string text, int maxCharsPerLine = 0)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            var lines = new List<string>();
            foreach (string paragraph in text.Replace("\r", "").Split('\n'))
                foreach (string line in Wrap(paragraph, maxCharsPerLine))
                    lines.Add(Reorder(Shape(line)));
            return string.Join("\n", lines);
        }

        // ---------- 1. joining (logical order) ----------

        private static string Shape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (!Table.TryGetValue(c, out Forms forms))
                {
                    sb.Append(c);
                    continue;
                }

                char previous = PreviousLetter(s, i);
                int nextIndex = NextLetterIndex(s, i);
                char next = nextIndex >= 0 ? s[nextIndex] : '\0';
                bool joinsPrevious = forms.Final != '\0' && JoinsToNext(previous);

                // lam + alef -> one ligature (لا)
                if (c == 'ل')
                {
                    char ligature = LamAlef(next, joinsPrevious);
                    if (ligature != '\0')
                    {
                        sb.Append(ligature);
                        for (int k = i + 1; k < nextIndex; k++)
                            sb.Append(s[k]); // marks between lam and alef
                        i = nextIndex;
                        continue;
                    }
                }

                bool joinsNext = forms.Initial != '\0' && JoinsToPrevious(next);
                if (joinsPrevious && joinsNext) sb.Append(forms.Medial);
                else if (joinsPrevious) sb.Append(forms.Final);
                else if (joinsNext) sb.Append(forms.Initial);
                else sb.Append(forms.Isolated);
            }
            return sb.ToString();
        }

        private static bool IsMark(char c) =>
            (c >= 'ً' && c <= 'ٟ') || c == 'ٰ' || (c >= 'ۖ' && c <= 'ۭ');

        private static bool JoinsToNext(char c) =>
            c == 'ـ' || (Table.TryGetValue(c, out Forms f) && f.Initial != '\0');

        private static bool JoinsToPrevious(char c) =>
            c == 'ـ' || (Table.TryGetValue(c, out Forms f) && f.Final != '\0');

        private static char PreviousLetter(string s, int i)
        {
            for (int k = i - 1; k >= 0; k--)
                if (!IsMark(s[k]))
                    return s[k];
            return '\0';
        }

        private static int NextLetterIndex(string s, int i)
        {
            for (int k = i + 1; k < s.Length; k++)
                if (!IsMark(s[k]))
                    return k;
            return -1;
        }

        private static char LamAlef(char alef, bool final)
        {
            switch (alef)
            {
                case 'آ': return final ? 'ﻶ' : 'ﻵ'; // لآ
                case 'أ': return final ? 'ﻸ' : 'ﻷ'; // لأ
                case 'إ': return final ? 'ﻺ' : 'ﻹ'; // لإ
                case 'ا': return final ? 'ﻼ' : 'ﻻ'; // لا
                default: return '\0';
            }
        }

        // ---------- 2. right-to-left display order ----------

        private static Direction DirectionOf(char c)
        {
            if ((c >= '٠' && c <= '٩') || (c >= '۰' && c <= '۹'))
                return Direction.LeftToRight; // Arabic-Indic digits are written left to right
            if ((c >= '֐' && c <= 'ࣿ') || (c >= 'יִ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿'))
                return Direction.RightToLeft;
            if (char.IsLetterOrDigit(c))
                return Direction.LeftToRight;
            return Direction.Neutral;
        }

        /// <summary>Simple bidi for one line whose base direction is right-to-left.</summary>
        private static string Reorder(string line)
        {
            int n = line.Length;
            var dirs = new Direction[n];
            for (int i = 0; i < n; i++)
                dirs[i] = DirectionOf(line[i]);

            // Spaces and punctuation take the direction around them, otherwise right-to-left.
            for (int i = 0; i < n; i++)
            {
                if (dirs[i] != Direction.Neutral)
                    continue;
                int j = i;
                while (j < n && dirs[j] == Direction.Neutral)
                    j++;
                Direction before = i > 0 ? dirs[i - 1] : Direction.RightToLeft;
                Direction after = j < n ? dirs[j] : Direction.RightToLeft;
                Direction resolved = before == after ? before : Direction.RightToLeft;
                for (int k = i; k < j; k++)
                    dirs[k] = resolved;
                i = j - 1;
            }

            // Runs are written from the last to the first; right-to-left runs are reversed.
            var sb = new StringBuilder(n);
            int end = n;
            while (end > 0)
            {
                int start = end - 1;
                Direction d = dirs[start];
                while (start > 0 && dirs[start - 1] == d)
                    start--;

                if (d == Direction.RightToLeft)
                {
                    int k = end - 1;
                    while (k >= start)
                    {
                        int clusterEnd = k;
                        while (k > start && IsMark(line[k]))
                            k--;
                        sb.Append(Mirror(line[k]));
                        for (int m = k + 1; m <= clusterEnd; m++)
                            sb.Append(line[m]); // keep a mark after its letter
                        k--;
                    }
                }
                else
                {
                    sb.Append(line, start, end - start);
                }
                end = start;
            }
            return sb.ToString();
        }

        private static char Mirror(char c)
        {
            switch (c)
            {
                case '(': return ')';
                case ')': return '(';
                case '[': return ']';
                case ']': return '[';
                case '{': return '}';
                case '}': return '{';
                case '<': return '>';
                case '>': return '<';
                case '«': return '»';
                case '»': return '«';
                default: return c;
            }
        }

        // ---------- 3. line wrapping (before reordering) ----------

        private static IEnumerable<string> Wrap(string paragraph, int maxChars)
        {
            if (maxChars <= 0 || paragraph.Length <= maxChars)
            {
                yield return paragraph;
                yield break;
            }

            var sb = new StringBuilder();
            foreach (string word in paragraph.Split(' '))
            {
                if (sb.Length > 0 && sb.Length + 1 + word.Length > maxChars)
                {
                    yield return sb.ToString();
                    sb.Clear();
                }
                if (sb.Length > 0)
                    sb.Append(' ');
                sb.Append(word);
            }
            if (sb.Length > 0)
                yield return sb.ToString();
        }
    }
}
