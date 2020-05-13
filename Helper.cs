using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace DFS_Basketball
{
    class Helper
    {
        public static long ConvertToTimestamp(DateTime dt)
        {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan elapsed_time = dt - epoch;
            return (long)elapsed_time.TotalSeconds;
        }
        public static string StripPunctuation(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (!char.IsPunctuation(c))
                    sb.Append(c);
            }
            return sb.ToString();
        }
        public static string RemoveDiacritics(string text)
        {
            string formD = text.Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder();

            foreach (char ch in formD)
            {
                UnicodeCategory uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                {
                    sb.Append(ch);
                }
            }

            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
        public static IEnumerable<int> GetAllIndexes(string source, string matchString)
        {
            matchString = Regex.Escape(matchString);
            foreach (Match match in Regex.Matches(source, matchString))
            {
                yield return match.Index;
            }
        }
        public static string FindBetween(string source, string start, string end, int after_index = 0)
        {
            int s = source.IndexOf(start, after_index + 1) + start.Length;
            int e = source.IndexOf(end, s + 1);
            string substring = source.Substring(s, e - s);
            return substring;
        }
        public static string GetSourceCode(string url)
        {
                    HttpClient client = new HttpClient();
            try
            {
                return client.GetStringAsync(url).Result;
            }
            catch (Exception e)
            {
                return "";
            }
        }
    }
}