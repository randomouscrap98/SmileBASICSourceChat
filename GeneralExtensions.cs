using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace MyExtensions
{
   public static class DateExtensions
   {
      public static readonly DateTime UnixEpoch = new DateTime(1970,1,1,0,0,0,0,System.DateTimeKind.Utc);

      //Convert a crappy unix time into a real DateTime object
      public static DateTime FromUnixTime( double unixTimeStamp )
      {
         return UnixEpoch.AddSeconds(unixTimeStamp).ToLocalTime();
      }

      //Convert a real DateTime object to unix time
      public static long ToUnixTime(DateTime realTime)
      {
         return (long)(Math.Floor((realTime - UnixEpoch).TotalSeconds));
      }

      //Taken from http://stackoverflow.com/questions/1300088/distinct-with-lambda
      //By: Jon Skeet (or MoreLINQ maybe)
      public static IEnumerable<TSource> DistinctBy<TSource, TKey>
         (this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
      {
         HashSet<TKey> knownKeys = new HashSet<TKey>();
         foreach (TSource element in source)
         {
            if (knownKeys.Add(keySelector(element)))
            {
               yield return element;
            }
         }
      }
   }

//   public class CustomLocking
//   {
//      public readonly int LockRetryWait;
//      public readonly int MaxRetries;
//
//      public CustomLocking(int maxRetries = 50, int lockRetryWait = 1)
//      {
//         LockRetryWait = lockRetryWait;
//         MaxRetries = maxRetries;
//      }
//
//      public bool TryLock(object lockObject)
//      {
//         int retries = 0;
//         do
//         {
//            if(Monitor.TryEnter(lockObject))
//               return true;
//         } while(retries < MaxRetries);
//
//         return false;
//      }
//      //public static bool TryLock(
//   }
}