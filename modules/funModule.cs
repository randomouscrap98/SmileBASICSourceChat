using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;
using System.Net;
using Newtonsoft.Json;

namespace ModulePackage1
{
   public class FunModule : Module
   {
      public FunModule()
      {
         Commands.Add(new ModuleCommand("me", new List<CommandArgument> { 
            new CommandArgument("message", ArgumentType.FullString)
         }, "be silly", true));
         Commands.Add(new ModuleCommand("code", new List<CommandArgument> { 
            new CommandArgument("message", ArgumentType.FullString)
         }, "output formatted code", true));
         Commands.Add(new ModuleCommand("img", new List<CommandArgument> { 
            new CommandArgument("link", ArgumentType.FullString)
         }, "output image directly (only users with the proper settings will see it", true));
         Commands.Add(new ModuleCommand("md", new List<CommandArgument> {
            new CommandArgument("text", ArgumentType.FullString)
         }, "output a markdown-formatted message", true));
         Commands.Add(new ModuleCommand("emotes", new List<CommandArgument>(), "See all available emotes"));

         AddOptions(new Dictionary<string, object>{{"emoteLink", "http://development.smilebasicsource.com/emotes.json"}});
      }

      public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();
         ModuleJSONObject moduleOutput;

         switch(command.Command)
         {
            case "me":
               moduleOutput = new ModuleJSONObject();
               //moduleOutput.broadcast = true;
               moduleOutput.sendtype = MessageBaseSendType.Broadcast;
               moduleOutput.message = user.Username + " " + command.Arguments[0]; //System.Security.SecurityElement.Escape(command.Arguments[0]);
               moduleOutput.tag = command.tag;
               outputs.Add(moduleOutput);
               break;

            case "code":
               MessageJSONObject codeMesssage = new MessageJSONObject(command.Arguments[0], user, command.tag);
               codeMesssage.encoding = "code";
               codeMesssage.spamvalue = 0.20;
               codeMesssage.SetSpammable(false);
               outputs.Add(codeMesssage);
               break;

            case "img":
               MessageJSONObject imageMessage = new MessageJSONObject(command.Arguments[0], user, command.tag);
               imageMessage.encoding = "image";
               imageMessage.spamvalue = 0.20;
               imageMessage.SetSpammable(false);
               outputs.Add(imageMessage);
               break;

            case "md":
               MessageJSONObject mdMessage = new MessageJSONObject(command.Arguments[0], user, command.tag);
               mdMessage.encoding = "markdown";
               mdMessage.spamvalue = 0.20;
               mdMessage.SetSpammable(false);
               outputs.Add(mdMessage);
               break;

            case "emotes":
               moduleOutput = new ModuleJSONObject();
               moduleOutput.message = "Emote list:\n";
             
               try
               {
                  string htmlCode;

                  using (WebClient client = new WebClient())
                  {
                     htmlCode = client.DownloadString(GetOption<string>("emoteLink"));
                  }

                  EmoteJSONObject emotes = JsonConvert.DeserializeObject<EmoteJSONObject>(htmlCode);

                  foreach(string emote in emotes.mapping.Keys)
                     moduleOutput.message += "\n" + emotes.format.Replace("emote", emote);
               }
               catch (Exception e)
               {
                  moduleOutput.message = "Sorry, could not retrieve emote list!\nException: " + e.ToString();
               }

               outputs.Add(moduleOutput);
               break;
         }

         return outputs;
      }

      //The format of the emote JSON object returned from the webserver
      public class EmoteJSONObject
      {
         public string format = "";
         public string location = "";
         public Dictionary<string, string> mapping = new Dictionary<string, string>();
      }
   }
}
