using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using System.Linq;
using Newtonsoft.Json;
using MyExtensions;

namespace ChatServer
{
   public class AuthServer : SimpleTCPPinger
   {
      public const string LocalAddress = "127.0.0.1";

      //Auth crap
      private RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
      private Dictionary<int, AuthData> authCodes = new Dictionary<int, AuthData>();
      private readonly Object authLock = new Object();

      public string AccessToken = null;

      //Set up the auth server with the given logger. Otherwise, log to an internal logger
      public AuthServer(int port, MyExtensions.Logging.Logger logger = null) : 
         base(IPAddress.Parse(LocalAddress), port, logger) {} 

      public override int ThreadSleepMilliseconds
      {
         get { return 100; }
      }

      protected override List<Action<byte[], NetworkStream>> MessageActions
      {
         get
         {
            return new List<Action<byte[], NetworkStream>>()
            {
               HandleAuthMessage
            };
         }
      }

      private void HandleAuthMessage(byte[] bytes, NetworkStream stream)
      {
         string data = System.Text.Encoding.UTF8.GetString(bytes);

         //Try to parse the json and send out the auth response
         dynamic json = JsonConvert.DeserializeObject(data);

         //If the client wants an auth code, send it.
         if(json.type == "auth")
         {
            if(!String.IsNullOrWhiteSpace(AccessToken) && AccessToken != (string)json.token)
            {
               Log("Received auth request without access token! Not sending auth key", 
                     MyExtensions.Logging.LogLevel.Warning);
               return;
            }

            string auth = RequestAuth((int)json.uid);
            byte[] response = System.Text.Encoding.ASCII.GetBytes(auth);
            stream.Write(response, 0, response.Length);

            Log("Sent authorization token " + auth + " for user " + json.uid);
         }
      }

      //Update the list of users who should have an authorization code
      public virtual void UpdateUserList(List<int> users)
      {
         lock(authLock)
         {
            //Start expiring any user codes that are no longer in the updated list
            foreach (int key in authCodes.Keys)
               if (!users.Contains(key))
                  authCodes[key].StartExpire(10);
               
            //Now remove any expired authcodes
            authCodes = authCodes.Where(
               x => !(x.Value.Expired && !users.Contains(x.Key))).ToDictionary(
               x => x.Key, x => x.Value);

            if (users.Except(authCodes.Keys).Count() > 0)
               Log("Someone attempted to add users to the authentication server", 
                  MyExtensions.Logging.LogLevel.Warning);

            //Next, add new users by simply adding those which were not already
            //in the dictionary.
//            foreach(int user in users.Except(authCodes.Keys))
//               RequestAuth(user); //Remember this automatically adds users 

            Log("Users with outstanding authentication codes: " + string.Join(", ", authCodes.Keys),
               MyExtensions.Logging.LogLevel.Debug);
         }
      }

      //The difference between requestauth and getauth is that getauth simply
      //returns the authentication code; requestauth actually generates an
      //authentication code for users that don't have one yet. 
      private string RequestAuth(int uid)
      {
         //If the uid isn't valid, just return bogus authkey
         if(uid <= 0)
            return GenerateAuth();

         lock(authLock)
         {
            //We need to generate a new auth for this user if they're not in
            //the auth list OR their old key is expired.
            if(!authCodes.ContainsKey(uid))
               authCodes.Add(uid, new AuthData(GenerateAuth()));

            //The auth key was requested, so stop expiring
            authCodes[uid].HaltExpire();

            //Now get the authentication
            return authCodes[uid].AuthKey; 
         }
      }

      //Get the authorization code for the given user.
      public virtual string GetAuth(int uid)
      {
         lock(authLock)
         {
            //Return some bogus authentication code if the username doesn't exist
            if(!authCodes.ContainsKey(uid))
               return GenerateAuth();
            else
               return authCodes[uid].AuthKey;
         }
      }

      public virtual bool CheckAuth(int uid, string key)
      {
         return GetAuth(uid) == key;  
      }

      //Generate an authentication key
      public string GenerateAuth()
      {
         byte[] randomBytes = new byte[8];
         random.GetBytes(randomBytes);
         return StringExtensions.ByteToHex(randomBytes);
      }
   }

   //The authorization key and expiration data. The key should not be removed
   //unless it is expired.
   public class AuthData
   {
      public const int ExpireMinutes = 5;
      public readonly string AuthKey;
      private DateTime expireDate = new DateTime(0);

      public AuthData(string key)
      {
         AuthKey = key;
         HaltExpire();
      }

      //Begin the expiration process
      public void StartExpire(int seconds = ExpireMinutes * 60)
      {
         if(expireDate.Ticks == 0)
            expireDate = DateTime.Now.AddSeconds(seconds);
      }

      //Stop the expiration process
      public void HaltExpire()
      {
         expireDate = new DateTime(0);
      }

      //When the auth data is expired, just throw it away immediately.
      public bool Expired
      {
         get 
         { 
            return expireDate.Ticks != 0 && DateTime.Now > expireDate; 
         }
      }

      public DateTime ExpiresOn
      {
         get { return expireDate; }
      }
   }

   //A fake authentication server. It literally does nothing.
   public class AuthServerFake : AuthServer
   {
      private bool running = false;

      public AuthServerFake(int port, MyExtensions.Logging.Logger logger = null) : base(port, logger){ }

      public override bool CheckAuth(int uid, string key)
      {
         return true;
      }

      public override string GetAuth(int uid)
      {
         return "Chickens";
      }

      public override bool Running
      {
         get { return running; }
      }

      public override bool Start()
      {
         Log("STARTING A FAKE AUTHENTICATION SERVER!!!");
         running = true;
         return true;
      }

      public override bool Stop()
      {
         running = false;
         return true;
      }

      public override void UpdateUserList(List<int> users)
      {
         //Do nothing because lol
      }
   }
}
