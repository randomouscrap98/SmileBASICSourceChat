using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Linq;

namespace ChatServer
{
   public class PmModule : Module
   {
      public PmModule ()
      {
         Commands.AddRange(new List<ModuleCommand>() {
            new ModuleCommand("pm", new List<CommandArgument>{
               new CommandArgument("user", ArgumentType.User),
               new CommandArgument("message", ArgumentType.FullString)},
               "Send a personal message to user", true),
            new ModuleCommand("pmcreateroom", new List<CommandArgument>{
               new CommandArgument("userlist", ArgumentType.User, RepeatType.OneOrMore)
            }, "Create a room for the given users (you're always included)", true),
            new ModuleCommand("pmleaveroom", new List<CommandArgument>{
               /*new CommandArgument("room", ArgumentType.User, RepeatType.OneOrMore)*/
            }, "Leave a room (you must be in the room to leave it)")
         });
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject output = new ModuleJSONObject();
         string error = "";

         switch (command.Command)
         {
            case "pm":
               //First, make sure this even works
               UserInfo recipient;
               output = new ModuleJSONObject();

               if (!GetUserFromArgument(command.Arguments[0], 
                      users.Where(x => x.Value.LoggedIn).ToDictionary(x => x.Key, y => y.Value), out recipient))
               {
                  AddError(outputs);
                  break;
               }
               
               output.tag = "any";
               output.message = user.Username + " -> " + command.Arguments[0] + ":\n" + command.Arguments[1];
                  //System.Security.SecurityElement.Escape(command.Arguments[1]);
               output.recipients.Add(recipient.UID);
               output.recipients.Add(command.uid);
               outputs.Add(output);
               break;
            case "pmcreateroom":
               HashSet<int> roomUsers = new HashSet<int>();

               foreach (string roomUser in command.ArgumentParts[0])
               {
                  UserInfo tempUser;
                  if (!GetUserFromArgument(roomUser, users, out tempUser))
                  {
                     error = "User not found!";
                     break;
                  }
                  else if (!roomUsers.Add(tempUser.UID))
                  {
                     error = "Duplicate user in list: " + tempUser.Username;
                     break;
                  }
               }

               roomUsers.Add(user.UID);

               if (!string.IsNullOrWhiteSpace(error) || !ChatRunner.Server.CreatePMRoom(roomUsers, user.UID, out error))
               {
                  WarningJSONObject warning = new WarningJSONObject(error);
                  outputs.Add(warning);
               }
               else
               {
                  output.message = "You created a chatroom for " + string.Join(", ", roomUsers.Select(x => users[x].Username));
                  outputs.Add(output);
               }

               break;

            case "pmleaveroom":
               if (!ChatRunner.Server.LeavePMRoom(user.UID, command.tag, out error))
               {
                  WarningJSONObject warning = new WarningJSONObject(error);
                  outputs.Add(warning);
               }
               else
               {
                  output.message = "You left this PM room";
                  outputs.Add(output);
               }
               break;
         }

         return outputs;
      }
   }
}
