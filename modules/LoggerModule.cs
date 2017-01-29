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
      private List<MessageJSONObject> unsavedMessages = new List<MessageJSONObject>();

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
         Dictionary<string, List<MessageJSONObject>> messageByDate = new Dictionary<string, List<MessageJSONObject>>();

         foreach (MessageJSONObject message in unsavedMessages)
         {
            string date = message.GetCreationTime().ToString("yy-MM-dd");
            if (!messageByDate.ContainsKey(date))
               messageByDate.Add(date, new List<MessageJSONObject>());
            messageByDate[date].Add(message);
         }

         try
         {
            foreach(string date in messageByDate.Keys)
            {
               File.AppendAllLines(date + ".txt", messageByDate[date].Select(x => 
                  x.sender.username.PadLeft(20) + x.GetCreationTime().ToString("[HH:mm]") + StringExtensions.Truncate(x.tag, 1) + ": " + x.GetRawMessage()));
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

      public override void ProcessMessage(MessageJSONObject message, UserInfo user, Dictionary<int, UserInfo> users)
      {
         if(message.IsSendable())
            unsavedMessages.Add(message);
      }
   }
}