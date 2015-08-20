using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using MyHelper;

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
      private bool consolePrint;
      private List<string> errors = new List<string>();
      private List<string> outputs = new List<string>();

      //Auth crap
      private RNGCryptoServiceProvider random = new RNGCryptoServiceProvider();
      private Dictionary<string, string> authCodes = new Dictionary<string, string>();
      private Object authLock = new Object();

      public AuthServer(int port, bool shouldPrintErrors = false)
      {
         Port = port;
         consolePrint = shouldPrintErrors;
      }

      //This should (hopefully) start the authorization server
      public bool Start()
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
            Error("Authorization thread was not stopped when asked.");

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
               Error("Aborting authorization thread threw exception: " +
                     e.ToString());
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

                  Output("Got an authorization client");

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
                        throw new Exception("Read timeout reached (" +
                              MaxWaitSeconds + " sec)");
                     }
                  }

                  //Keep reading until there's nothing left
                  while((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                     data += System.Text.Encoding.ASCII.GetString(bytes, 0, i);

                  Output("Read data from authorization client");

                  //Try to parse the json and send out the auth response
                  dynamic json = JsonConvert.DeserializeObject(data);

                  //If the client wants an auth code, send it.
                  if(json.type == "auth")
                  {
                     byte[] response = System.Text.Encoding.ASCII.GetBytes(
                           GetAuth(json.username));
                     stream.Write(response, 0, response.Length);

                     Output("Sent data to authorization client");
                  }
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
                  Output("Closed connection with authorization client");
               }
            } 

            Thread.Sleep(ThreadSleepMilliseconds);
         }
      }

      //Call this function as an alternative to printing to STDOUT
      private void Output(string output)
      {
         if(consolePrint)
            Console.WriteLine(output);
         else
            outputs.Add(output);
      }

      //Call this function as an alternative to print to STDERR
      private void Error(string error)
      {
         if(consolePrint)
            Console.WriteLine(error);
         else
            errors.Add(error);
      }

      //Update the list of users who should have an authorization code
      public void UpdateUserList(List<string> users)
      {
         lock(authLock)
         {
            //Create a new dictionary for authorization codes.
            Dictionary<string, string> newCodes = new Dictionary<string, string>();

            //Fill up the new dictionary with the old authorization values. If
            //any user no longer exists in the given list, they will not be
            //added to the new dictionary.
            foreach(string user in users)
               newCodes.Add(user, GetAuth(user));

            //The final step to victory
            authCodes = newCodes;
         }
      }

      //Get the authorization code for the given user. Alternatively, generate
      //a new one if one doesn't already exist.
      public string GetAuth(string username)
      {
         lock(authLock)
         {
            if(authCodes.ContainsKey(username))
            {
               return authCodes[username];
            }
            else
            {
               //We need to generate a new auth for this user.
               byte[] randomBytes = new byte[8];
               random.GetBytes(randomBytes);
               authCodes.Add(username, StringHelper.ByteToHex(randomBytes));

               return authCodes[username];
            }
         }
      }

      //Give the user a list of errors (a NEW list so it can't be altered)
      public List<string> RetrieveErrors()
      {
         List<string> retrievedErrors = new List<string>(errors);
         errors.Clear();
         return retrievedErrors;
      }
   }
}
