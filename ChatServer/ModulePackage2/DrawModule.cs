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

      public override List<MessageBaseJSONObject> ProcessCommand(UserCommand command, UserInfo user, Dictionary<int, UserInfo> users)
      {
         List<MessageBaseJSONObject> outputs = new List<MessageBaseJSONObject>();

         if (command.Command == "drawsubmit")
         {
            //unsavedMessages.Add(new DrawingInfo() { Drawing = command.Arguments[0], User = user, postTime = DateTime.Now });
            MessageJSONObject drawingMessage = new MessageJSONObject(command.Arguments[0], user, command.tag);
            drawingMessage.encoding = Nickname;
            drawingMessage.spamvalue = 0.50;
            drawingMessage.SetSpammable(false);
            outputs.Add(drawingMessage);
         }

         return outputs; //base.ProcessCommand(command, user, users);
      }
   }
}

