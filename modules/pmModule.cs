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
            new ModuleCommand("pmcreate", new List<CommandArgument>{
               new CommandArgument("userlist", ArgumentType.User, RepeatType.OneOrMore)
            }, "Create a room for the given users (you're always included)", true),
            new ModuleCommand("pmleave", new List<CommandArgument>{
               /*new CommandArgument("room", ArgumentType.User, RepeatType.OneOrMore)*/
            }, "Leave a room (you must be in the room to leave it)"),
            new ModuleCommand("pmlist", new List<CommandArgument>{
               /*new CommandArgument("room", ArgumentType.User, RepeatType.OneOrMore)*/
            }, "List users in the current room")
         });
      }

      public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();
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
               output.sendtype = MessageBaseSendType.OnlyRecipients;
               output.recipients.Add(recipient.UID);
               output.recipients.Add(command.sender.uid);
               outputs.Add(output);
               break;
            case "pmcreate":
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
                  WarningMessageJSONObject warning = new WarningMessageJSONObject(error);
                  outputs.Add(warning);
               }
               else
               {
                  output.message = "You created a chatroom for " + string.Join(", ", roomUsers.Select(x => users[x].Username));
                  outputs.Add(output);
               }

               break;

            case "pmleave":
               if (!ChatRunner.Server.LeavePMRoom(user.UID, command.tag, out error))
               {
                  WarningMessageJSONObject warning = new WarningMessageJSONObject(error);
                  outputs.Add(warning);
               }
               else
               {
                  output.message = "You left this PM room";
                  outputs.Add(output);
               }
               break;

            case "pmlist":
               List<UserInfo> pmUsers = ChatRunner.Server.UsersInPMRoom(command.tag);

               if(pmUsers.Count == 0)
               {
                  output.message = "You're not in a PM room!";
               }
               else
               {
                  output.message = "Users in " + command.tag + ": " + 
                     String.Join(", ", pmUsers.Select(x => x.Username));
               }

               outputs.Add(output);
               break;
         }

         return outputs;
      }
   }
}
