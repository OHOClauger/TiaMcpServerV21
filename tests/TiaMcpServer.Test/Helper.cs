using System.Text.RegularExpressions;

namespace TiaMcpServer.Test
{
    internal class Helper
    {
        private static string DecodeUnicodeEscapes(string text)
        {
            // response = response.Replace("\\u002B", "+"); 
            // response = response.Replace("\\u002F", "/"); 
            // response = response.Replace("\\u003A", ":"); 
            // response = response.Replace("\\u003C", "<"); 
            // response = response.Replace("\\u003E", ">"); 
            // response = response.Replace("\\u005C", "\\");
            // response = response.Replace("\\u0022", "\"");
            // response = response.Replace("\\u0027", "'"); 
            // response = response.Replace("\\u0026", "&"); 
            // response = response.Replace("\\u0009", "\t");
            // response = response.Replace("\\u000A", "\n");
            // response = response.Replace("\\u000D", "\r");
            // response = response.Replace("\\u0020", " "); 
            // response = response.Replace("\\u00A0", " ");
            // response = response.Replace("\\u00E4", "ä"); 
            // response = response.Replace("\\u00F6", "ö"); 
            // response = response.Replace("\\u00FC", "ü"); 
            // response = response.Replace("\\u00DF", "ß"); 

            // general replace for 4-digit unicode escapes
            return Regex.Replace(text, @"\\u([0-9a-fA-F]{4})", match =>
            {
                // Konvertiert den 4-stelligen Hex-Code in ein Unicode-Zeichen
                return ((char)int.Parse(match.Groups[1].Value, System.Globalization.NumberStyles.HexNumber)).ToString();
            });
        }
        
    }
}
