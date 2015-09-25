using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

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
   }
}