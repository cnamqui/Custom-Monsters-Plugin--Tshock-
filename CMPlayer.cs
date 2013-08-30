using System;
using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

using System.IO;

namespace CustomMonsters
{
    internal class CMPlayer
    {
        public int Index { get; set; }
        public TSPlayer TSPlayer { get { return TShock.Players[Index]; } }
        public List<int> NPCIDs { get; set; }
        public DateTime LastCustomZoneSpawn { get; set; }
        public CMPlayer(int index)
        {
            Index = index;
            NPCIDs = new List<int>();
            LastCustomZoneSpawn = DateTime.Now;
        }
    }
}
