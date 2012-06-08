using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using System.Drawing;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

using System.Text;
using Newtonsoft.Json;

namespace CustomMonsters
{
    public class CustomMonsterConfigFile
    {
        public int CustomSpawnRate = 600;
        public int MaxCustomSpawns = 5;

        public static CustomMonsterConfigFile Read(string path)
        {
            if (!File.Exists(path))
                return new CustomMonsterConfigFile();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                return Read(fs);
            }
        }

        public static CustomMonsterConfigFile Read(Stream stream)
        {
            using (var sr = new StreamReader(stream))
            {
                var cf = JsonConvert.DeserializeObject<CustomMonsterConfigFile>(sr.ReadToEnd());
                if (ConfigRead != null)
                    ConfigRead(cf);
                return cf;
            }
        }

        public void Write(string path)
        {
            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Write))
            {
                Write(fs);
            }
        }

        public void Write(Stream stream)
        {
            var str = JsonConvert.SerializeObject(this, Formatting.Indented);
            using (var sw = new StreamWriter(stream))
            {
                sw.Write(str);
            }
        }

        public static Action<CustomMonsterConfigFile> ConfigRead;
    }
}
