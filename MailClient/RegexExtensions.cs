using System;
using System.Text.RegularExpressions;

namespace MailClient
{
    public static class RegexExtensions
    {
        public static string Parse(this Regex regex, string str)
        {
            if (!regex.IsMatch(str))
                throw new ArgumentException($"Incorrect line: {str}");
            return regex.Match(str).Groups[1].Value;
        }
    }
}
