using System.Text.RegularExpressions;

namespace VoicevoxPlugin
{
    /// <summary>
    /// 名前とコメントから読み上げ用の文字列を組み立てる
    /// </summary>
    static class SpeechTextBuilder
    {
        private static readonly Regex UrlRegex = new Regex(@"https?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SpacesRegex = new Regex(@"\s+", RegexOptions.Compiled);
        public const string UrlReplacement = "URL省略";
        public const string OmittedSuffix = "以下略";

        public static string Build(string name, string comment, Options options)
        {
            var body = Clean(comment, options);
            var text = "";
            if (options.IsReadHandleName && !string.IsNullOrWhiteSpace(name))
            {
                text = name.Trim();
                if (options.IsAppendNickTitle)
                {
                    text += options.NickTitle;
                }
            }
            if (!string.IsNullOrEmpty(body))
            {
                //名前とコメントの間に読点を入れると一呼吸おいて読んでくれる
                text = string.IsNullOrEmpty(text) ? body : text + "、" + body;
            }
            return text;
        }

        private static string Clean(string comment, Options options)
        {
            if (string.IsNullOrWhiteSpace(comment))
                return "";
            var s = comment;
            if (options.IsReplaceUrl)
            {
                s = UrlRegex.Replace(s, UrlReplacement);
            }
            s = SpacesRegex.Replace(s, " ").Trim();
            var max = options.MaxTextLength;
            if (s.Length > max)
            {
                //絵文字などのサロゲートペアの途中で切らないようにする
                if (char.IsHighSurrogate(s[max - 1])) max--;
                s = s.Substring(0, max) + " " + OmittedSuffix;
            }
            return s;
        }
    }
}
