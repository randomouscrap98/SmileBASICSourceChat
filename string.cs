using System;

namespace MyHelper
{
   public class StringHelper
   {
      //Convert byte array to hexadecimal string
      public static string ByteToHex(byte[] ba)
      {
         string hex = BitConverter.ToString(ba);
         return hex.Replace("-","");
      }
   }
}
