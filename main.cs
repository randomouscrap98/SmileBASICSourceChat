using MyHelper;
using System;

public class ChatServer 
{
   public static void Main()
   {
      var thing = HTTPHelper.ParseHeader("whatever: thing\nmorewhatever: otherthing");
      
      foreach(var pair in thing)
         Console.WriteLine(pair.Key + " had value " + pair.Value);

      Console.WriteLine("Dude, it ran");
   }
}
