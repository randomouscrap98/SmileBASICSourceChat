using System;
using System.Net;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Text.RegularExpressions;
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
      private static Dictionary<string, User> users = new Dictionary<string, User>();
      private static readonly Object userLock = new Object();
      
      //The username for this session
      public string Username
      {
         get { return username; }
      }

      public User ThisUser
      {
         get
         {
            lock (userLock) 
            {
               if (!users.ContainsKey (username) && !string.IsNullOrWhiteSpace (username))
                  users.Add (username, new User (username));

               return users [username];
            }
         }
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

      //Update the authenticated users. You should do this on chat join and
      //leave, although it may be unnecessary for joins.
      public void UpdateAuthUsers()
      {
         lock(authLock)
         {
            authServer.UpdateUserList(GetUsers());
         }
      }

      //Get JUST a list of users currently in chat (useful for auth)
      public List<string> GetUsers()
      {
         lock(chatLock)
         {
            return activeChatters.Select(x => x.Username).ToList();    
         }
      }

      //Get a JSON string representing a list of users currently in chat
      public string GetUserList()
      {
         UserListJSONObject userList = new UserListJSONObject();

         userList.users = GetUsers().Select(x => new UserJSONObject(x, true)).ToList();

         return JsonConvert.SerializeObject(userList);
      }

      //Get a JSON string representing a list of the last 10 messages
      public string GetMessageList()
      {
         MessageListJSONObject jsonMessages = new MessageListJSONObject();

         lock(messageLock)
         {
            for(int i = 0; i < Math.Min(10, messages.Count); i++)
               jsonMessages.messages.Add(messages[messages.Count - 1 - i]);

            //Oops, remember we added them in reverse order. Fix that
            jsonMessages.messages.Reverse();
         }

         return JsonConvert.SerializeObject(jsonMessages);
      }

      protected override void OnOpen()
      {
         //I don't know what we're doing for OnOpen yet.
      }

      //On closure of the websocket, remove ourselves from the list of active
      //chatters.
      protected override void OnClose(CloseEventArgs e)
      {
         Console.WriteLine("Session disconnect: " + username);

         lock(chatLock)
         {
            username = "";
            activeChatters.Remove(this);
            Sessions.Broadcast(GetUserList());
         }
         UpdateAuthUsers();
      }

      //I guess this is WHENEVER it receives a message?
      protected override void OnMessage(MessageEventArgs e)
      {
         ResponseJSONObject response = new ResponseJSONObject();
         response.result = false;
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
                  response.errors.Add("Username was invalid");
               }
               else
               {
                  //Oops, auth key was invalid
                  if(!CheckAuth(key, newUser))
                  {
                     response.errors.Add("Key was invalid");
                  }
                  else
                  {
                     //Before we do anything, remove other chatting sessions
                     List<Chat> removals = activeChatters.Where(
                           x => x.Username == newUser).Distinct().ToList();

                     foreach(Chat removeChat in removals)
                        Sessions.CloseSession(removeChat.ID);
                     
                     //All is well.
                     username = newUser;
                     activeChatters.Add(this);
                     response.result = true;
                     Console.WriteLine("Authenticated " + username + " for chat.");
                     Sessions.Broadcast(GetUserList());
                     UpdateAuthUsers();
                  }
               }
            }
            catch
            {
               response.errors.Add("BIND message was missing fields");
            }
         }
         else if (type == "message")
         {
            try
            {
               //First, gather information from the JSON. This is so that if
               //the json is invalid, it will fail as soon as possible
               string key = json.key;
               string message = System.Security.SecurityElement.Escape((string)json.text);
               string tag = json.tag;

               //Authenticate the user. If not, just quit.
               if(string.IsNullOrWhiteSpace(message))
               {
                  response.errors.Add("No empty messages please");
               }
               else if(!CheckAuth(key))
               {
                  response.errors.Add("Your key is invalid");
               }
               else if (ThisUser.BlockedUntil >= DateTime.Now)
               {
                  response.errors.Add("You are blocked for " + 
                     ThisUser.SecondsToUnblock + " second(s) for spamming");
               }
               else if(Regex.IsMatch(message, @"^\s*/spamscore\s*$"))
               {
                  WarningJSONObject tempWarning = new WarningJSONObject();
                  tempWarning.warning = "This is a temporary debug feature.  " +
                     "Your spam score is: " + ThisUser.SpamScore + 
                     ", badness score: " + ThisUser.GlobalSpamScore;
                  Send(tempWarning.ToString()); 
               }
               else
               {
                  WarningJSONObject warning;

                  //User was authenticated (and we know the fields exist
                  //because we just pulled them). Just go ahead and add the
                  //message
                  lock(messageLock)
                  {
                     warning = ThisUser.UpdateSpam(messages, message);

                     if(ThisUser.BlockedUntil < DateTime.Now)
                     {
                        messages.Add(new Message(username, message, tag));
                        messages = messages.Skip(Math.Max(0, messages.Count() - 1000)).ToList();
                        ThisUser.PerformOnPost();
                        response.result = true;
                     }
                  }

                  //Since we added a new message, we need to broadcast.
                  if(response.result)
                     Sessions.Broadcast(GetMessageList());

                  //Send a warning if you got one.
                  if(warning != null)
                     Send(warning.ToString());
               }
            }
            catch
            {
               response.errors.Add("Message was missing fields");
            }
         }
         else if (type == "request")
         {
            try
            {
               string wanted = json.request;

               if(wanted == "userList")
               {
                  Send(GetUserList());
                  response.result = true;
               }
               else if (wanted == "messageList")
               {
                  Send(GetMessageList());
                  response.result = true;
               }
               else
               {
                  response.errors.Add("Invalid request field");
               }
            }
            catch
            {
               response.errors.Add("Request was missing fields");
            }
         }

         //Send the "OK" message back.
         Send(response.ToString());

         Console.WriteLine("Got message: " + e.Data);
      }
   }

   public abstract class JSONObject
   {
      public readonly string type;

      public JSONObject(string type)
      {
         this.type = type;
      }

      public override string ToString ()
      {
         return JsonConvert.SerializeObject(this);
      }
   }

   //Responses from the server should have this format.
   public class ResponseJSONObject : JSONObject
   {
      public ResponseJSONObject() : base("response") {}
      public string from = "unknown";
      public bool result = false;
      public List<string> errors = new List<string>();
   }

   public class WarningJSONObject : JSONObject
   {
      public WarningJSONObject() : base("warning") {}
      public string warning = "";
   }

   //The list of messages sent out in JSON should follow this format
   public class MessageListJSONObject : JSONObject
   {
      public MessageListJSONObject() : base("messageList") {}
      public List<Message> messages = new List<Message>();
   }

   //Sending out a list of users should follow this format
   public class UserListJSONObject : JSONObject
   {
      public UserListJSONObject() : base("userList") {}
      public List<UserJSONObject> users = new List<UserJSONObject>();
   }

   //A single user within the UserList JSON object
   public class UserJSONObject
   {
      public readonly string username = "";
      public bool active = false;

      public UserJSONObject(string username, bool active)
      {
         this.username = username;
         this.active = active;
      }
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

      public string time
      {
         get { return postTime.ToString() + " UTC"; }
      }

      //This is ONLY because we don't want to serialize it.
      public DateTime PostTime()
      {
         return postTime;
      }
   }
}
