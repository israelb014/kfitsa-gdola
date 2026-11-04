using System.Text;

namespace Kfitsa
{
    /// uGUI Text has no bidi: turns logical Hebrew into visual order, keeping digit and Latin runs readable.
    public static class Rtl
    {
        static bool Ltr(char c) => c < 128 && (char.IsLetterOrDigit(c) || c == '.');

        static char Mirror(char c) => c == '(' ? ')' : c == ')' ? '(' : c;

        public static string V(string s)
        {
            var lines = s.Split('\n');
            for (int n = 0; n < lines.Length; n++) lines[n] = Line(lines[n]);
            return string.Join("\n", lines);
        }

        static string Line(string s)
        {
            var r = new StringBuilder(s.Length);
            int i = s.Length - 1;
            while (i >= 0)
            {
                if (Ltr(s[i]))
                {
                    int j = i;
                    while (j >= 0 && Ltr(s[j])) j--;
                    r.Append(s, j + 1, i - j);
                    i = j;
                }
                else r.Append(Mirror(s[i--]));
            }
            return r.ToString();
        }
    }
}
