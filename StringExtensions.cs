using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;

namespace MyExtensions
{
	public static class StringExtensions
	{
      public const int SafeStringSize = 300;
      public const double DefaultSubstringWeight = 0.8;

		// Taken from: http://stackoverflow.com/questions/13793560/find-closest-match-to-input-string-in-a-list-of-strings
		// This code is an implementation of the pseudocode from the Wikipedia,
		// showing a naive implementation.
		// You should research an algorithm with better space complexity.
		public static int LevenshteinDistance(string s, string t)
		{
			int n = s.Length;
			int m = t.Length;

			if (n == 0)
			{
				return m;
			}
			if (m == 0)
			{
				return n;
			}
			if (s == t)
			{
				return 0;
			}

			int[,] d = new int[n + 1, m + 1];

			for (int i = 0; i <= n; d[i, 0] = i++)
				;
			for (int j = 0; j <= m; d[0, j] = j++)
				;
			for (int i = 1; i <= n; i++)
			{
				for (int j = 1; j <= m; j++)
				{
					int cost = (t[j - 1] == s[i - 1]) ? 0 : 1;
					d[i, j] = Math.Min(
						Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
						d[i - 1, j - 1] + cost);
				}
			}
			return d[n, m];
		}

      //Returns a normalized number between 0 and 1 representing the difference. 0 means strings are the same
      public static double StringDifference(string s1, string s2, double substringWeight = DefaultSubstringWeight)
      {
         return StringDifference_Unsafe(s1.Truncate(SafeStringSize), s2.Truncate(SafeStringSize), substringWeight);
      }

      //Returns a normalized number between 0 and 1 representing the difference. 0 means strings are the same.
      //WARNING: DO NOT CALL WITH OVERLY LARGE STRINGS!
      public static double StringDifference_Unsafe(string s1, string s2, double substringWeight = DefaultSubstringWeight)
      {
         if (substringWeight > 1)
            substringWeight = 1;
         else if (substringWeight < 0)
            substringWeight = 0;
         
         //Go through all the subsets matching the shorter string to the longer string and get the min distance.
         string longer = s1;
         string shorter = s2;

         if (s1.Length < s2.Length)
         {
            longer = s2;
            shorter = s1;
         }

         int minLength = shorter.Length;

         for (int i = 0; i <= longer.Length - shorter.Length; i++)
         {
            int subLength = LevenshteinDistance(shorter, longer.Substring(i, shorter.Length));

            if (subLength < minLength)
               minLength = subLength;
         }

         return substringWeight * ((double)minLength / (shorter.Length < 1 ? 1 : shorter.Length)) + (1 - substringWeight) * ((double)LevenshteinDistance(s1, s2) / (longer.Length < 1 ? 1 : longer.Length));

         //return (double)LevenshteinDistance(s1, s2) / Math.Max(s1.Length, s2.Length);
      }

		private static int GetStupid(int x, int y, int[,] silly)
		{
			return silly[x + 1, y + 1];
		}
		private static void SetStupid(int x, int y, ref int[,] silly, int value)
		{
			silly[x + 1, y + 1] = value;
		}

		//Taken from: http://en.wikipedia.org/wiki/Damerau%E2%80%93Levenshtein_distance
		/// <summary>
		/// This function is a piece of crap. Don't rely on it; use LevenshteinDistance instead!
		/// </summary>
		/// <param name="one"></param>
		/// <param name="two"></param>
		/// <returns></returns>
		public static int DamerauLevenshteinDistance(string one, string two)
		{
			const int C = 256;
			string a = "";
			string b = "";

			for (int i = 0; i < one.Length; i++)
				if (one[i] > 255)
					a += ' ';
				else
					a += one[i];

			for (int i = 0; i < two.Length; i++)
				if (two[i] > 255)
					b += ' ';
				else
					b += two[i];

			// "infinite" distance is just the max possible distance
			int INF = a.Length + b.Length;
 
			// make and initialize the character array indices            
			int[] DA = new int[C];
			for (int k = 0; k < C; ++k) 
				DA[k]=0;
 
			// make the distance matrix H[-1..a.length][-1..b.length]
			int[,] H = new int[a.Length+2,b.Length+2];
 
			// initialize the left and top edges of H
			SetStupid(-1, -1, ref H, INF);
			for (int i = 0; i <= a.Length; ++i)
			{
				SetStupid(i, -1, ref H, INF);
				SetStupid(i, 0, ref H, i);
			}
			for (int j = 0; j <= b.Length; ++j)
			{
				SetStupid(-1, j, ref H, INF);
				SetStupid(0, j, ref H, j);
			}
 
			// fill in the distance matrix H
			// look at each character in a
			for (int i = 1; i <= a.Length; ++i)
			{
				int DB = 0;
				// look at each character in b
				for (int j = 1; j <= b.Length; ++j)
				{
					int i1 = DA[b[j-1]];
					int j1 = DB;
					int cost;
					if (a[i-1] == b[j-1])
					{
						cost = 0;
						DB   = j;
					}
					else
					   cost = 1;

					SetStupid(i, j, ref H, Math.Min(GetStupid(i - 1, j - 1, H) + cost,
										  Math.Min(GetStupid(i, j - 1, H) + 1,
										  Math.Min(GetStupid(i - 1, j, H) + 1,
										  GetStupid(i1 - 1, j1 - 1, H) + (i - i1 - 1) + 1 + (j - j1 - 1)))));

					//H[i][j] = Math.min(    H[i-1 ][j-1 ] + cost,  // substitution
					//                       H[i   ][j-1 ] + 1,     // insertion
					//                       H[i-1 ][j   ] + 1,     // deletion
					//                       H[i1-1][j1-1] + (i-i1-1) + 1 + (j-j1-1));
				}
				DA[a[i-1]] = i;
			}
			return GetStupid(a.Length, b.Length, H);// H[a.Length][b.Length];
		}

