using System;
using WebSocketSharp;
using WebSocketSharp.Server;

namespace ChatServer
{
   //This is the thing that will get passed as the... controller to Websockets?
   public class Chat : WebSocketBehavior
   {
      //I guess this is WHENEVER it receives a message?
      protected override void OnMessage(MessageEventArgs e)
      {

      }
   }
}
