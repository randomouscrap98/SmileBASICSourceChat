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
   //Holds statistics for one user. This is a statistics-specific class and has nothing to do with modules.
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
         get 
         { 
            if (messageCount == 0)
               return 0;
            
            return (double)totalCharacters / messageCount; 
         }
      }

      public double AverageUsersWhenChatting
      {
         get 
         { 
            if (messageCount == 0)
               return 0;
            
            return (double)totalUsers / messageCount; 
         }
      }

      public int UniqueUsersSeen
      {
         get { return allSeenUsers.Count; }
      }
   }

   //This is my derived class. I derive my class from "Module" here. This means I have certain functions
   //I can "override" so that I have custom functionality.
   public class StatisticsModule : Module
   {
      //A variable which holds a whole bunch of user statistics. Users are designated by numbers in modules, not by name
      private Dictionary<int, UserStatistics> userStatistics = new Dictionary<int, UserStatistics>();

      //The constructor for your module should NOT have any parameters! It MUST be an empty constructor, or it will not load
      public StatisticsModule()
      {
         //These lines register commands with the chat server. You give the command a name, you specify the list of arguments
         //(if any), and provide a description for your command. The "false" or "true" at the end is whether or not this
         //command should update the spamscore (I think it defaults to false... check intellisense). There are some
         //standard formats for a command argument; for instance, it could be an integer, a module, a user, etc. If you
         //choose "user", it will automatically search the database for you and reject commands that do not include a valid
         //user. It will also perform the ? and ?? completion for you, so you do not need to worry about any of that. Assume
         //that if an argument is of type ArgumentType.User, you will be getting a real user. If you need a special format
         //for your argument, you can specify ArgumentType.Custom and include your custom regex.
         commands.Add(new ModuleCommand("mystatistics", new List<CommandArgument> (), "view personal chat statistics", false));
         commands.Add(new ModuleCommand("statistics", new List<CommandArgument> (), "view global chat statistics", false));
         commands.Add(new ModuleCommand("statistics", new List<CommandArgument> { 
            new CommandArgument("user", ArgumentType.User) }, "view user chat statistics", false));
      }

      public override bool LoadFiles()
      {
         //This "LoadObject" function turns a file saved with "SaveObject" into that object again. If you stick with saving
         //and loading through LoadObject and SaveObject, you will be a happy person. DefaultSaveFile is a filename provided
         //by the Module base class which is nicely named (in case you're too lazy to come up with a name, like me). That
         //"out" next to userStatistics is important; make sure you put that in yours too (but put your variable there instead
         //of userStatistics).
         return MySerialize.LoadObject<Dictionary<int, UserStatistics>>(DefaultSaveFile, out userStatistics);
      }

      public override bool SaveFiles()
      {
         //This function converts objects into files. It lets you save stuff like your variables into a file, so that when
         //the chat server restarts, your data is still there. Anything you want to persist should be saved with SaveObject.
         //Note this function works in conjunction with LoadObject, so files saved with SaveObject should be able to be loaded
         //with LoadObject. You can call this function for each variable you want to save; I'm only calling it once because
         //I only save one object.
         return MySerialize.SaveObject<Dictionary<int, UserStatistics>>(DefaultSaveFile, userStatistics);
      }

      //Most modules will not need this. This function is called by the chat server whenever it recieves a message 
      //(any message, including non commands). Unless you're performing analysis or saving information on each message
      //(like this statistics class), you won't need to use this. Notice that it does not return any information; you
      //cannot give output to the chat server here. Also notice the "Log" function call here. This is a wrapper provided
      //by Module which lets you write to the chat server log. There's a whole system in place for creating your own logs,
      //but you probably just want to write to the main log. You can also specify the level of the message, which defaults
      //to "Normal". If the message is unimportant, you probably want "Debug".
      public override void ProcessMessage(UserMessageJSONObject message, UserInfo user, Dictionary<int, UserInfo> users)
      {
         //Add user to statistics dictionary if they don't already exist.
         if (!userStatistics.ContainsKey(user.UID))
         {
            userStatistics.Add(user.UID, new UserStatistics());
            Log("Added new user: " + user.Username);
         }
            
         if (message.Display)
         {
            userStatistics[user.UID].AddMessage(message.message);
            userStatistics[user.UID].AddUsers(users.Where(x => x.Value.LoggedIn).Select(x => x.Value.UID).ToList());
         }
      }

      //THIS is the function you're looking for. When the chat server detects a command for your module, it passes it along
      //to this function. You can process the command here, then send the output as the return object. Notice that the return
      //is a list; you can return multiple messages per command. This function is called ONCE for each command detected. 
      //"user" is the user who performed the command; the class UserInfo contains a lot of information about a user, so
      //use it as needed. The provided dictionary is a list of ALL users ever seen by the chat, so it's like the chat server's
      //database. Each UserInfo object has a "LoggedIn" field, so you can tell if they're logged in or not. "command" has
      //two important fields: "Command" and "Arguments". Command is simply the string containing just the command performed,
      //and Arguments is a list (array) of the arguments, each as a string. The returned messages are in a JSON format, which
      //is masked by the JSONObject class. You will almost ALWAYS be returning one or more ModuleJSONObjects, which is a 
      //chat server message specific to modules. You can also return system messages, etc. All the X_JSONObjects classes like
      //ModuleJSONObject derive from the JSONObject class, so you can return any of them. The "message" field of the 
      //ModuleJSONObject holds the output, and you can change the recipient by adding users (as integers) to the "recipients"
      //field. If you do not specify any recipients, it defaults to the sender (this is usually what you want). If you
      //want to broadcast a message to everyone (please don't do this for every command), you can set the "broadcast" field
      //to true.
      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         //This is what we're returning from our function
         List<JSONObject> outputs = new List<JSONObject>();

         //Our commands (and probably yours too) usually returns just one message, so that's what this is.
         //It gets added to the above "outputs" list.
         ModuleJSONObject moduleOutput = new ModuleJSONObject();

         List<UserStatistics> allStats = userStatistics.Select(x => x.Value).ToList();

         //Run different code depending on the command (duh)
         switch(command.Command)
         {
            case "mystatistics":
               //Always use the UID, NOT the username to identifiy users. Usernames can change.
               if (!userStatistics.ContainsKey(user.UID))
               {
                  //Here, we're setting the message that will be displayed for this chat message object.
                  moduleOutput.message = "You have no statistics yet. You will after this message!";
               }
               else
               {
                  moduleOutput.message = GetUserStats(user);
               }

               //We add just the one output to our list of outputs (we only want to output one thing)
               outputs.Add(moduleOutput);
               break;

            case "statistics":
               if (command.Arguments.Count == 0)
               {
                  moduleOutput.message = "---Global Chat Statistics---\n";
                  moduleOutput.message += "Total messages: " + allStats.Sum(x => x.TotalMessages) + "\n";
                  moduleOutput.message += "Average message size: " + (allStats.Count == 0 ? 0 : (int)(allStats.Sum(x => x.AverageMessageLength) / allStats.Count)) + " characters\n";
                  moduleOutput.message += "Total users seen: " + allStats.Count + "\n";
                  moduleOutput.message += "Total user chat time: " + StringExtensions.LargestTime(new TimeSpan(users.Sum(x => x.Value.TotalChatTime.Ticks))) + "\n";
                  moduleOutput.message += "Average session time: " + (users.Count == 0 ? "error" : StringExtensions.LargestTime(new TimeSpan(users.Sum(x => x.Value.AverageSessionTime.Ticks) / users.Count))) + "\n";
                  outputs.Add(moduleOutput);
               }
               else
               {
                  //THIS IS IMPORTANT! Arguments containing a user will be given to you as a USERNAME! However, you want
                  //to store their information based on their UID. This function "GetUserFromArgument()" will search through
                  //the given dictionary to find you the user with the given username. It should always succeed, and puts the
                  //result in the "out" parameter. You probably don't need to check the return of GetUserFromArgument like I
                  //do, but just to be save, if it fails, you can just fail with a generic error. AddError adds a generic
                  //module error to the given list of outputs, then you can return outputs and it'll contain that error.
                  UserInfo findUser;
                  if (!GetUserFromArgument(command.Arguments[0], users, out findUser))
                  {
                     AddError(outputs);
                     break;
                  }

                  moduleOutput.message = GetUserStats(findUser);
                  outputs.Add(moduleOutput);
               }
               break;
         }

         return outputs;
      }

      //Get stats for given user. This is a statistics-specific function
      public string GetUserStats(UserInfo user)
      {
         List<UserStatistics> allStats = userStatistics.Select(x => x.Value).ToList();
         UserStatistics myStats = userStatistics[user.UID];
         long totalMessages = allStats.Sum(x => x.TotalMessages);

         if (totalMessages == 0)
            totalMessages = 1;

         string message = "---" + user.Username + "'s Chat Statistics---\n";
         message += "Total messages: " + myStats.TotalMessages + 
            " (" + string.Format("{0:N2}%", myStats.TotalMessages * 100.0 / totalMessages) + 
            ", #" + (allStats.OrderByDescending(x => x.TotalMessages).ToList().IndexOf(myStats) + 1) + ")\n";
         message += "Average message size: " + (int)myStats.AverageMessageLength +
            " characters (#" + (allStats.OrderByDescending(x => x.AverageMessageLength).ToList().IndexOf(myStats) + 1) + ")\n";
         message += "Average users while chatting: " + (int)myStats.AverageUsersWhenChatting + "\n";
         message += "Total users you've seen: " + myStats.UniqueUsersSeen + "\n";
         message += "Total chat time: " + StringExtensions.LargestTime(user.TotalChatTime) + "\n";
         message += "Average session time: " + StringExtensions.LargestTime(user.AverageSessionTime) + "\n";

         return message;
      }
   }
}