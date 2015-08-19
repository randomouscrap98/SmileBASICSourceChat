using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Security.Cryptography;

//MyHelper is a namespace where I put all my "helper" classes.
namespace MyHelper
{
   //A class to help with HTTP stuff.
   public class HTTPHelper 
   {
      //This will break the header into a Dictionary of key-value pairs
      public static Dictionary<string, string> ParseHeader(string header)
      {
         List<string> lines = header.Replace("\r", "")
            .Split('\n').Select(x => x.Trim()).ToList();

         Dictionary<string, string> headerParts = new Dictionary<string, string>();

         //Go through each line of the header
         foreach(string line in lines)
         {
            //Try to match against the standard header field format
            Match match = Regex.Match(line, @"^([^:]+):(.+)$");

            //If we got it, add the pair to the dictionary
            if(match.Success)
               headerParts.Add(match.Groups[1].Value, match.Groups[2].Value.Trim());
         }

         return headerParts;
      }
   }

   //Crap to help with websockets
   public class WebsocketHelper
   {
      //This is the "websocket" identifier. It's the same for all websockets
      public const string GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

      //Gets the handshake key you should return when performing the websocket
      //handshake. Just plop this in a header and you should be good...
      public static string HandshakeKey(string serverKey)
      {
         //Some stupid thing websockets wants us to do
         string newKey = serverKey + GUID;

         //"Cryptography" (yeah whatever)
         SHA1 sha1 = SHA1CryptoServiceProvider.Create();
         byte[] hashBytes = sha1.ComputeHash(
               System.Text.Encoding.ASCII.GetBytes(newKey));

         return Convert.ToBase64String(hashBytes);
      }
   }
}
