using System;
using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;

using System.IO;
using System.Reflection;

namespace CustomMonsters
{
    internal class Shooter
    {
        internal int ID;
        internal DateTime LastShot;

        internal Shooter(int id)
        {
            ID = id;
            LastShot = DateTime.Now;
        }
    }
}
