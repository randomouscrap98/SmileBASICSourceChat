using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;

namespace WikiaChatLogger
{
	public class Options
	{
		public const string SubsectionIdentifier = "*";
		public const string CommentIdentifier = "#";
		public const string ConfigurationFile = "config.ini";

		private readonly Dictionary<string, Dictionary<string, object>> optionData;

		public Options()
		{
			optionData = new Dictionary<string, Dictionary<string, object>>()
			{
				{"default", new Dictionary<string, object>()}
			};
		}

		public Options(string options) : this()
		{
			string[] lines = options.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
			string subsection = "default";

			foreach (string rawLine in lines.Select(x => x.Trim()))
			{
				//Skip comments
				if (rawLine.StartsWith(CommentIdentifier))
					continue;

				string line = Regex.Replace(rawLine, "#.*$", "");

				//Update the subsection to add options to 
				if (line.StartsWith(SubsectionIdentifier))
				{
					subsection = line.Replace(SubsectionIdentifier, "").Trim();

					if (!optionData.ContainsKey(subsection))
						optionData.Add(subsection, new Dictionary<string, object>());

					continue;
				}

				//Separate the name of the option from the value of the option
				string[] parts = line.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).ToArray();

				//Figure out the data type of the option
				if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
				{
					bool trueFalse;
					int number;
					double real;

					if (bool.TryParse(parts[1], out trueFalse))
					{
						optionData[subsection][parts[0]] = trueFalse;
					}
					else if (int.TryParse(parts[1], out number))
					{
						optionData[subsection][parts[0]] = number;
					}
					else if (double.TryParse(parts[1], out real))
					{
						optionData[subsection][parts[0]] = real;
					}
					else
					{
						optionData[subsection][parts[0]] = parts[1];
					}
				}
			}
		}

		//Retrieve a dictionary value as a particular type. Should be safe for the
		//basic types used in the option file (bool, double, int, string)
		public T GetAsType<T>(string key, string subkey)
		{
			object returnValue = this[key, subkey];

			if (returnValue == null || typeof(T) != returnValue.GetType())
				return (T)GetDefaultTypeValue(typeof(T));

			return (T)returnValue;		

			//if (typeof(T) == typeof(string))
			//    returnValue = "";
			//else if (typeof(T) == typeof(bool))
			//    returnValue = false;
			//else if (typeof(T) == typeof(int))
			//    returnValue = 0;
			//else if (typeof(T) == typeof(double))
			//    returnValue = 0.0d;

			//return (T)returnValue;
		}

		public static object GetDefaultTypeValue(Type T)
		{
			object returnValue = null;

			if (T.IsValueType)
				returnValue = Activator.CreateInstance(T);

			if (T == typeof(string))
				returnValue = "";
			else if (T == typeof(bool))
				returnValue = false;
			else if (T == typeof(int))
				returnValue = 0;
			else if (T == typeof(double))
				returnValue = 0.0d;

			return returnValue;
		}

		//Return the raw object stored in the data structure. Use GetAsType instead.
		public object this[string key, string subkey]
		{
			get
			{
				if (optionData.ContainsKey(key) && optionData[key].ContainsKey(subkey))
					return optionData[key][subkey];

				return null;
			}
		}

		//"Append" the given options to this one. Options will overwrite
		//previous values.
		public void AddOptions(Options options)
		{
			foreach (string key in options.optionData.Keys)
			{
				if (!optionData.ContainsKey(key))
					optionData.Add(key, new Dictionary<string, object>());

				foreach (string subkey in options.optionData[key].Keys)
				{
					optionData[key][subkey] = options.optionData[key][subkey];
				}
			}
		}

		public List<string> Keys
		{
			get { return optionData.Keys.ToList(); }
		}
	}
}
