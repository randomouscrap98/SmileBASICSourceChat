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
   public class AuthServer
   {
      public readonly int Port;
      public const string Address = "127.0.0.1";
      public const int ThreadSleepMilliseconds = 10;
      public const int MaxWaitSeconds = 5;
      public const string LogTag = "Authentication";

      //Server crap
      private bool stop = false;
      private TcpListener server;
      private Thread authSpinner = null;

      //Error crap
      private MyExtensions.Logging.Logger logger = MyExtensions.Logging.Logger.DefaultLogger;

      //Auth crap
      private RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
      private Dictionary<int, AuthData> authCodes = new Dictionary<int, AuthData>();
      private readonly Object authLock = new Object();

      //Set up the auth server with the given logger. Otherwise, log to an internal logger
      public AuthServer(int port, MyExtensions.Logging.Logger logger = null)
      {
         Port = port;

         if (logger != null)
            this.logger = logger;
      }

      public void Log(string message, MyExtensions.Logging.LogLevel level = MyExtensions.Logging.LogLevel.Normal)
      {
         logger.LogGeneral(message, level, LogTag);
      }

      public void Error(string message)
      {
         Log(message, MyExtensions.Logging.LogLevel.Error);
      }

      //This should (hopefully) start the authorization server
      public virtual bool Start()
      {
         //Oops, we already have a spinner for authorization stuff
         if(authSpinner != null)
         {
            Error("Auth server already running");
            return false;
         }

         try
         {
            IPAddress localAddr = IPAddress.Parse(Address);
            server = new TcpListener(localAddr, Port);
            server.Start();

            stop = false;
            ThreadStart work = RunAuthServer;
            authSpinner = new Thread(work);
            authSpinner.Start();
         }
         catch(Exception e)
         {
            Error(e.ToString());
            return false;
         }

         return true;
      }

      public virtual bool Running
      {
         get { return authSpinner != null && authSpinner.IsAlive; }
      }

      //Try to stop the auth server
      public virtual bool Stop()
      {
         //We're already stopped.
         if(authSpinner == null)
            return true;

         //Well whatever. We should signal the stop.
         stop = true;

         //Wait for a bit to see if the thread will stop itself. If not, we
         //need to force its hand.
         if(!SpinWait())
         {
            Log("Authorization thread was not stopped when asked.", MyExtensions.Logging.LogLevel.Warning);

            //Try to force the thread to stop
            try
            {
               authSpinner.Abort();

               //Oops, even with aborting, the thread would not yield
               if(!SpinWait())
                  Error("Authorization thread could not be forcibly stopped.");
            }
            catch (Exception e)
            {
               //Wow, aborting threw an exception. Yuck
               Error("Aborting authorization thread threw exception: " + e.ToString());
            }
         }

         //Deallocate thread if it's finally dead.
         if(!authSpinner.IsAlive)
         {
            server.Stop();
            authSpinner = null;
            return true;
         }
         else
         {
            return false;
         }
      }

      //Wait for a bit (5 seconds) on the authorization thread. If it's still
      //running, return false.
      private bool SpinWait()
      {
         double waitTime = 0;

         //Do this for a bit while we wait for whatever the thread thinks it
         //needs to finish before actually finishing
         while(authSpinner.IsAlive && waitTime < MaxWaitSeconds)
         {
            Thread.Sleep(ThreadSleepMilliseconds);
            waitTime += ThreadSleepMilliseconds / 1000.0;
         }

         return !authSpinner.IsAlive;
      }

      //This should be run on a thread.
      private void RunAuthServer()
      {
         byte[] bytes = new byte[1024];
         string data = "";

         //Keep going until someone told us to stop
         while(!stop)
         {
            //If there's a pending connection, let's service it. It shouldn't
            //really take a lot of time, so no need to spawn a new thread...
            //hopefully. If it turns out to be a problem, I'll fix it.
            if(server.Pending())
            {
               TcpClient client = null;

               try
               {
                  //Get the client and get a stream for them
                  client = server.AcceptTcpClient();
                  NetworkStream stream = client.GetStream();

                  int i = 0;
                  double wait = 0.0;
                  data = "";

                  //First, wait for data to become available or a timeout,
                  //whatever comes first.
                  while(!stream.DataAvailable)
                  {
                     Thread.Sleep(ThreadSleepMilliseconds);
                     wait += ThreadSleepMilliseconds / 1000.0;

                     //Oops, we waited too long for a response
                     if(wait > MaxWaitSeconds)
                     {
                        throw new Exception("Read timeout reached (" + MaxWaitSeconds + " sec)");
                     }
                  }

                  //Keep reading until there's nothing left
                  while(stream.DataAvailable)
                  {
                     i = stream.Read(bytes, 0, bytes.Length);
                     data += System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                  }

                  //Try to parse the json and send out the auth response
                  dynamic json = JsonConvert.DeserializeObject(data);

                  //If the client wants an auth code, send it.
                  if(json.type == "auth")
                  {
                     string auth = RequestAuth((int)json.uid);
                     byte[] response = System.Text.Encoding.ASCII.GetBytes(auth);
                     stream.Write(response, 0, response.Length);

                     Log("Sent authorization token " + auth + " for user " + json.uid);
                  }

                  client.Close();
               }
               catch (Exception e)
               {
                  Error("Auth server error: " + e.ToString());
               }
               finally
               {
                  //Now just stop. We're done after the response.
                  if(client != null)
                     client.Close();
               }
            } 

            Thread.Sleep(ThreadSleepMilliseconds);
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
            //Oops, the user code expired. Just remove it
//            if(authCodes.ContainsKey(username) && authCodes[username].Expired)
//               authCodes.Remove(username);

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
