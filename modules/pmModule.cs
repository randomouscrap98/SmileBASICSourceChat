using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;

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

      public override List<JSONObject> ProcessCommand(UserCommand command, User user, Dictionary<string, User> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();
         ModuleJSONObject output = new ModuleJSONObject();

         switch (command.Command)
         {
            case "pm":
               output = new ModuleJSONObject();
               output.tag = "any";
               output.message = command.username + " -> " + command.Arguments[0] + ":\n" + command.Arguments[1];
               output.recipients.Add(command.Arguments[0]);
               output.recipients.Add(command.username);
               outputs.Add(output);
               break;
         }

         return outputs;
      }
   }
}
