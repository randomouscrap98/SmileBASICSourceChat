using MyHelper;
using System;

public class ChatServer 
{
   public static void Main()
   {
      var thing = HTTPHelper.ParseHeader("whatever");
      Console.WriteLine("Dude, it ran");
   }
}
