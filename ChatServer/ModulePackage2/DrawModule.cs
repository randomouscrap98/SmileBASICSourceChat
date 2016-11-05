using System;
using ModuleSystem;
using System.Collections.Generic;
using ChatEssentials;

namespace ModulePackage2
{
   public class DrawModule: ModuleSystem.Module
   {
      private class DrawingInfo
      {
         public UserInfo User = null;
         public string Drawing = "";
         public DateTime postTime = new DateTime(0);
      }

      //private List<DrawingInfo> unsavedMessages = new List<DrawingInfo>();

      public DrawModule()
      {
         Commands.Add(new ModuleCommand("drawsubmit", new List<CommandArgument> {
            new CommandArgument("encodedDrawing", ArgumentType.Word)
         }, "Submit an encoded drawing.", false));
      }

      public override string Nickname
      {
         get { return "draw"; }
      }

//      public override bool SaveFiles()
//      {
//         if(MyExtensions.MySerialize.SaveObject<List
//      }

      public override List<JSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<JSONObject> outputs = new List<JSONObject>();

         if (command.Command == "drawsubmit")
         {
            //unsavedMessages.Add(new DrawingInfo() { Drawing = command.Arguments[0], User = user, postTime = DateTime.Now });
            UserMessageJSONObject drawingMessage = new UserMessageJSONObject(user, command.Arguments[0], command.tag);
            drawingMessage.encoding = Nickname;
            drawingMessage.spamValue = 0.50;
            drawingMessage.SetUnspammable();
            outputs.Add(drawingMessage);
         }

         return outputs; //base.ProcessCommand(command, user, users);
      }
   }
}

