using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace MyExtensions
{
	public class SpecialPoint
	{
		int across = 0;
		int down = 0;

		public SpecialPoint() { }

		//Initialize point based on real integer location
		public SpecialPoint(int across, int down)
		{
			this.across = across;
			this.down = down;
		}

		//Initialize point based on external point system (A1, C5, etc.)
		public SpecialPoint(char across, int down)
		{
			this.across = char.ToLower(across) - 'a';
			this.down = down;
		}

		//Give this point as a string
		public override string ToString()
		{
			string representation = "";
			representation += (char)(across + 'A');
			representation += down.ToString();

			return representation;
		}

		//Accessors (so that the point isn't useless)
		public int Across
		{
			get { return across; }
		}
		public int Down
		{
			get { return down; }
		}

		//Assume that the string is correct and just parse anyway. If it didn't work, it'll return null
		public static SpecialPoint Parse(string parseString)
		{
			SpecialPoint point;

			TryParse(parseString, out point);

			return point;
		}

		//Attempt to parse a string into a point.
		public static bool TryParse(string parseString, out SpecialPoint point)
		{
			Match match = Regex.Match(parseString, @"([a-zA-Z])([0-9]+)");

			point = null;

			//Oops, the point isn't in the format I want it in
			if (!match.Success)
				return false;

			//Set up the new point
			int down = int.Parse(match.Groups[2].Value);
			point = new SpecialPoint(match.Groups[1].Value[0], down);

			return true;
		}

		public override bool Equals(object obj)
		{
			if (obj is SpecialPoint)
				return Equals((SpecialPoint)obj);

			return false;
		}
		public override int GetHashCode()
		{
			return across + down * 26;
		}
		public bool Equals(SpecialPoint otherPoint)
		{
			return otherPoint.across == across && otherPoint.down == down;
		}
	}
}
