using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.Serialization.Formatters.Soap;
using System.Xml;

namespace MyExtensions
{
   [Serializable]
	public class Options
	{
		//public const string SubsectionIdentifier = "*";
		//public const string CommentIdentifier = "#";
		//public const string ConfigurationFile = "config.ini";

      private readonly Dictionary<string, Dictionary<string, object>> optionData = 
         new Dictionary<string, Dictionary<string, object>>();

		public Options()
		{
         AddOptions ("default", new Dictionary<string, object> ());
		}

      public Options(Options copy)
      {
         AddOptions(copy);
      }

      //Only add options that are not already here.
      public void AddMissing(Options options)
      {
         foreach (string key in options.optionData.Keys)
         {
            if (!optionData.ContainsKey(key))
               optionData.Add(key, new Dictionary<string, object>());

            foreach (string subkey in options.optionData[key].Keys)
            {
               //Only assign if we don't already have it
               if(!optionData[key].ContainsKey(subkey))
                  optionData[key][subkey] = options.optionData[key][subkey];
            }
         }
      }

      //Hopefully this will just preserve types...
      public void AddOptions(string section, Dictionary<string, object> values)
      {
         if (!optionData.ContainsKey (section))
            optionData.Add (section, new Dictionary<string, object>());
        
         foreach (KeyValuePair<string, object> valuePair in values) 
         {
            if (!optionData [section].ContainsKey (valuePair.Key))
               optionData [section].Add (valuePair.Key, valuePair.Value);
            else
               optionData [section] [valuePair.Key] = valuePair.Value;
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

      //Try to write out options to the given file. Returns success or not
      public bool WriteToFile(string filename)
      {
         Stream OptionFileStream = null;

         try
         {
            OptionFileStream = File.Create(filename);
            //System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(optionData.GetType());
            SoapFormatter serializer = new SoapFormatter();
            serializer.Serialize(OptionFileStream, optionData);//optionData.Select(kv=>new OptionPair(){id = kv.Key,value=kv.Value}).ToArray());
         }
         catch (Exception e)
         {
            Console.WriteLine("Exception: " + e);
            return false;
         }
         finally
         {
            if (OptionFileStream != null)
               OptionFileStream.Close();
         }

         return true;
      }

      //Try to load options from the given file. Returns success or not
      public bool LoadFromFile(string filename)
      {
         Stream OptionFileStream = null;
         try
         {
            OptionFileStream = File.OpenRead(filename);
            //System.Xml.Serialization.XmlSerializer deserializer = new System.Xml.Serialization.XmlSerializer(typeof(OptionPair[]));
            SoapFormatter deserializer = new SoapFormatter();
            Dictionary<string, Dictionary<string, object>> tempOptions = 
               (Dictionary<string, Dictionary<string, object>>)deserializer.Deserialize(OptionFileStream);

            foreach(string key in tempOptions.Keys)
               AddOptions(key, tempOptions[key]);
         }
         catch 
         {
            return false;
         }
         finally
         {
            if (OptionFileStream != null)
               OptionFileStream.Close();
         }

         return true;
      }

//		public Options(string options) : this()
//		{
//			string[] lines = options.Split("\n\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
//			string subsection = "default";
//
//			foreach (string rawLine in lines.Select(x => x.Trim()))
//			{
//				//Skip comments
//				if (rawLine.StartsWith(CommentIdentifier))
//					continue;
//
//				string line = Regex.Replace(rawLine, "#.*$", "");
//
//				//Update the subsection to add options to 
//				if (line.StartsWith(SubsectionIdentifier))
//				{
//					subsection = line.Replace(SubsectionIdentifier, "").Trim();
//
//					if (!optionData.ContainsKey(subsection))
//						optionData.Add(subsection, new Dictionary<string, object>());
//
//					continue;
//				}
//
//				//Separate the name of the option from the value of the option
//				string[] parts = line.Split("=".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x=>x.Trim()).ToArray();
//
//				//Figure out the data type of the option
//				if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
//				{
//					bool trueFalse;
//					int number;
//					double real;
//
//					if (bool.TryParse(parts[1], out trueFalse))
//					{
//						optionData[subsection][parts[0]] = trueFalse;
//					}
//					else if (int.TryParse(parts[1], out number))
//					{
//						optionData[subsection][parts[0]] = number;
//					}
//					else if (double.TryParse(parts[1], out real))
//					{
//						optionData[subsection][parts[0]] = real;
//					}
//					else
//					{
//						optionData[subsection][parts[0]] = parts[1];
//					}
//				}
//			}
//		}

      public Dictionary<string, object> GetOptionsForKey(string key)
      {
         if (optionData.ContainsKey(key))
            return optionData[key];
         else
            return new Dictionary<string, object>();
      }

      //Break options up by global key. The returned list contains option objects that only have
      //one global key.
      public Dictionary<string, Options> BreakOptions()
      {
         Dictionary<string, Options> optionList = new Dictionary<string, Options>();

         foreach (string key in optionData.Keys)
         {
            Options option = new Options();
            option.AddOptions(key, optionData[key]);

            optionList.Add(key, option);
         }

         return optionList;
      }

		//Retrieve a dictionary value as a particular type. Should be safe for the
		//basic types used in the option file (bool, double, int, string)
		public T GetAsType<T>(string key, string subkey)
		{
         try
         {
            return (T)Convert.ChangeType(this[key, subkey], typeof(T));
         }
         catch
         {
            return (T)GetDefaultTypeValue(typeof(T));
         }

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

		public List<string> Keys
		{
			get { return optionData.Keys.ToList(); }
		}
	}
}
