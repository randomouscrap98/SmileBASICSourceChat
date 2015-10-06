using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System.Net;
using MyExtensions.Logging;

namespace ChatEssentials
{
   //All available tags in different langauges
   public enum ChatTags
   {
      Join,
      Leave,
      Welcome,
      Warning,
      Blocked,
      None = 99
   }

   public enum ChatReplaceables
   {
      Username,
      Seconds
   }

   //A sort of container class for all language tags provided by the server
   public class LanguageTags
   {
      //When logging, messages from this class will show up with this tag.
      public const string LogTag = "LanguageTags";

      private Dictionary<string, Dictionary<string, string>> tags = null;
      private Logger logger = Logger.DefaultLogger;
      private bool printedDefaultTagsWarning = false;
      private HashSet<Tuple<string, string>> missingTagWarnings = new HashSet<Tuple<string, string>>();

      public LanguageTags(Logger newLogger = null)
      {
         if (newLogger != null)
            logger = newLogger;
      }

      //In case we simply can't load a language tag specification, we provide some defaults.
      public Dictionary<ChatTags, string> DefaultTags
      {
         get
         {
            return new Dictionary<ChatTags, string>
            {
               { ChatTags.Join, UserReplacement + " has entered the chat." },
               { ChatTags.Leave, UserReplacement + " has left the chat." },
               { ChatTags.Welcome, "Welcome to the SmileBASIC Source chat, " + UserReplacement },
               { ChatTags.Warning, "Warning: Your spam score is high. Please wait a bit before posting again." },
               { ChatTags.Blocked, "You have been blocked for " + SecondsReplacement + " seconds for spamming." }
            };
         }
      }

      //Get the actual substitute text for the given replacement tag name (not language tag btw)
      public static string ReplacementText(string baseTag)
      {
         return "{" + baseTag + "}";
      }

      public static string ReplacementText(ChatReplaceables baseTag)
      {
         return "{" + baseTag.ToString().ToLower() + "}";
      }

      public static string UserReplacement
      {
         get { return ReplacementText(ChatReplaceables.Username); }
      }

      public static string SecondsReplacement
      {
         get { return ReplacementText(ChatReplaceables.Seconds); }
      }

      //Initialize language tags from a networked file (using a URL though)
      public bool InitFromURL(string url)
      {
         Log("Trying to load language from URL: " + url, LogLevel.Debug);

         try
         {
            using (WebClient client = new WebClient())
            {
               string htmlCode = client.DownloadString(url);
               return InitFromString(htmlCode);
            }
         }
         catch (Exception e)
         {
            Log("Can't init language from URL: " + e.Message, LogLevel.Error);
            return false;
         }
      }

      //Initialize language tags from a local file
      public bool InitFromFile(string file)
      {
         Log("Trying to load language from file: " + file, LogLevel.Debug);

         try
         {
            string jsonText = File.ReadAllText(file);
            return InitFromString(jsonText);
         }
         catch (Exception e)
         {
            logger.Error("Can't init language from file: " + e.Message);
            return false;
         }
      }

      //Plain old initialization of tags from a json string
      public bool InitFromString(string json)
      {
         try
         {
            tags = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json);
            return true;
         }
         catch (Exception e)
         {
            logger.Error("Can't init language from json: " + e.Message);
            return false;
         }
      }

      //provides a uniform way to add messages to the logger with the correct tag
      private void Log(string message, LogLevel level = LogLevel.Normal)
      {
         logger.LogGeneral(message, level, LogTag);
      }

      //Perform all the replacements necessary with the given user information
      private string ReplaceText(string message, Dictionary<ChatReplaceables, string> replacements)
      {
         foreach (KeyValuePair<ChatReplaceables, string> replacementTag in replacements)
            message = message.Replace(ReplacementText(replacementTag.Key.ToString().ToLower()), replacementTag.Value);

         return message;
      }

      public static Dictionary<ChatReplaceables, string> QuickDictionary(UserInfo user)
      {
         return new Dictionary<ChatReplaceables, string>
         {
            { ChatReplaceables.Username, user.Username },
            { ChatReplaceables.Seconds, user.SecondsToUnblock.ToString() }
         }.ToDictionary(x => x.Key, y => y.Value);
      }

      //Get the given tag but "localized" for the given user.
      public string GetTag(LanguageTagParameters parameters)
      {
         //This is just silly
         if (parameters.Tag == ChatTags.None)
            return "";

         try
         {
            //For a lot of errors, we're better off returning the default tag
            string defaultReplacement = ReplaceText(DefaultTags[parameters.Tag], parameters.Replacements);

            //Oops, tag dictionary was not initialized. Using default
            if(tags == null)
            {
               if(!printedDefaultTagsWarning)
               {
                  Log("Language not initialized. Using defaults", LogLevel.Warning);
                  printedDefaultTagsWarning = true;
               }
               
               return defaultReplacement;
            }
            //Oops, the user's language couldn't be found (wut). Use default
            else if (!tags.ContainsKey(parameters.Language))
            {
               Log(parameters.User.Username + "'s language (" + parameters.Language + ") was not found! Using default");
               return defaultReplacement;
            }

            string realTag = parameters.Tag.ToString().ToLower();
            Tuple<string, string> tagTuple = Tuple.Create(parameters.Language, realTag);

            //Oops, looks like the dictionary we initialized from doesn't have the tag we're looking for.
            if(!tags[parameters.Language].ContainsKey(realTag))
            {
               //Don't bombard the console with warning about missing tags. Only do it the first time
               if(!missingTagWarnings.Contains(tagTuple))
               {
                  missingTagWarnings.Add(tagTuple);
                  Log("Language dictionary was missing tag: " + realTag + " for language: " + parameters.Language, LogLevel.Warning);
               }

               return defaultReplacement;
            }

            return ReplaceText(tags[parameters.Language][realTag], parameters.Replacements);
         }
         catch (Exception e)
         {
            Log("Error while retrieving tag " + parameters.Tag + " for user " + parameters.User.Username + ": " + e.Message);
            return "Tag error: Fatal internal error. The server is broken";
         }
      }
   }

   public class LanguageTagParameters
   {
      public readonly ChatTags Tag;
      public readonly Dictionary<ChatReplaceables, string> Replacements;
      private UserInfo user;

      public LanguageTagParameters(ChatTags tag, User user, User userForDictionary = null)
      {
         if (userForDictionary == null)
            userForDictionary = user;

         Tag = tag;
         this.user = new UserInfo(user, true);
         Replacements = LanguageTags.QuickDictionary(new UserInfo(userForDictionary, true));
      }

      public void UpdateUser(User user)
      {
         this.user = new UserInfo(user, true);
      }

      public string Language
      {
         get { return User.Language; }
      }

      public UserInfo User
      {
         get { return user; }
      }
   }
}
