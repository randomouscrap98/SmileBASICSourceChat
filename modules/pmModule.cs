using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Linq;

namespace ModulePackage1
{
   public class PmModule : Module
   {
      public PmModule ()
      {
         commands.AddRange(new List<ModuleCommand>() {
            new ModuleCommand("pm", new List<CommandArgument>{
               new CommandArgument("user", ArgumentType.User),
               new CommandArgument("message", ArgumentType.FullString)},
               "Send a personal message to user", true)
         });
      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject output = new ModuleJSONObject();

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
               output.recipients.Add(recipient.UID);
               output.recipients.Add(command.uid);
               outputs.Add(output);
               break;
         }

         return outputs;
      }
   }
}
