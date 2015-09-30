using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using Newtonsoft.Json;

namespace MyExtensions
{
   public static class MySerialize
   {
      private static JsonSerializerSettings defaultSettings = new JsonSerializerSettings() 
      { 
         ContractResolver = new MyContractResolver(),
         Formatting = Formatting.Indented
      };

      //A quick and easy way to save objects to a file
      public static bool SaveObject<T>(string filename, T saveObject) where T : new()
      {
         try
         {
            string json = JsonConvert.SerializeObject(saveObject, defaultSettings);
            File.WriteAllText(filename, json);
         }
         catch
         {
            return false;
         }

         return true;
      }

      //A quick and easy way to load an object from a file
      public static bool LoadObject<T>(string filename, out T loadObject) where T : new()
      {
         loadObject = new T();

         try
         {
            string json = File.ReadAllText(filename);
            loadObject = JsonConvert.DeserializeObject<T>(json, defaultSettings);
         }
         catch //(Exception e)
         {
            return false;
         }

         return true;
      }
   }

   //Taken from http://stackoverflow.com/questions/24106986/json-net-force-serialization-of-all-private-fields-and-all-fields-in-sub-classe
   public class MyContractResolver : Newtonsoft.Json.Serialization.DefaultContractResolver
   {
      protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
      {
         var props = /*type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .Select(p => base.CreateProperty(p, memberSerialization))
            .Union(*/type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               .Select(f => base.CreateProperty(f, memberSerialization))//)
            .ToList();
         props.ForEach(p => { p.Writable = true; p.Readable = true; });
         return props;
      }
   }
}