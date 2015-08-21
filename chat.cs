using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using WebSocketSharp;
using WebSocketSharp.Server;
using Newtonsoft.Json;

namespace ChatServer
{
   //This is the thing that will get passed as the... controller to Websockets?
   public class Chat : WebSocketBehavior
   {
      private string username = "";
      private static AuthServer authServer;
      private static readonly Object authLock = new Object();
      private static List<Message> messages = new List<Message>();
      private static readonly Object messageLock = new Object();
      private static HashSet<Chat> activeChatters = new HashSet<Chat>();
      private static readonly Object chatLock = new Object();
      
      //The username for this session
      public string Username
      {
         get { return username; }
      }

      //Assign an authentication server 
      public static void LinkAuthServer(AuthServer linked)
      {
         lock(authLock)
         {
            authServer = linked;
         }
      }

      //Check authorization with the given user.
      public bool CheckAuth(string key, string userCheck)
      {
         lock(authLock)
         {
            return authServer.GetAuth(userCheck) == key;
         }
      }

      //Check authorization for this user's session
      public bool CheckAuth(string key)
      {
         return CheckAuth(key, username);
      }

      public string GetUserList()
      {
         lock(chatLock)
         {

         }

         return "";
      }

      public string GetMessageList()
      {
         lock(messageLock)
         {
            
         }

         return "";
      }

      protected override void OnOpen()
      {
         //I don't know what we're doing for OnOpen yet.
      }

      //On closure of the websocket, remove ourselves from the list of active
      //chatters.
      protected override void OnClose(CloseEventArgs e)
      {
         lock(chatLock)
         {
            username = "";
            activeChatters.Remove(this);
            Sessions.Broadcast(GetUserList());
         }
      }

      //I guess this is WHENEVER it receives a message?
      protected override void OnMessage(MessageEventArgs e)
      {
         ResponseJSONObject response = new ResponseJSONObject();
         dynamic json = new Object();
         string type = "";

         //First, just try to parse the JSON they gave us. If it's absolute
         //garbage (or just not JSON), let them know and quit immediately.
         try
         {
            json = JsonConvert.DeserializeObject(e.Data);
            type = json.type;
            response.from = type;
         }
         catch
         {
            response.result = false;
            response.errors.Add("Could not parse JSON");
         }

         //If we got a bind message, let's try to authorize this channel.
         if(type == "bind")
         {
            try
            {
               //First, gather information from the JSON. THis is so that if
               //the json is invalid, it will fail as soon as possible
               string key = json.key;
               string newUser = json.username;

               //Oops, username was invalid
               if(string.IsNullOrWhiteSpace(newUser))
               {
                  response.result = true;
                  response.errors.Add("Username was invalid");
               }
               else
               {
                  //Oops, auth key was invalid
                  if(!CheckAuth(key, newUser))
                  {
                     response.result = false;
                     response.errors.Add("Key was invalid");
                  }
                  else
                  {
                     //All is well.
                     username = newUser;
                     response.result = true;
                     Console.WriteLine("Authenticated " + username + " for chat.");
                     Sessions.Broadcast(GetUserList());
                  }
               }
            }
            catch
            {
               response.result = false;
               response.errors.Add("BIND message was missing fields");
            }
         }
         else if (type == "message")
         {
            try
            {
               //First, gather information from the JSON. THis is so that if
               //the json is invalid, it will fail as soon as possible
               string key = json.key;
               string message = System.Security.SecurityElement.Escape(json.message);
               string tag = json.tag;

               //Authenticate the user. If not, just quit.
               if(!CheckAuth(key))
               {
                  response.result = false;
                  response.errors.Add("Your key is invalid");
               }
               else
               {
                  //User was authenticated (and we know the fields exist
                  //because we just pulled them). Just go ahead and add the
                  //message
                  lock(messageLock)
                  {
                     messages.Add(new Message(username, message, tag));
                     messages = messages.Skip(Math.Max(0, messages.Count() - 100))
                        .ToList();
                  }

                  Sessions.Broadcast(GetMessageList());
               }
            }
            catch
            {
               response.result = false;
               response.errors.Add("Message was missing fields");
            }

         }

         //Send the "OK" message back.
         Send(JsonConvert.SerializeObject(response));

         Sessions.Broadcast(e.Data);
         Console.WriteLine("Got message: " + e.Data);
      }
   }

   //Responses from the server should have this format.
   public class ResponseJSONObject
   {
      public readonly string type = "response"; 
      public string from = "unknown";
      public bool result = false;
      public List<string> errors = new List<string>();
   }

   //Sending out a list of users should follow this format
   public class UserListJSONObject
   {
      public readonly string type = "userList";
      public List<string> users = new List<string>();
   }

   public class MessageListJSONObject
   {
      public readonly string type = "messageList";
      public List<Message> messages = new List<Message>();
   }

   //A message. It SHOULD be readonly, honestly.
   public class Message
   {
      public readonly string username;
      public readonly string text;
      public readonly long id;
      public readonly string tag;
      private readonly DateTime postTime;
      private static long NextID = 0;

      public Message(string username, string message, string tag)
      {
         this.username = username;
         this.text = message;
         this.tag = tag;
         this.postTime = DateTime.Now;
         this.id = Interlocked.Increment(ref NextID);
      }

      //This is ONLY because we don't want to serialize it.
      public DateTime PostTime()
      {
         return postTime;
      }
   }
}
