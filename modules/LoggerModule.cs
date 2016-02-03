using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Linq;
using MyExtensions;
using System.IO;

namespace ModulePackage1
{
   public class LoggerModule : Module
   {
      private List<UserMessageJSONObject> unsavedMessages = new List<UserMessageJSONObject>();

      public LoggerModule()
      {
         GeneralHelp = "You can't do anything with this module yet.";
      }

      public override string Nickname
      {
         get
         {
            return "logger";
         }
      }

      public override bool Hidden(UserInfo user)
      {
         return true;
      }

      public override bool SaveFiles()
      {
         bool result = true;
         Dictionary<string, List<UserMessageJSONObject>> messageByDate = new Dictionary<string, List<UserMessageJSONObject>>();

         foreach (UserMessageJSONObject message in unsavedMessages)
         {
            string date = message.PostTime().ToString("yy-MM-dd");
            if (!messageByDate.ContainsKey(date))
               messageByDate.Add(date, new List<UserMessageJSONObject>());
            messageByDate[date].Add(message);
         }

         try
         {
            foreach(string date in messageByDate.Keys)
            {
               File.AppendAllLines(date + ".txt", messageByDate[date].Select(x => 
                  x.username.PadLeft(20) + x.PostTime().ToString("[HH:mm]") + StringExtensions.Truncate(x.tag, 1) + ": " + System.Net.WebUtility.HtmlDecode(x.message)));
            }
         }
         catch(Exception e)
         {
            Log("Save error: " + e.Message, MyExtensions.Logging.LogLevel.Error);
            result = false;
         }
         finally
         {
            unsavedMessages.Clear();
         }

         return result;
      }

      public override void ProcessMessage(UserMessageJSONObject message, UserInfo user, Dictionary<int, UserInfo> users)
      {
         if(message.Display)
            unsavedMessages.Add(message);
      }
   }
}