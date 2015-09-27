using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Net;
using Newtonsoft.Json;
using MyExtensions;
using System.Linq;

namespace ModulePackage1
{
   //Holds statistics for one user.
   public class UserStatistics
   {
      private long messageCount = 0;
      private long totalCharacters = 0;
      private long totalUsers = 0;

      private HashSet<int> allSeenUsers = new HashSet<int>();

      //When chat server receives message from this user, perform this using the message
      public void AddMessage(string message)
      {
         messageCount++;
         totalCharacters += message.Length;
      }

      //When user sends message, add statistics about the users currently in chat
      public void AddUsers(List<int> usersInChat)
      {
         totalUsers += usersInChat.Count;
         allSeenUsers.UnionWith(usersInChat);
      }

      public long TotalMessages
      {
         get { return messageCount; }
      }

      public double AverageMessageLength
      {
         get { return (double)totalCharacters / messageCount; }
      }

      public double AverageUsersWhenChatting
      {
         get { return (double)totalUsers / messageCount; }
      }

      public int UniqueUsersSeen
      {
         get { return allSeenUsers.Count; }
      }
   }

   public class StatisticsModule : Module
   {
      private Dictionary<int, UserStatistics> userStatistics = new Dictionary<int, UserStatistics>();

      public StatisticsModule()
      {
         commands.Add(new ModuleCommand("mystatistics", new List<CommandArgument> (), "view personal chat statistics", false));
         commands.Add(new ModuleCommand("statistics", new List<CommandArgument> (), "view global chat statistics", false));
      }

      public override bool LoadFiles()
      {
         return MySerialize.LoadObject<Dictionary<int, UserStatistics>>(DefaultSaveFile, out userStatistics);
      }

      public override bool SaveFiles()
      {
         return MySerialize.SaveObject<Dictionary<int, UserStatistics>>(DefaultSaveFile, userStatistics);
      }

      public override void ProcessMessage(UserMessageJSONObject message, User user, Dictionary<int, User> users)
      {
         //Add user to statistics dictionary if they don't already exist.
         if (!userStatistics.ContainsKey(user.UID))
         {
            userStatistics.Add(user.UID, new UserStatistics());
            Log("Added new user: " + user.Username);
         }
            
         userStatistics[user.UID].AddMessage(message.message);
         userStatistics[user.UID].AddUsers(users.Select(x => x.Value.UID).ToList());
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<int, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput = new ModuleJSONObject();

         List<UserStatistics> allStats = userStatistics.Select(x => x.Value).ToList();

         switch(command.Command)
         {
            case "mystatistics":
               if (!userStatistics.ContainsKey(user.UID))
               {
                  moduleOutput.message = "You have no statistics yet. You will after this message!";
               }
               else
               {
                  UserStatistics myStats = userStatistics[user.UID];
                  moduleOutput.message = "---Your Chat Statistics---\n";
                  moduleOutput.message += "Total messages: " + myStats.TotalMessages + 
                     " (" + string.Format("{0:N2}%", myStats.TotalMessages * 100.0 / allStats.Sum(x => x.TotalMessages)) + 
                     ", #" + (allStats.OrderByDescending(x => x.TotalMessages).ToList().IndexOf(myStats) + 1) + ")\n";
                  moduleOutput.message += "Average message size: " + (int)myStats.AverageMessageLength +
                     " characters (#" + (allStats.OrderByDescending(x => x.AverageMessageLength).ToList().IndexOf(myStats) + 1) + ")\n";
                  moduleOutput.message += "Average users while chatting: " + (int)myStats.AverageUsersWhenChatting + "\n";
                  moduleOutput.message += "Total users you've seen: " + myStats.UniqueUsersSeen + "\n";
               }
               outputs.Add(moduleOutput);
               break;

            case "statistics":
               moduleOutput.message = "---Global Chat Statistics---\n";
               moduleOutput.message += "Total messages: " + allStats.Sum(x => x.TotalMessages) + "\n";
               moduleOutput.message += "Average message size: " + (int)(allStats.Sum(x => x.AverageMessageLength) / allStats.Count) + " characters\n";
               moduleOutput.message += "Total users seen: " + allStats.Count + "\n";
               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }
   }
}