		/// <summary>
		/// Only compare the first N letters for Levenshtein
		/// </summary>
		/// <param name="s"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public static int ShortLevenshteinDistance(string s, string t, int length)
		{
			return LevenshteinDistance(s.Truncate(length), t.Truncate(length));
		}

		/// <summary>
		/// Returns a string which only has maxLength of the original characters. Truncates the right side.
		/// </summary>
		/// <param name="value"></param>
		/// <param name="maxLength"></param>
		/// <returns></returns>
		public static string Truncate(this string value, int maxLength)
		{
			if (string.IsNullOrEmpty(value)) return value;
			return value.Length <= maxLength ? value : value.Substring(0, maxLength);
		}

      public static string ShortDecimal(double data, int decimalPlaces = 2)
      {
         return string.Format("{0:0." + new string('0', decimalPlaces) + "}", data);
      }

		public static string ReplaceBadWords(string data, string[] badWords, out int badWordCount)
		{
			int count = 0;
			Regex r;
			string op = data;
			foreach (var word in badWords)
			{
				var expword = ExpandBadWordToIncludeIntentionalMisspellings(word);
				r = new Regex(@"(?<Pre>\s+)(?<Word>" + expword + @")(?<Post>\s+|\!\?|\.)");
				var matches = r.Matches(data);
				foreach (Match match in matches)
				{
					string pre = match.Groups["Pre"].Value;
					string post = match.Groups["Post"].Value;
					string output = pre + new string('*', word.Length) + post;
					op = op.Replace(match.Value, output);
					count++;
				}
			}
			badWordCount = count;
			return op;
		}

		public static string ExpandBadWordToIncludeIntentionalMisspellings(string word)
		{
			var chars = word
				.ToCharArray();

			var op = "[" + string.Join("][", chars) + "]";

			return op
				.Replace("[a]", "[a A @]")
				.Replace("[b]", "[b B I3 l3 i3]")
				.Replace("[c]", "(?:[c C \\(]|[k K])")
				.Replace("[d]", "[d D]")
				.Replace("[e]", "[e E 3]")
				.Replace("[f]", "(?:[f F]|[ph pH Ph PH])")
				.Replace("[g]", "[g G 6]")
				.Replace("[h]", "[h H]")
				.Replace("[i]", "[i I l ! 1]")
				.Replace("[j]", "[j J]")
				.Replace("[k]", "(?:[c C \\(]|[k K])")
				.Replace("[l]", "[l L 1 ! i]")
				.Replace("[m]", "[m M]")
				.Replace("[n]", "[n N]")
				.Replace("[o]", "[o O 0]")
				.Replace("[p]", "[p P]")
				.Replace("[q]", "[q Q 9]")
				.Replace("[r]", "[r R]")
				.Replace("[s]", "[s S $ 5]")
				.Replace("[t]", "[t T 7]")
				.Replace("[u]", "[u U v V]")
				.Replace("[v]", "[v V u U]")
				.Replace("[w]", "[w W vv VV]")
				.Replace("[x]", "[x X]")
				.Replace("[y]", "[y Y]")
				.Replace("[z]", "[z Z 2]")
				;
		}

