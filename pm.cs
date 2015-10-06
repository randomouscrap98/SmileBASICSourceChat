using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Text.RegularExpressions;

namespace ChatEssentials
{
   public class PMRoom
   {
      public const string NamePrepend = "room_";

      public readonly HashSet<int> Users;
      public readonly int Creator;
      public readonly string Name;
      public readonly TimeSpan ExpireTime;

      private DateTime lastMessageTime = DateTime.Now;

      private static long nextID = 1;
      public static readonly Object Lock = new Object();

      public PMRoom(HashSet<int> users, int creator, TimeSpan expireTimeout, string name = "")
      {
         lock (Lock)
         {
            if (string.IsNullOrWhiteSpace(name))
               name = NamePrepend + nextID;

            nextID++;
         }

         Users = users;
         Creator = creator;
         Name = name;
         ExpireTime = expireTimeout;
      }

      public bool HasExpired
      {
         get 
         {
            lock (Lock)
            {
               return (DateTime.Now - lastMessageTime) > ExpireTime; 
            }
         }
      }

      public long NextID
      {
         get
         { 
            lock (Lock)
            {
               return nextID; 
            }
         }
      }

      public static void FindNextID(List<string> roomNames)
      {
         lock (Lock)
         {
            foreach (string name in roomNames)
            {
               Match match = Regex.Match(name, NamePrepend + @"(\d+)");
               long newID;

               if (match.Success && long.TryParse(match.Groups[1].Value, out newID) && newID > nextID)
                  nextID = newID + 1;
            }
         }
      }

      public void OnMessage()
      {
         lock (Lock)
         {
            lastMessageTime = DateTime.Now;
         }
      }
   }
}