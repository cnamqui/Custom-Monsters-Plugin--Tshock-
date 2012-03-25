using System;
using System.Collections.Generic;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using MySql.Data.MySqlClient;
using System.IO;

namespace CustomMonsters
{
    internal class CustomMonster
    {
        public int ID { get; set; }
        public CustomMonsterType CMType { get; set; }
        public NPC MainNPC { get { return Main.npc[ID]; } }
        public DateTime SpawnTime { get; set; }
        //public bool Customized { get; set; }
        internal int MODLevel { get; set; }
        internal CustomMonster(int id, int modlevel, CustomMonsterType cmtype)
        {
            ID = id;
            MODLevel = modlevel;
            CMType = cmtype;
            SpawnTime = DateTime.Now;
            //Customized = true;
        }
    }
    internal class ZoneAndRate
    {
        public int Rate { get; set; }
        public bool SpawnHere { get; set; }
        public ZoneAndRate(int rate = 10, bool spawnhere = false)
        {
            Rate = rate;
            SpawnHere = spawnhere;
        }
    }
    internal class RegionAndRate
    {
        public Region SpawnRegion { get; set; }
        public int SpawnRate { get; set; }
        public int SpawnChance { get; set; }
        public bool StaticSpawnRate { get; set; }
        public int DefaultMaxSpawns { get; set; }
        public List<NPC> MonstersInRegion
        {
            get
            {
                List<NPC> TBR = new List<NPC>();
                for(int i = 0; i< Main.maxNPCs;i++)
                {
                    if (SpawnRegion.InArea(Main.npc[i].frame))
                        TBR.Add(Main.npc[i]);
                }
                return TBR;
            }
        }
        public DateTime LastSpawn { get; set; }
        public List<CMPlayer> PlayersInRegion { get { return CustomMonstersPlugin.CMPlayers.FindAll(player => SpawnRegion.InArea(player.TSPlayer.TPlayer.bodyFrame)); } }
        public int MaxSpawns
        {
            get
            {
                if (StaticSpawnRate)
                    return DefaultMaxSpawns;
                else
                {
                    return (PlayersInRegion.Count * DefaultMaxSpawns);
                }
            }
        }

        public RegionAndRate(Region spawnregion, int spawnrate, int maxspawns = 5, int spawnchance = 1, bool staticspawnrate = false)
        {
            SpawnRegion = spawnregion;
            SpawnRate = spawnrate;
            DefaultMaxSpawns = maxspawns;
            StaticSpawnRate = staticspawnrate;
            SpawnChance = spawnchance;
            LastSpawn = CustomMonstersPlugin.Init;
        }
    }
    internal class BuffRateandDuration
    {
        public int BuffType { get; set; }
        public int BuffTime { get; set; }
        public int Rate { get; set; }
        public BuffRateandDuration(int type, int time = 5, int rate = 10)
        {
            BuffType = type;
            BuffTime = time;
            Rate = rate;
        }
    }
    internal class BlitzData
    {
        internal int BlitzerType { get; set; }
        internal int BlitzStyle { get; set; }
        internal int BlitzTime { get; set; }
        internal BlitzData(int type, int style, int time)
        {
            BlitzerType = type;
            BlitzStyle = style;
            BlitzTime = time;
        }
    }
    internal class CBlitzData
    {
        internal string CBlitzerType { get; set; }
        internal int CBlitzStyle { get; set; }
        internal int CBlitzTime { get; set; }
        internal CBlitzData(string type, int style, int time)
        {
            CBlitzerType = type;
            CBlitzStyle = style;
            CBlitzTime = time;
        }
    }
    internal class ShooterData
    {
        internal int ProjectileType { get; set; }
        internal int ProjectileDamage { get; set; }
        internal int ShootStyle { get; set; }
        internal int ShootTime { get; set; }
        internal ShooterData(int type, int damage, int style, int time)
        {
            ProjectileType = type;
            ProjectileDamage = damage;
            ShootStyle = style;
            ShootTime = time;
        }
    }
    internal class Transformation
    {
        internal string TransformTo { get; set; }
        internal bool transform { get; set; }
        internal CustomMonsterType TransToType { get { return CustomMonstersPlugin.CMTypes.Find(item => item.Name.ToLower() == TransformTo.ToString()); } }
        internal int HP { get; set; }
        internal Transformation(string name, int hp)
        {
            HP = hp;
            TransformTo = name;
            transform = true;
        }
        internal Transformation()
        {
            HP = 0;
            TransformTo = "";
            transform = false;
        }
    }
    internal class CustomMonsterType
    {
        internal string Name { get; set; }
        internal int BaseType { get; set; }
        internal int? Life { get; set; }

        internal List<BlitzData> BlitzData { get; set; }
        internal List<CBlitzData> CBlitzData { get; set; }
        internal List<ShooterData> ShooterData { get; set; }
        internal List<BuffRateandDuration> Buffs { get; set; }

        internal ZoneAndRate Corruption { get; set; }
        internal ZoneAndRate Jungle { get; set; }
        internal ZoneAndRate Meteor { get; set; }
        internal ZoneAndRate Dungeon { get; set; }
        internal ZoneAndRate Hallow { get; set; }
        internal ZoneAndRate Forest { get; set; }
        internal List<RegionAndRate> SpawnRegions { get; set; }
        internal List<NPC> Replaces { get; set; }

        internal int? CustomAIStyle { get; set; }
        internal bool MultiplyOnDeath { get; set; }
        internal int MODMaxLevel { get; set; }
        internal Transformation Transformation { get; set; }

        internal bool? dontTakeDamage { get; set; }
        internal bool? lavaImmune { get; set; }
        internal bool? Boss { get; set; }
        internal bool? noTileCollide { get; set; }
        internal bool? noGravity { get; set; }
        internal float? Value { get; set; }
        internal string SpawnMessage { get; set; }

        internal bool? OnFire { get; set; }
        internal bool? Poisoned { get; set; }

        public CustomMonsterType(string name ="",int basetype=1)
        {
            Name = name;
            BaseType = basetype;
            Life = null;
            
            BlitzData = new List<BlitzData>();
            CBlitzData = new List<CBlitzData>();
            ShooterData = new List<ShooterData>();
            Buffs = new List<BuffRateandDuration>();

            Corruption = new ZoneAndRate();
            Jungle = new ZoneAndRate();
            Meteor = new ZoneAndRate();
            Dungeon = new ZoneAndRate();
            Hallow = new ZoneAndRate();
            Forest = new ZoneAndRate();

            Transformation = new Transformation();

            SpawnRegions = new List<RegionAndRate>();
            Replaces = new List<NPC>();

            CustomAIStyle = null;
            MultiplyOnDeath = false;
            MODMaxLevel = 0;
            Transformation = null;

            dontTakeDamage = null;
            lavaImmune = null;
            Boss = null;
            noGravity = null;
            noTileCollide = null;
            Value = null;
            SpawnMessage = "";

            OnFire = null;
            Poisoned = null;
        }
    }
}