		public static string WordWrap(string original, int width)
		{
			string newString = "";
			string[] words = original.Split(" ".ToArray());
			string currentLine = "";

			foreach (string word in words)
			{
				if (currentLine.Length + word.Length <width)
				{
					currentLine += word;
				}
				else
				{
					newString += currentLine.TrimEnd() + "\n";
					currentLine = word;
				}

				currentLine += " ";
			}

			newString += currentLine;

			return newString;
		}

		public static string ShiftLines(string original, int shift = 1)
		{
			if (shift <= 0)
				return original;

			return ShiftLines(original, new string(' ', shift));
		}

		public static string ShiftLines(string original, string shiftString)
		{
			return shiftString + original.Replace("\n", "\n" + shiftString);
		}

		public static string Pluralify(this string baseWord, int num)
		{
			if (num == 1)
				return baseWord;
			else
				return baseWord + "s";
		}

		public static string LargestTime(TimeSpan time)
		{
			if (time.Ticks < 0)
				return "consult Dialga";

			int days = (int)time.TotalDays;
			int hours = (int)time.TotalHours;
			int minutes = (int)time.TotalMinutes;
			int seconds = (int)time.TotalSeconds;
            int milliseconds = (int)time.TotalMilliseconds;

			if (days > 0)
				return days + " day".Pluralify(days);
			if (hours > 0)
				return hours + " hour".Pluralify(hours);
			if (minutes > 0)
				return minutes + " minute".Pluralify(minutes);
            if (seconds > 0)
			    return seconds + " second".Pluralify(seconds);

            return milliseconds + " millisecond".Pluralify(milliseconds);
		}

		public static string ToRoman(int number)
		{
			if ((number < 0) || (number > 3999)) throw new ArgumentOutOfRangeException("insert value betwheen 1 and 3999");
			if (number < 1) return string.Empty;
			if (number >= 1000) return "M" + ToRoman(number - 1000);
			if (number >= 900) return "CM" + ToRoman(number - 900); //EDIT: i've typed 400 instead 900
			if (number >= 500) return "D" + ToRoman(number - 500);
			if (number >= 400) return "CD" + ToRoman(number - 400);
			if (number >= 100) return "C" + ToRoman(number - 100);
			if (number >= 90) return "XC" + ToRoman(number - 90);
			if (number >= 50) return "L" + ToRoman(number - 50);
			if (number >= 40) return "XL" + ToRoman(number - 40);
			if (number >= 10) return "X" + ToRoman(number - 10);
			if (number >= 9) return "IX" + ToRoman(number - 9);
			if (number >= 5) return "V" + ToRoman(number - 5);
			if (number >= 4) return "IV" + ToRoman(number - 4);
			if (number >= 1) return "I" + ToRoman(number - 1);
			throw new ArgumentOutOfRangeException("something bad happened");
		}

		public static string RemoveLinks(this string baseString)
		{
			return Regex.Replace(baseString, @"<\s*a\s+href\s*\=s*""([^""]*)""\s*>[^<]*<\s*/a\s*>", "$1");
		}

      public static string AutoCorrectionMatch(string substring, List<string> possibilities)
      {
         return AutoCorrectionMatch_Unsafe(substring.Truncate(SafeStringSize), possibilities.Select(x => x.Truncate(SafeStringSize)).ToList());
      }

		public static string AutoCorrectionMatch_Unsafe(string substring, List<string> possibilities)
		{
			if (possibilities.Count == 0)
				return "";

			List<string> subStringMatch = possibilities.Where(x => !string.IsNullOrWhiteSpace(x) && x.ToLower().StartsWith(substring.ToLower())).ToList();
			if (subStringMatch != null && subStringMatch.Count == 1)
				return subStringMatch[0];	//There is only one string which starts with our substring. This is the best match in human terms
			else
				return possibilities.OrderBy(x => StringExtensions.StringDifference_Unsafe(substring.ToLower(), x.ToLower())).First();
            //return possibilities.OrderBy(x => (double)StringExtensions.DamerauLevenshteinDistance(substring.ToLower(), x.ToLower()) / x.Length).First();
		}

      public static string PathFixer(string path) //A helper function which fixes paths if they don't include the ending slashes
      {
         if (!string.IsNullOrWhiteSpace(path) && !path.EndsWith(Path.DirectorySeparatorChar.ToString()))
            return path + Path.DirectorySeparatorChar;

         return path;
      }

      //Convert byte array to hexadecimal string
      public static string ByteToHex(byte[] ba)
      {
         string hex = BitConverter.ToString(ba);
         return hex.Replace("-","");
      }
	}
}
