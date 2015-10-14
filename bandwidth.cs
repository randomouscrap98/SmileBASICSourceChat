using System;
using System.Collections.Generic;
using System.Linq;

namespace ChatServer
{
   //Just a tiny class for a small portion of the bandwidth
   [Serializable]
   public class BandwidthWindow
   {
      public readonly DateTime WindowBegin = DateTime.Now;
      public long Bytes = 0;

      public BandwidthWindow() { }

      //The copy constructor
      public BandwidthWindow(BandwidthWindow copy)
      {
         WindowBegin = copy.WindowBegin;
         Bytes = copy.Bytes;
      }

      public TimeSpan Age
      {
         get { return (DateTime.Now - WindowBegin); }
      }
   }

   //This adds editing abilities to the bandwidth container.
   [Serializable]
   public class BandwidthMonitor : BandwidthContainer
   {
      //Generic way to add bytes (internal only, adds bytes to the given array)
      private void AddBytes(int bytes, List<BandwidthWindow> windowBytes)
      {
         lock (byteLock)
         {
            //Merge minutely bandwidth. In the future, this may be less granular (like hours), but for now this is fine
            if (windowBytes.Count > 0 && windowBytes.Last().Age.TotalMinutes < 1)
               windowBytes.Last().Bytes += bytes;   
            else
               windowBytes.Add(new BandwidthWindow{ Bytes = bytes });
         }
      }

      public void AddOutgoing(int bytes)
      {
         AddBytes(bytes, outgoingBytes);
      }

      public void AddIncoming(int bytes)
      {
         AddBytes(bytes, incomingBytes);
      }
   }

   [Serializable]
   public class BandwidthContainer
   {
      public enum BytePowers
      {
         GB = 30,
         MB = 20,
         KB = 10,
         B = 0
      }

      protected List<BandwidthWindow> outgoingBytes = new List<BandwidthWindow>();
      protected List<BandwidthWindow> incomingBytes = new List<BandwidthWindow>();
      public readonly Object byteLock = new Object();

      public BandwidthContainer() { }

      //The copy constructor
      public BandwidthContainer(BandwidthContainer copy)
      {
         lock (byteLock)
         {
            foreach (BandwidthWindow window in copy.outgoingBytes)
               outgoingBytes.Add(new BandwidthWindow(window));
            foreach (BandwidthWindow window in copy.incomingBytes)
               incomingBytes.Add(new BandwidthWindow(window));
         }
      }

      //Generic, internal function to get bandwidth from any list (but of course it's only the internals)
      private long GetBandwidth(DateTime begin, List<BandwidthWindow> windowBytes)
      {
         lock (byteLock)
         {
            return windowBytes.Where(x => x.WindowBegin >= begin).Sum(x => x.Bytes);
         }
      }

      public long GetOutgoingBandwidth(DateTime begin)
      {
         return GetBandwidth(begin, outgoingBytes);
      }

      public long GetIncomingBandwidth(DateTime begin)
      {
         return GetBandwidth(begin, incomingBytes);
      }

      public string GetHourBandwidthOutgoing()
      {
         return BytesToString(GetOutgoingBandwidth(DateTime.Now.AddHours(-1)));
      }

      public string GetTotalBandwidthOutgoing()
      {
         return BytesToString(GetOutgoingBandwidth(new DateTime(0)));
      }

      public string GetHourBandwidthIncoming()
      {
         return BytesToString(GetIncomingBandwidth(DateTime.Now.AddHours(-1)));
      }

      public string GetTotalBandwidthIncoming()
      {
         return BytesToString(GetIncomingBandwidth(new DateTime(0)));
      }

      //Convert a big ol' byte count to something more readable
      public static string BytesToString(long bytes)
      {
         for (int i = 30; i >= 0; i -= 10)
         {
            double pow = Math.Pow(2, i);

            if (bytes >= pow)
               return String.Format("{0:0.##}", (double)bytes / pow) + Enum.GetName(typeof(BytePowers), (BytePowers)i);
         }

         return "ERROR";
      }
   }
}