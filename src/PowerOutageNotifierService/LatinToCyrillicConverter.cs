namespace PowerOutageNotifier.PowerOutageNotifierService
{
    using System.Text;

    /// <summary>
    /// Class for converting Latin text to Cyrillic text.
    /// </summary>
    public static class LatinToCyrillicConverter
    {
        /// <summary>
        /// Converts Latin text to Cyrillic text.
        /// </summary>
        /// <param name="latinText">Text in latin.</param>
        /// <returns>Text in cyrillic.</returns>
        public static string? ConvertLatinToCyrillic(string? latinText)
        {
            if (latinText == null)
            {
                return null;
            }

            Dictionary<string, string> latinToCyrillicMap = new Dictionary<string, string>
            {
                {"a", "а"}, {"b", "б"}, {"c", "ц"}, {"č", "ч"}, {"ć", "ћ"},
                {"d", "д"}, {"đ", "ђ"}, {"dj", "ђ"}, {"e", "е"}, {"f", "ф"}, {"g", "г"},
                {"h", "х"}, {"i", "и"}, {"j", "ј"}, {"k", "к"}, {"l", "л"},
                {"lj", "љ"}, {"m", "м"}, {"n", "н"}, {"nj", "њ"}, {"o", "о"},
                {"p", "п"}, {"r", "р"}, {"s", "с"}, {"š", "ш"}, {"t", "т"},
                {"u", "у"}, {"v", "в"}, {"z", "з"}, {"ž", "ж"},
                {"A", "А"}, {"B", "Б"}, {"C", "Ц"}, {"Č", "Ч"}, {"Ć", "Ћ"},
                {"D", "Д"}, {"Đ", "Ђ"}, {"DJ", "Ђ"}, {"Dj", "Ђ"}, {"E", "Е"}, {"F", "Ф"}, {"G", "Г"},
                {"H", "Х"}, {"I", "И"}, {"J", "Ј"}, {"K", "К"}, {"L", "Л"},
                {"Lj", "Љ"}, {"LJ", "Љ"}, {"M", "М"}, {"N", "Н"}, {"Nj", "Њ"}, {"NJ", "Њ"}, {"O", "О"},
                {"P", "П"}, {"R", "Р"}, {"S", "С"}, {"Š", "Ш"}, {"T", "Т"},
                {"U", "У"}, {"V", "В"}, {"Z", "З"}, {"Ž", "Ж"}
            };

            StringBuilder cyrillicText = new StringBuilder();

            for (int i = 0; i < latinText.Length; i++)
            {
                string currentChar = latinText[i].ToString();

                if (i < latinText.Length - 1 && latinToCyrillicMap.ContainsKey(currentChar + latinText[i + 1]))
                {
                    // Handle two-character combinations like 'lj' or 'nj'
                    string twoCharCombination = currentChar + latinText[i + 1];
                    _ = cyrillicText.Append(latinToCyrillicMap[twoCharCombination]);
                    i++; // Skip the next character since it's part of the two-character combination
                }
                else if (latinToCyrillicMap.ContainsKey(currentChar))
                {
                    _ = cyrillicText.Append(latinToCyrillicMap[currentChar]);
                }
                else
                {
                    _ = cyrillicText.Append(currentChar); // Keep unchanged if not in the mapping
                }
            }

            return cyrillicText.ToString();
        }
    }
}
