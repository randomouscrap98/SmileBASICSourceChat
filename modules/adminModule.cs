using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Text.RegularExpressions;
using System.Net;
using System.Linq;
using System.Text;
using Newtonsoft.Json;

namespace ChatServer
{
   public class SneakyModule : Module
   {
      //public Dictionary<int, 
      public SneakyModule()
      {
         Commands.Add(new ModuleCommand("uptonogood", new List<CommandArgument>(), "Toggle the hidden state", true));
      }

      /*public override bool Hidden(UserInfo user)
      {
         return !(user.ChatControl || user.ChatControlExtended);
      }*/

      public static void UnhideUser(int uid)
      {
         User thisRealUser = ChatRunner.Server.GetUser(uid);
         thisRealUser.Hiding = false;
         thisRealUser.LastJoin = DateTime.Now;
         ChatRunner.Server.BroadcastUserList();
         ChatRunner.Server.Broadcast(new LanguageTagParameters(ChatTags.Join, thisRealUser), 
               new SystemMessageJSONObject());
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         User thisRealUser = ChatRunner.Server.GetUser(user.UID);

         if (command.Command == "uptonogood")
         {
            /*if (!user.ChatControl)
               return FastMessage("This command doesn't *AHEM* exist");*/

            thisRealUser.Hiding = !thisRealUser.Hiding;

            if (thisRealUser.Hiding)
            {
               ChatRunner.Server.BroadcastUserList();
               ChatRunner.Server.Broadcast(new LanguageTagParameters(ChatTags.Leave, thisRealUser), new SystemMessageJSONObject());

               return FastMessage("You're now hiding. Hiding persists across reloads. Be careful, you can still use commands!");
            }
            else
            {
               UnhideUser(user.UID);
               /*thisRealUser.LastJoin = DateTime.Now;
               ChatRunner.Server.BroadcastUserList();
               ChatRunner.Server.Broadcast(new LanguageTagParameters(ChatTags.Join, thisRealUser), new SystemMessageJSONObject());*/

               return FastMessage("You've come out of hiding.");
            }
         }

         return new List<JSONObject>();
      }
   }

   public class AdminModule : Module
   {
      //public Dictionary<int, 
      public AdminModule()
      {
         Commands.Add(new ModuleCommand("showhiding", new List<CommandArgument>(), "See sneaks"));
         Commands.Add(new ModuleCommand("badmin", new List<CommandArgument>() {
            new CommandArgument("message", ArgumentType.FullString) 
         }, "Output direct messages with no safety precautions"));
         Commands.Add(new ModuleCommand("expose", new List<CommandArgument>() { 
            new CommandArgument("user", ArgumentType.User) }, "Kick a user out from hiding"));
         //Commands.Add(new ModuleCommand("expose", new List<CommandArgument>(), "See sneaks", true));
      }

      //Ugh hidden returns false IF the user CAN'T view it.
      public override bool Hidden(UserInfo user)
      {
         return !(user.ChatControl || user.ChatControlExtended);
      }

      public List<User> GetHidingUsers(Dictionary<int, UserInfo> users)
      {
         List<User> hidingUsers = new List<User>();

         foreach(User hidingUser in users.Select(
                  x => ChatRunner.Server.GetUser(x.Value.UID)).Where(x => x.Hiding).ToList())
         {
            hidingUser.Hiding = false;

            if (hidingUser.OpenSessionCount > 0)
               hidingUsers.Add(hidingUser);

            hidingUser.Hiding = true;
         }

         return hidingUsers;
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         string output = "";
         UserInfo parsedUser = null;

         if (command.Command == "showhiding")
         {
            if (!user.ChatControlExtended)
               return FastMessage("This command doesn't *AHEM* exist");

            output = "Users that are hiding:\n" + String.Join("\n", GetHidingUsers(users).Select(x => x.Username));

            return FastMessage(output);
         }
         else if (command.Command == "expose")
         {
            if (!user.ChatControlExtended)
               return FastMessage("This command doesn't *AHEM* exist");

            //This should eventually use AddError instead.
            if(!GetUserFromArgument(command.Arguments[0], users, out parsedUser))
               return FastMessage("Something weird happened in the backend during user parse!", true); 
            else if (!GetHidingUsers(users).Any(x => x.UID == parsedUser.UID))
               return FastMessage(parsedUser.Username + " isn't hiding!", true);

            SneakyModule.UnhideUser(parsedUser.UID);
            return FastMessage("You forced " + parsedUser.Username + " out of hiding!");
         }
         else if (command.Command == "badmin")
         {
            if (!user.ChatControlExtended)
               return FastMessage("This command doesn't *AHEM* exist");

            UserMessageJSONObject directMessage = 
               new UserMessageJSONObject(user, command.Arguments[0], command.tag);
            directMessage.encoding = "raw";

            return new List<JSONObject>() {directMessage};
         }

         return new List<JSONObject>();
      }
   }
   /*public class AdminModule : Module
   {
      public AdminModule()
      {
         //AddOptions(new Dictionary<string, object>{ {"enabled", true} });

         commands.Add(new ModuleCommand("ban", new List<CommandArgument> { 
            new CommandArgument("user", ArgumentType.User),
            new CommandArgument("time", ArgumentType.Custom, @"[0-9]+[hd]")
         }, "Ban a user for the given time (ex: /ban ?random 5h)"));
         commands.Add(new ModuleCommand("unban", new List<CommandArgument> { 
            new CommandArgument("user", ArgumentType.User)
         }, "Remove a user's ban"));
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<int, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject moduleOutput;

         switch(command.Command)
         {
            case "ban":
               moduleOutput = new ModuleJSONObject();
               int hours = 0;

               Match match = Regex.Match(command.Arguments[1], @"([0-9]+)([hd])");

               if (!match.Success)
               {
                  moduleOutput.message = "An internal error occurred: time regex did not match command regex";
                  outputs.Add(moduleOutput);
                  break;
               }
               else if (!int.TryParse(match.Groups[1].Value, out hours))
               {
                  moduleOutput.message = "An internal error occurred: time value too big";
                  outputs.Add(moduleOutput);
                  break;
               }

               hours *= (match.Groups[2].Value == "d" ? 24 : 1);

               string banError;

               if (!TryBanQuery(new Dictionary<string, string> {
                  { "username", command.uid },
                  { "time", hours.ToString() } }, out banError))
               {
                  moduleOutput.message = banError;
                  outputs.Add(moduleOutput);
                  break;
               }

               moduleOutput.message = command.uid + " " + command.Arguments[0];
               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }

      private bool TryBanQuery(Dictionary<string, string> queryParameters, out string error)
      {
         string banResult = "";
         error = "";
         bool result = false;

         using (WebClient client = new WebClient())
         {
            System.Collections.Specialized.NameValueCollection reqparm = new System.Collections.Specialized.NameValueCollection();

            foreach(var queryParameter in queryParameters)
               reqparm.Add(queryParameter.Key, queryParameter.Value);

            byte[] responsebytes = client.UploadValues("http://development.smilebasicsource.com/query/ban.php", "POST", reqparm);
            banResult = Encoding.UTF8.GetString(responsebytes);
         }

         try
         {
            dynamic json = JsonConvert.DeserializeObject(banResult);
            result = json.result;

            foreach(string jsonError in json.errors)
               error += "Ban Error: " + jsonError + "\n";
         }
         catch
         {
            error = "An internal error has occurred: The ban page returned an invalid object";
            return false;
         }

         return result;
      }
   }*/
}
