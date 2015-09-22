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

      //Server crap
      private bool stop = false;
      private TcpListener server;
      private Thread authSpinner = null;

      //Error crap
      private MyExtensions.Logging.Logger logger = MyExtensions.Logging.Logger.DefaultLogger;

      //Auth crap
      private RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
      private Dictionary<string, AuthData> authCodes = new Dictionary<string, AuthData>();
      private readonly Object authLock = new Object();

      //Set up the auth server with the given logger. Otherwise, log to an internal logger
      public AuthServer(int port, MyExtensions.Logging.Logger logger = null)
      {
         Port = port;

         if (logger != null)
            this.logger = logger;
      }

      //This should (hopefully) start the authorization server
      public bool Start()
      {
         //Oops, we already have a spinner for authorization stuff
         if(authSpinner != null)
         {
            logger.Error("Auth server already running");
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
            logger.Error(e.ToString());
            return false;
         }

         return true;
      }

      //Try to stop the auth server
      public bool Stop()
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
            logger.Error("Authorization thread was not stopped when asked.");

            //Try to force the thread to stop
            try
            {
               authSpinner.Abort();

               //Oops, even with aborting, the thread would not yield
               if(!SpinWait())
                  logger.Error("Authorization thread could not be forcibly stopped.");
            }
            catch (Exception e)
            {
               //Wow, aborting threw an exception. Yuck
               logger.Error("Aborting authorization thread threw exception: " + e.ToString());
            }
         }

         //Deallocate thread if it's finally dead.
         if(!authSpinner.IsAlive)
         {
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

                     //Oops, we waited to long for a response
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
                     byte[] response = System.Text.Encoding.ASCII.GetBytes(
                           RequestAuth((string)json.username));
                     stream.Write(response, 0, response.Length);

                     logger.Log("Sent authorization token for user: " + json.username);
                  }
               }
               catch (Exception e)
               {
                  logger.Error("Auth server error: " + e.ToString());
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
      public void UpdateUserList(List<string> users)
      {
         lock(authLock)
         {
            //First, remove old users by removing the ones which are expired
            //and no longer in the list of users.
            authCodes = authCodes.Where(x => !(x.Value.Expired && 
                  !users.Contains(x.Key))).ToDictionary(x => x.Key, 
                  x => x.Value);
            
            //Next, add new users by simply adding those which were not already
            //in the dictionary.
            foreach(string user in users.Except(authCodes.Keys))
               RequestAuth(user); //Remember this automatically adds users 

            logger.Log("Users with outstanding authentication codes: " + 
                  string.Join(", ", authCodes.Keys));
         }
      }

      //The difference between requestauth and getauth is that getauth simply
      //returns the authentication code; requestauth actually generates an
      //authentication code for users that don't have one yet. 
      private string RequestAuth(string username)
      {
         //If the username doesn't contain any characters, just return bogus
         if(string.IsNullOrWhiteSpace(username))
            return GenerateAuth();

         lock(authLock)
         {
            //Oops, the user code expired. Just remove it
//            if(authCodes.ContainsKey(username) && authCodes[username].Expired)
//               authCodes.Remove(username);

            //We need to generate a new auth for this user if they're not in
            //the auth list OR their old key is expired.
            if(!authCodes.ContainsKey(username))
               authCodes.Add(username, new AuthData(GenerateAuth()));

            //Now get the authentication
            return authCodes[username].AuthKey; 
         }
      }

      //Get the authorization code for the given user.
      public string GetAuth(string username)
      {
         lock(authLock)
         {
            //Return some bogus authentication code if the username doesn't exist
            if(!authCodes.ContainsKey(username))
               return GenerateAuth();
            else
               return authCodes[username].AuthKey;
         }
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
      public readonly DateTime Expires;

      public AuthData(string key)
      {
         AuthKey = key;
         Expires = DateTime.Now.AddMinutes(ExpireMinutes);
      }

      public bool Expired
      {
         get { return DateTime.Now > Expires; }
      }
   }
}
