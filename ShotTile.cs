using System;
//using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using MySql.Data.MySqlClient;
using System.IO;

namespace CustomMonsters
{
    internal class ShotTile
    {
        internal float X { get; set; }
        internal float Y { get; set; }

        internal ShotTile(float x, float y)
        {
            X = x;
            Y = y;
        }
        
    }
}
