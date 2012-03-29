using System;
using System.Collections.Generic;
using System.Threading;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using MySql.Data.MySqlClient;
using System.IO;
using System.Reflection;

namespace CustomMonsters
{
    [APIVersion(1, 11)]
    public class CustomMonstersPlugin : TerrariaPlugin
    {
        private static CustomMonsterConfigFile CMConfig { get; set; }
        internal static string CMConfigPath { get { return Path.Combine(TShock.SavePath, "cmconfig.json"); } }
        internal static string CustomMonstersDataDirectory { get { return Path.Combine(TShock.SavePath, "Custom Monsters"); } }
        internal static List<CMPlayer> CMPlayers = new List<CMPlayer>();
        internal static List<CustomMonster> CustomMonsters = new List<CustomMonster>();
        internal static List<CustomMonsterType> CMTypes = new List<CustomMonsterType>();
        public static DateTime Init;
        #region Shot tiles
        private static ShotTile TopLeft { get { return new ShotTile((float)(-Math.Sqrt(2) * 8), (float)(-Math.Sqrt(2) * 8)); } }
        private static ShotTile Top { get { return new ShotTile(0, -16); } }
        private static ShotTile TopRight { get { return new ShotTile((float)(Math.Sqrt(2) * 8), (float)(-Math.Sqrt(2) * 8)); } }
        private static ShotTile Left { get { return new ShotTile(-16, 0); } }
        private static ShotTile Center { get { return new ShotTile(0, 0); } }
        private static ShotTile Right { get { return new ShotTile(16, 0); } }
        private static ShotTile BottomLeft { get { return new ShotTile((float)(-Math.Sqrt(2) * 8), (float)(Math.Sqrt(2) * 8)); } }
        private static ShotTile Bottom { get { return new ShotTile(0, 16); } }
        private static ShotTile BottomRight { get { return new ShotTile((float)(Math.Sqrt(2) * 8), (float)(Math.Sqrt(2) * 8)); } }
#endregion

        public override string Name
        {
            get { return "Custom Monsters Plugin"; }
        }
        public override string Author
        {
            get { return "Created by Vharonftw"; }
        }
        public override string Description
        {
            get { return ""; }
        }
        public override Version Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version; }
        }

        public override void Initialize()
        {
            Init = DateTime.Now;

            GameHooks.Update += OnUpdate;
            GameHooks.Initialize += OnInitialize;
            NetHooks.GreetPlayer += OnGreetPlayer;
            ServerHooks.Leave += OnLeave;
            ServerHooks.Chat += OnChat;
            NetHooks.GetData += OnGetData;
            //NpcHooks.NetDefaults += OnNetDefaults;
            NpcHooks.SetDefaultsInt += OnSetDefaultsInt;
            NpcHooks.SetDefaultsString += OnSetDefaultsString;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                GameHooks.Update -= OnUpdate;
                GameHooks.Initialize -= OnInitialize;
                NetHooks.GreetPlayer -= OnGreetPlayer;
                ServerHooks.Leave -= OnLeave;
                ServerHooks.Chat -= OnChat;
                NetHooks.GetData -= OnGetData;
                //NpcHooks.NetDefaults -= OnNetDefaults;
                NpcHooks.SetDefaultsInt -= OnSetDefaultsInt;
                NpcHooks.SetDefaultsString -= OnSetDefaultsString;
            }
            base.Dispose(disposing);
        }
        public CustomMonstersPlugin(Main game)
            : base(game)
        {
            Order = -1;
            CMConfig = new CustomMonsterConfigFile();
        }
        //public void OnNetDefaults(SetDefaultsEventArgs<NPC, int> e)
        //{
        //}
        public void OnSetDefaultsInt(SetDefaultsEventArgs<NPC, int> e)
        {
            CustomMonster CM = CustomMonsters.Find(cm => cm.ID == e.Object.whoAmI);
            CustomMonsters.Remove(CM);
        }
        public void OnSetDefaultsString(SetDefaultsEventArgs<NPC, string> e)
        {
            CustomMonster CM = CustomMonsters.Find(cm => cm.ID == e.Object.whoAmI);
            CustomMonsters.Remove(CM);
        }
        private void OnGetData(GetDataEventArgs e)
        {
            if (e.MsgID == PacketTypes.NpcStrike)
            {
                using (var data = new MemoryStream(e.Msg.readBuffer, e.Index, e.Length))
                {
                    var reader = new BinaryReader(data);
                    var npcid = reader.ReadInt16();
                    var dmg = reader.ReadInt16();
                    var knockback = reader.ReadSingle();
                    var direction = reader.ReadByte();
                    var crit = reader.ReadBoolean();
                    int critmultiply = 1;
                    if (crit)
                        critmultiply = 2;
                    int actualdmg = (dmg - Main.npc[npcid].defense / 2) * critmultiply;
                    if (actualdmg < 0)
                        actualdmg = 1;
                    if (actualdmg >= Main.npc[npcid].life && Main.npc[npcid].life > 0 && Main.npc[npcid].active)
                    {
                        CustomMonster DeadMonster = CustomMonsters.Find(monster => monster.ID == npcid);
                        if (DeadMonster != null)
                        {
                            if (DeadMonster.CMType.MultiplyOnDeath)
                            {
                                int killer = e.Msg.whoAmI;
                                int monstersplit = SpawnCustomMonsterExactPosition(DeadMonster.CMType, TShock.Players[killer].TileX + 1, TShock.Players[killer].TileY, (DeadMonster.MODLevel + 1));
                                int monstersplit2 = SpawnCustomMonsterExactPosition(DeadMonster.CMType, TShock.Players[killer].TileX + 2, TShock.Players[killer].TileY, (DeadMonster.MODLevel + 1));
                            }
                        }
                    }
                    return;
                }
            }
        }
        private void OnInitialize()
        {
            LoadAllCustomMonsters();
            Commands.ChatCommands.Add(new Command("spawncustommonsters", SpawnCustomMonsterPlayer, "scm"));
            Commands.ChatCommands.Add(new Command("reload", CMReload, "cmreload"));
            
        }

        private void OnUpdate()
        {
            SpawnInZones();
            HandleShooters();
            HandleBlitzers();
            HandleCBlitzers();
            HandleTransFormations();
            ReplaceMonsters();
            UpdateCAIMonsters();
            SpawnInRegions();
            HandleBuffers();
	    RemoveInactive();
        }

        private void OnGreetPlayer(int who, HandledEventArgs e)
        {
            CMPlayers.Add(new CMPlayer(who));
        }

        private void OnLeave(int ply)
        {
            lock (CMPlayers)
            {
                CMPlayers.Remove(CMPlayers.Find(player => player.Index == ply));
            }
        }

        private void OnChat(messageBuffer msg, int ply, string text, HandledEventArgs e)
        {
        }

        private static void SetupConfig()
        {
            try
            {
                if (File.Exists(CMConfigPath))
                {
                    CMConfig = CustomMonsterConfigFile.Read(CMConfigPath);
                    // Add all the missing config properties in the json file
                }
                CMConfig.Write(CMConfigPath);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error in (CM) config file");
                Console.ForegroundColor = ConsoleColor.Gray;
                Log.Error("(CM) Config Exception");
                Log.Error(ex.ToString());
            }
        }

        private static void SpawnCustomMonsterPlayer(CommandArgs args)
        {
            if (args.Parameters.Count > 0)
            {
                CustomMonsterType CMType = CMTypes.Find(cmt =>cmt.Name.ToLower().StartsWith(args.Parameters[0].ToLower()));
                if (CMType != null)
                {
                    int count = 1;
                    if (args.Parameters.Count > 1)
                        Int32.TryParse(args.Parameters[1], out count);
                    int i = 0;
                    while (i < count)
                    {
                        SpawnCustomMonster(CMType, (int)args.Player.X + 48, (int)args.Player.Y);
                        i++;
                    }
                    TShock.Utils.Broadcast(args.Player.Name + " spawned " + count + " " + CMType.Name + "s", Color.Yellow);
                }
                else
                    args.Player.SendMessage("no Custom Monster Matched", Color.Red);
            }
        }

        private static void CMReload(CommandArgs args)
        {
            LoadAllCustomMonsters();
        }
        private static void CustomizeMonster(int npcid, CustomMonsterType CMType, int modlevel, int life = -1)
        {
            NPC Custom = Main.npc[npcid];
            Custom.netDefaults(CMType.BaseType);

            Custom.name = CMType.Name;
            Custom.displayName = CMType.Name;
            Custom.lifeMax = CMType.Life ?? Custom.lifeMax;
            Custom.life = life <= 0 ? (CMType.Life ?? Custom.life): life;

            Custom.aiStyle = CMType.CustomAIStyle ?? Custom.aiStyle;
            Custom.dontTakeDamage = CMType.dontTakeDamage ?? Custom.dontTakeDamage;
            Custom.lavaImmune = CMType.lavaImmune ?? Custom.lavaImmune;
            Custom.boss = CMType.Boss ?? Custom.boss;
            Custom.noGravity = CMType.noGravity ?? Custom.noGravity;
            Custom.noTileCollide = CMType.noTileCollide ?? Custom.noTileCollide;

            Custom.value = CMType.Value ?? Custom.value;
            Custom.onFire = CMType.OnFire ?? Custom.onFire;
            Custom.poisoned = CMType.Poisoned ?? Custom.poisoned;

            if (modlevel == 0 && CMType.SpawnMessage != "")
                TShockAPI.TShock.Utils.Broadcast(CMType.SpawnMessage, Color.MediumPurple);
            CustomMonsters.Add(new CustomMonster(npcid, modlevel, CMType));
        }

        private static int SpawnCustomMonster(CustomMonsterType CMType, int X, int Y, int modlevel = 0)
        {
            int CID = -1;
            if (modlevel <= CMType.MODMaxLevel)
            {
                int npcid = NPC.NewNPC(X , Y , CMType.BaseType);
//		Console.WriteLine(String.Format("id is {0} X {1} Y {2} - compare {3} and {4}",npcid,X,Y,Main.npc[1].position.X,Main.npc[1].position.Y));
		
                Main.npc[npcid].SetDefaults(CMType.BaseType);
                CustomizeMonster(npcid, CMType, modlevel);
                CID = npcid;
                NetMessage.SendData(23, -1, -1, "", npcid, 0f, 0f, 0f, 0);
            }
            return CID;
        }
        
        private static int SpawnCustomMonsterExactPosition(CustomMonsterType CMType, int X, int Y, int modlevel = 0)
        {
            int CID = -1;
            if (modlevel <= CMType.MODMaxLevel)
            {
                int spawnTileX;
                int spawnTileY;
                TShockAPI.TShock.Utils.GetRandomClearTileWithInRange(X, Y, 10, 10, out spawnTileX, out spawnTileY);
                int npcid = NPC.NewNPC(spawnTileX * 16, spawnTileY * 16, CMType.BaseType);
                Main.npc[npcid].SetDefaults(CMType.BaseType);
                CustomizeMonster(npcid, CMType, modlevel);
                CID = npcid;
                NetMessage.SendData(23, -1, -1, "", npcid, 0f, 0f, 0f, 0);
            }
            return CID;
        }

        private static void HandleShooters()
        {
            List<CustomMonster> Shooters = CustomMonsters.FindAll(monster => monster.CMType.ShooterData.Count > 0);
            foreach (CustomMonster shooter in Shooters)
            {
                foreach (ShooterData sd in shooter.CMType.ShooterData)
                {
                    if ((((int)(shooter.SpawnTime - DateTime.Now).TotalMilliseconds/100) % (sd.ShootTime) == 0) && (Main.npc[shooter.ID].active))
                    {
                        ShootProjectile(shooter.MainNPC.position.X, shooter.MainNPC.position.Y, sd.ShootStyle, sd.ProjectileDamage, shooter.ID, sd.ProjectileType);
                    }
                }
            }
        }

        private static void HandleBlitzers()
        {
            List<CustomMonster> Blitzers = CustomMonsters.FindAll(monster => monster.CMType.BlitzData.Count > 0);
            foreach (CustomMonster blitzer in Blitzers)
            {
                foreach (BlitzData bd in blitzer.CMType.BlitzData)
                {
                    if (((int)(blitzer.SpawnTime - DateTime.Now).TotalMilliseconds/100) % (bd.BlitzTime) == 0)
                    {
                        Blitz(blitzer.MainNPC.position.X, blitzer.MainNPC.position.Y, bd.BlitzStyle, bd.BlitzerType);
                    }
                }
            }
        }

        private static void HandleCBlitzers()
        {
            List<CustomMonster> CBlitzers = CustomMonsters.FindAll(monster => monster.CMType.CBlitzData.Count > 0);
            foreach (CustomMonster Cblitzer in CBlitzers)
            {
                foreach (CBlitzData cbd in Cblitzer.CMType.CBlitzData)
                {
                    if (((int)(Cblitzer.SpawnTime - DateTime.Now).TotalMilliseconds/100) % (cbd.CBlitzTime) == 0)
                    {
                        CBlitz(Cblitzer.MainNPC.position.X, Cblitzer.MainNPC.position.Y, cbd.CBlitzStyle, cbd.CBlitzerType);
                    }
                }
            }
        }

        private static void HandleTransFormations()
        {
            List<CustomMonster> Transformers = CustomMonsters.FindAll(cm => cm.CMType.Transformation.transform);
            foreach (CustomMonster CM in Transformers)
            {
                if (CM.MainNPC.life <= CM.CMType.Transformation.HP)
		try {
                    CustomizeMonster(CM.ID, CM.CMType.Transformation.TransToType, 0, CM.MainNPC.life);
		    }catch (NullReferenceException Z)
		    {
		    // TODO: track down this error and fix it for real.
		    }
            }
        }

        private static void ShootProjectile(float X, float Y, int ShootStyle, int ProjectileDamage, int npcid,int ProjectileType)
        {
            if (ShootStyle > 0)
            {
                List<ShotTile> LaserGrid = new List<ShotTile>();
                if (ShootStyle >= 1 && ShootStyle <= 9)
                {
                    LaserGrid.Add(Top);
                    if (ShootStyle > 1)
                        LaserGrid.Add(Bottom);
                    if ((ShootStyle % 4) <= 1 && ShootStyle > 1)
                    {
                        LaserGrid.Add(Left);
                        LaserGrid.Add(Right);
                    }
                    if (ShootStyle > 1 && ShootStyle % 2 == 1)
                        LaserGrid.Add(Center);
                    if (ShootStyle > 5)
                    {
                        LaserGrid.Add(TopLeft);
                        LaserGrid.Add(TopRight);
                        LaserGrid.Add(BottomLeft);
                        LaserGrid.Add(BottomRight);
                    }
                }
                else if (ShootStyle >= 10)
                {
                    if (ShootStyle == 11)
                    {
                        LaserGrid.Add(Left);
                        LaserGrid.Add(Right);
                    }
                    if (ShootStyle == 10)
                        LaserGrid.Add(Center);
                }

                foreach (ShotTile bt in LaserGrid)
                {
                    if (Main.npc[npcid].target >= 0 && Main.npc[npcid].target < 256)
                    {
                        int targetid = Main.npc[npcid].target;
                        Vector2 Target = Main.player[targetid].position;
                        Vector2 Start = new Vector2(X + (2 * bt.X) + 10, Y + (2 * bt.Y) + 23);
                        //Vector2 Target = Main.player[targetid].position;
                        //Vector2 Start = new Vector2(X + (2 * bt.X) + 10, Y + (2 * bt.Y) + 23);
                        float initY = Target.Y - Start.Y;
                        float initX = Target.X - Start.X;
                        int parityX = initX < 0 ? -1 : 1;
                        int parityY = initY < 0 ? -1 : 1;
                        float VelocityX = (float)(10 * Math.Sqrt(1 - (Math.Pow(initX, 2) / (Math.Pow(initX, 2) + Math.Pow(initY, 2))))) * parityX;
                        float VelocityY = (float)(10 * Math.Sqrt(1 - (Math.Pow(initY, 2) / (Math.Pow(initX, 2) + Math.Pow(initY, 2))))) * parityY;

                        if (Collision.CanHit(Start, 4, 4, Target, Main.player[targetid].width, Main.player[targetid].height))
                        {
                            int New = Projectile.NewProjectile(Start.X, Start.Y, VelocityX, VelocityY, ProjectileType, ProjectileDamage, (float)0.5);
                            Main.projectile[New].SetDefaults(ProjectileType);
                            NetMessage.SendData(27, -1, -1, "", New, 0f, 0f, 0f, 0);
                        }
                    }
                }
            }
        }

        private static void Blitz(float X, float Y, int blitzstyle, int blitztype)
        {
            if (blitzstyle > 0)
            {
                List<ShotTile> BlitzGrid = new List<ShotTile>();
                BlitzGrid.Add(Top);
                if (blitzstyle > 1)
                    BlitzGrid.Add(Bottom);
                if ((blitzstyle % 4) <= 1 && blitzstyle > 1)
                {
                    BlitzGrid.Add(Left);
                    BlitzGrid.Add(Right);
                }
                if (blitzstyle > 1 && blitzstyle % 2 == 1)
                    BlitzGrid.Add(Center);
                if (blitzstyle > 5)
                {
                    BlitzGrid.Add(TopLeft);
                    BlitzGrid.Add(TopRight);
                    BlitzGrid.Add(BottomLeft);
                    BlitzGrid.Add(BottomRight);
                }

                foreach (ShotTile bt in BlitzGrid)
                {
                    int blitzshot = NPC.NewNPC((int)(X + (2 * bt.X) + 10), (int)(Y + (2 * bt.Y) + 23), blitztype, 0);
                    Main.npc[blitzshot].SetDefaults(blitztype);
                }
            }
        }

        private static void CBlitz(float X, float Y, int blitzstyle, string SCMType)
        {
            CustomMonsterType CMType = CMTypes.Find(cmt => cmt.Name == SCMType);
            if (blitzstyle > 0)
            {
                List<ShotTile> BlitzGrid = new List<ShotTile>();
                BlitzGrid.Add(Top);
                if (blitzstyle > 1)
                    BlitzGrid.Add(Bottom);
                if ((blitzstyle % 4) <= 1 && blitzstyle > 1)
                {
                    BlitzGrid.Add(Left);
                    BlitzGrid.Add(Right);
                }
                if (blitzstyle > 1 && blitzstyle % 2 == 1)
                    BlitzGrid.Add(Center);
                if (blitzstyle > 5)
                {
                    BlitzGrid.Add(TopLeft);
                    BlitzGrid.Add(TopRight);
                    BlitzGrid.Add(BottomLeft);
                    BlitzGrid.Add(BottomRight);
                }

                if (SCMType != null)
                {
                    foreach (ShotTile bt in BlitzGrid)
                    {
                        int blitzshot = NPC.NewNPC((int)(X + (2 * bt.X) + 10), (int)(Y + (2 * bt.Y) + 23), CMType.BaseType, 0);
                        CustomizeMonster(blitzshot, CMType, 0);
                    }
                }
            }
        }

        private static void SpawnInZones()
        {
            List<CustomMonsterType> Corruption = CMTypes.FindAll(cmt => cmt.Corruption.SpawnHere == true);
            List<CustomMonsterType> Dungeon = CMTypes.FindAll(cmt => cmt.Dungeon.SpawnHere == true);
            List<CustomMonsterType> Meteor = CMTypes.FindAll(cmt => cmt.Meteor.SpawnHere == true);
            List<CustomMonsterType> Jungle = CMTypes.FindAll(cmt => cmt.Jungle.SpawnHere == true);
            List<CustomMonsterType> Hallow = CMTypes.FindAll(cmt => cmt.Hallow.SpawnHere == true);
            List<CustomMonsterType> Forest = CMTypes.FindAll(cmt => cmt.Forest.SpawnHere == true);

            List<CMPlayer> CorruptionPlayers = CMPlayers.FindAll(player => player.TSPlayer.TPlayer.zoneEvil == true);
            List<CMPlayer> DungeonPlayers = CMPlayers.FindAll(player => player.TSPlayer.TPlayer.zoneDungeon == true);
            List<CMPlayer> MeteorPlayers = CMPlayers.FindAll(player => player.TSPlayer.TPlayer.zoneMeteor == true);
            List<CMPlayer> HallowPlayers = CMPlayers.FindAll(player => player.TSPlayer.TPlayer.zoneHoly == true);
            List<CMPlayer> JunglePlayers = CMPlayers.FindAll(player => player.TSPlayer.TPlayer.zoneJungle == true);
            List<CMPlayer> ForestPlayers = CMPlayers.FindAll(player => player.TSPlayer.TPlayer.zoneJungle == false && player.TSPlayer.TPlayer.zoneDungeon == false && player.TSPlayer.TPlayer.zoneMeteor == false && player.TSPlayer.TPlayer.zoneEvil == false);
            #region corruption spawn
            if (Corruption.Count>0 && CorruptionPlayers.Count>0)
            {
                lock (CorruptionPlayers)
                {
                    foreach (CMPlayer player in CorruptionPlayers)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active = false);
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((player.LastCustomZoneSpawn - DateTime.Now).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (Corruption)
                                {
                                    CustomMonsterType cmtype = Corruption[mt.Next() % Corruption.Count];
                                    if (mc.Next() % cmtype.Corruption.Rate == 0)
                                    {
                                        int NPCID = SpawnCustomMonster(cmtype, (int)player.TSPlayer.X, (int)player.TSPlayer.Y);
                                        if (NPCID >= 0)
                                        {
                                            player.NPCIDs.Add(NPCID);
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Hallow spawn
            if (Hallow.Count > 0 && HallowPlayers.Count > 0)
            {
                lock (HallowPlayers)
                {
                    foreach (CMPlayer player in HallowPlayers)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active = false);
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((player.LastCustomZoneSpawn - DateTime.Now).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (Hallow)
                                {
                                    CustomMonsterType cmtype = Hallow[mt.Next() % Hallow.Count];
                                    if (mc.Next() % cmtype.Hallow.Rate == 0)
                                    {
                                        int NPCID = SpawnCustomMonster(cmtype, (int)player.TSPlayer.X, (int)player.TSPlayer.Y);
                                        if (NPCID >= 0)
                                        {
                                            player.NPCIDs.Add(NPCID);
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Meteor spawn
            if (Meteor.Count > 0 && MeteorPlayers.Count > 0)
            {
                lock (MeteorPlayers)
                {
                    foreach (CMPlayer player in MeteorPlayers)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active = false);
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((player.LastCustomZoneSpawn - DateTime.Now).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (Meteor)
                                {
                                    CustomMonsterType cmtype = Corruption[mt.Next() % Meteor.Count];
                                    if (mc.Next() % cmtype.Meteor.Rate == 0)
                                    {
                                        int NPCID = SpawnCustomMonster(cmtype, (int)player.TSPlayer.X, (int)player.TSPlayer.Y);
                                        if (NPCID >= 0)
                                        {
                                            player.NPCIDs.Add(NPCID);
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Jungle spawn
            if (Jungle.Count > 0 && JunglePlayers.Count > 0)
            {
                lock (JunglePlayers)
                {
                    foreach (CMPlayer player in JunglePlayers)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active = false);
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((player.LastCustomZoneSpawn - DateTime.Now).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (Jungle)
                                {
                                    CustomMonsterType cmtype = Jungle[mt.Next() % Jungle.Count];
                                    if (mc.Next() % cmtype.Jungle.Rate == 0)
                                    {
                                        int NPCID = SpawnCustomMonster(cmtype, (int)player.TSPlayer.X, (int)player.TSPlayer.Y);
                                        if (NPCID >= 0)
                                        {
                                            player.NPCIDs.Add(NPCID);
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Dungeon spawn
            if (Dungeon.Count > 0 && DungeonPlayers.Count > 0)
            {
                lock (DungeonPlayers)
                {
                    foreach (CMPlayer player in DungeonPlayers)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active = false);
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((player.LastCustomZoneSpawn - DateTime.Now).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (Dungeon)
                                {
                                    CustomMonsterType cmtype = Dungeon[mt.Next() % Dungeon.Count];
                                    if (mc.Next() % cmtype.Dungeon.Rate == 0)
                                    {
                                        int NPCID = SpawnCustomMonster(cmtype, (int)player.TSPlayer.X, (int)player.TSPlayer.Y);
                                        if (NPCID >= 0)
                                        {
                                            player.NPCIDs.Add(NPCID);
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion

            #region Forest spawn
            if (Forest.Count > 0 && ForestPlayers.Count > 0)
            {
                lock (ForestPlayers)
                {
                    foreach (CMPlayer player in ForestPlayers)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active = false);
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((player.LastCustomZoneSpawn - DateTime.Now).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (Forest)
                                {
                                    CustomMonsterType cmtype = Forest[mt.Next() % Forest.Count];
                                    if (mc.Next() % cmtype.Forest.Rate == 0)
                                    {
                                        int NPCID = SpawnCustomMonster(cmtype, (int)player.TSPlayer.X, (int)player.TSPlayer.Y);
                                        if (NPCID >= 0)
                                        {
                                            player.NPCIDs.Add(NPCID);
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            #endregion
        }

        private static void ReplaceMonsters()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (CustomMonsters.Find(monster => monster.ID == i) == null)
                {
                    CustomMonsterType CMType = new CustomMonsterType();
                    List<CustomMonsterType> RCMTypes = CMTypes.FindAll(monstertype => monstertype.Replaces.Count > 0);
                    foreach (CustomMonsterType cmt in RCMTypes)
                    {
                        foreach (NPC npc in cmt.Replaces)
                        {
                            if (npc.name.ToLower() == Main.npc[i].name.ToLower())
                            {
                                CustomizeMonster(i, CMType, 0);
                            }
                        }
                    }
                    if (CMType.Name != "" && CMType.BaseType > 0)
                        CustomizeMonster(i, CMType, 0);
                }
                else
                    continue;
            }
        }

        private static void UpdateCAIMonsters()
        {
            List<CustomMonster> CAIMs = CustomMonsters.FindAll(cm => cm.CMType.CustomAIStyle.HasValue);
            foreach (CustomMonster CM in CAIMs)
            {
                NetMessage.SendData(23, -1, -1, "", CM.ID, 0f, 0f, 0f, 0);
            }
        }

        private static void SpawnInRegions()
        {
            List<CustomMonsterType> RSers = CMTypes.FindAll(CMType => CMType.SpawnRegions.Count > 0);
            foreach (CustomMonsterType RS in RSers)
            {
                foreach (RegionAndRate SR in RS.SpawnRegions)
                {
                    Random r = new Random();
                    if (r.Next() % SR.SpawnChance == 0 && (SR.LastSpawn - DateTime.Now).TotalMilliseconds >= SR.SpawnRate && SR.PlayersInRegion.Count>0 && SR.MonstersInRegion.Count<SR.MaxSpawns)
                    {
                        int x = (int)SR.PlayersInRegion[0].TSPlayer.X;
                        int y = (int)SR.PlayersInRegion[0].TSPlayer.Y;
                        int outx;
                        int outy;

                        TShock.Utils.GetRandomClearTileWithInRange(x, y, 10, 10, out outx, out outy);
                        SpawnCustomMonsterExactPosition(RS, outx, outy);
                        SR.LastSpawn = DateTime.Now;
                    }
                }
            }
        }

        private static void HandleBuffers()
        {
            List<CustomMonster> Buffers = CustomMonsters.FindAll(CM => CM.CMType.Buffs.Count>0);
            foreach (CustomMonster buffer in Buffers)
            {
                List<CMPlayer> BuffThese = CMPlayers.FindAll(ply => buffer.MainNPC.frame.Intersects(ply.TSPlayer.TPlayer.bodyFrame));
                foreach (CMPlayer buffthis in BuffThese)
                {
                    foreach (BuffRateandDuration buff in buffer.CMType.Buffs)
                    {
                        Random r = new Random();
                        if (r.Next() % buff.Rate == 0)
                            buffthis.TSPlayer.SetBuff(buff.BuffType, buff.BuffTime);
                    }
                }
            }
        }

        private static void LoadCustomMonstersFromText()
        {

            string[] CustomMonstersDataPaths = Directory.GetFiles(@CustomMonstersDataDirectory);

            foreach (string CMDataPath in CustomMonstersDataPaths)
            {
                List<string> MonsterData = new List<string>();
                try
                {
                    using (StreamReader sr = new StreamReader(CMDataPath))
                    {
                        string line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            if (!line.StartsWith("##"))
                                MonsterData.Add(line);
                        }
                    }
                }
                catch (Exception e)
                {
                    string errormessage = string.Format("The file \"{0}\" could not be read:", CMDataPath);
                    Console.WriteLine(errormessage);
                    Console.WriteLine(e.Message);
                }
                CustomMonsterType CMType = new CustomMonsterType();

                foreach (string CMFieldAndVal in MonsterData)
                {
                    if (CMFieldAndVal.Split(':').Length > 1)
                    {
                        #region donator version switch block
                        switch (CMFieldAndVal.Split(':')[0].ToLower())
                        {
                            case "name":
                                {
                                    CMType.Name = CMFieldAndVal.Split(':')[1];
                                    break;
                                }
                            case "basetype":
                            case "type":
                                {
                                    int type;
                                    Int32.TryParse(CMFieldAndVal.Split(':')[1], out type);
                                    CMType.BaseType = type;
                                    break;
                                }
                            case "life":
                            case "lifemax":
                                    {
                                        int life;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out life);
                                        CMType.Life = life;
                                        break;
                                    }
                            case "blitzdata":
                            case "blitz":
                                    {
                                        int type;
                                        int style;
                                        int time;
                                        if (CMFieldAndVal.Split(':').Length > 3)
                                        {
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out type) && Int32.TryParse(CMFieldAndVal.Split(':')[2], out style) && Int32.TryParse(CMFieldAndVal.Split(':')[3], out time))
                                            {
                                                CMType.BlitzData.Add(new BlitzData(type, style, time));
                                            }
                                        }
                                        break;
                                    }
                            case "cblitzdata":
                            case "cblitz":
                                    {
                                        int style;
                                        int time;
                                        if (CMFieldAndVal.Split(':').Length > 3)
                                        {
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out style) && Int32.TryParse(CMFieldAndVal.Split(':')[3], out time))
                                            {
                                                CMType.CBlitzData.Add(new CBlitzData(CMFieldAndVal.Split(':')[0], style, time));
                                            }
                                        }
                                        break;
                                    }
                            case "shooterdata":
                            case "shooter":
                                    {
                                        int type;
                                        int style;
                                        int time;
                                        int damage;
                                        if (CMFieldAndVal.Split(':').Length > 4)
                                        {
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out type) && Int32.TryParse(CMFieldAndVal.Split(':')[2], out style) && Int32.TryParse(CMFieldAndVal.Split(':')[3], out time) && Int32.TryParse(CMFieldAndVal.Split(':')[4], out damage))
                                            {
                                                CMType.ShooterData.Add(new ShooterData(type, damage, style, time));
                                            }
                                        }
                                        break;
                                    }
                            case "buff":
                                    {
                                        int type;
                                        int time = 5;
                                        int rate = 10;
                                        
                                        if (CMFieldAndVal.Split(':').Length > 1)
                                        {
                                            if (CMFieldAndVal.Split(':').Length > 2)
                                                Int32.TryParse(CMFieldAndVal.Split(':')[2], out time);
                                            if (CMFieldAndVal.Split(':').Length > 3)
                                                Int32.TryParse(CMFieldAndVal.Split(':')[3], out rate);
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out type))
                                            {
                                                CMType.Buffs.Add(new BuffRateandDuration(type, time, rate));
                                            }
                                        }
                                        break;
                                    }
                                case "corruption":
                                    {
                                        int rate=10;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out rate);
                                        CMType.Corruption.SpawnHere = true;
                                        CMType.Corruption.Rate = rate;                                        
                                        break;
                                    }
                                case "meteor":
                                    {
                                        int rate = 10;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out rate);
                                        CMType.Meteor.SpawnHere = true;
                                        CMType.Meteor.Rate = rate;
                                        break;
                                    }
                                case "jungle":
                                    {
                                        int rate = 10;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out rate);
                                        CMType.Jungle.SpawnHere = true;
                                        CMType.Jungle.Rate = rate;
                                        break;
                                    }
                                case "hallow":
                                    {
                                        int rate = 10;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out rate);
                                        CMType.Hallow.SpawnHere = true;
                                        CMType.Hallow.Rate = rate;
                                        break;
                                    }
                                case "dungeon":
                                    {
                                        int rate = 10;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out rate);
                                        CMType.Dungeon.SpawnHere = true;
                                        CMType.Dungeon.Rate = rate;
                                        break;
                                    }
                                case "forest":
                                    {
                                        int rate = 10;
                                        Int32.TryParse(CMFieldAndVal.Split(':')[1], out rate);
                                        CMType.Forest.SpawnHere = true;
                                        CMType.Forest.Rate = rate;
                                        break;
                                    }
                            case "region":
                                    {
                                        if (CMFieldAndVal.Split(':').Length > 5)
                                        {
                                            Region spawnregion = TShock.Regions.GetRegionByName(CMFieldAndVal.Split(':')[1]);
                                            int spawnrate;
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                            {
                                                int spawnchance = 1;
                                                int maxspawns =5;
                                                bool staticspawnrate = false;
                                                Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxspawns);
                                                Int32.TryParse(CMFieldAndVal.Split(':')[4], out spawnchance);
                                                bool.TryParse(CMFieldAndVal.Split(':')[5], out staticspawnrate);
                                                CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate, maxspawns, spawnchance, staticspawnrate));
                                            }
                                        }
                                        else if (CMFieldAndVal.Split(':').Length > 4)
                                        {
                                            Region spawnregion = TShock.Regions.GetRegionByName(CMFieldAndVal.Split(':')[1]);
                                            int spawnrate;
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                            {
                                                int spawnchance = 1;
                                                int maxspawns = 5;
                                                Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxspawns);
                                                Int32.TryParse(CMFieldAndVal.Split(':')[4], out spawnchance);
                                                CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate, maxspawns, spawnchance));
                                            }
                                        }
                                        else if (CMFieldAndVal.Split(':').Length > 3)
                                        {
                                            Region spawnregion = TShock.Regions.GetRegionByName(CMFieldAndVal.Split(':')[1]);
                                            int spawnrate;
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                            {
                                                int maxspawns = 5;
                                                Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxspawns);
                                                CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate, maxspawns));
                                            }
                                        }
                                        else if (CMFieldAndVal.Split(':').Length > 2)
                                        {
                                            Region spawnregion = TShock.Regions.GetRegionByName(CMFieldAndVal.Split(':')[1]);
                                            int spawnrate;
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                            {
                                                CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate));
                                            }
                                        }
                                        break;
                                    }
                            case "replace":
                            case "replaces":
                                    {
                                        if (TShock.Utils.GetNPCByIdOrName(CMFieldAndVal.Split(':')[1]).Count == 1)
                                        {
                                            NPC npc = TShock.Utils.GetNPCByIdOrName(CMFieldAndVal.Split(':')[1])[0];
                                            CMType.Replaces.Add(npc);
                                        }
                                        break;
                                    }
                            case "ai":
                            case "customai":
                            case "customaistyle":
                            case "aistyle":
                                    {
                                        int ai;
                                        if(Int32.TryParse(CMFieldAndVal.Split(':')[1],out ai))
                                            CMType.CustomAIStyle = ai;
                                        break;
                                    }
                            case "multiplyondeath":
                                    {
                                        int MODML;
                                        if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out MODML))
                                        {
                                            CMType.MODMaxLevel = MODML;
                                            CMType.MultiplyOnDeath = false;
                                        }

                                        break;
                                    }
                            case "transform":
                            case "transformation":
                                    {
                                        if (CMFieldAndVal.Split(':').Length > 2)
                                        {
                                            int hp;
                                            if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out hp))
                                                CMType.Transformation = new Transformation(CMFieldAndVal.Split(':')[1], hp);
                                        }
                                        break;
                                    }
                            case "donttakedamage":
                                    {
                                        bool dtd;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out dtd))
                                            CMType.dontTakeDamage = dtd;
                                        break;
                                    }
                            case "lavaimmune":
                                    {
                                        bool lavaimmune;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out lavaimmune))
                                            CMType.lavaImmune = lavaimmune;
                                        break;
                                    }
                            case "boss":
                                    {
                                        bool boss;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out boss))
                                            CMType.Boss = boss;
                                        break;
                                    }
                            case "notilecollide":
                                    {
                                        bool noTileCollide;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out noTileCollide))
                                            CMType.noTileCollide = noTileCollide;
                                        break;
                                    }
                            case "nogravity":
                                    {
                                        bool noGravity;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out noGravity))
                                            CMType.noGravity = noGravity;
                                        break;
                                    }
                            case "value":
                                    {
                                        float value;
                                        if (float.TryParse(CMFieldAndVal.Split(':')[1], out value))
                                            CMType.Value = value;
                                        break;
                                    }
                            //case "spawnmessage":
                            //        {
                            //            CMType.SpawnMessage = CMFieldAndVal.Split(':')[1];
                            //            break;
                            //        }
                            case "onfire":
                                    {
                                        bool onfire;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out onfire))
                                            CMType.OnFire = onfire;
                                        break;
                                    }
                            case "poisoned":
                                    {
                                        bool poisoned;
                                        if (bool.TryParse(CMFieldAndVal.Split(':')[1], out poisoned))
                                            CMType.Poisoned = poisoned;
                                        break;
                                    }
                            default:
                                CMType.SpawnMessage = CMFieldAndVal;
                                        break;
                        }
                    }
                    #endregion

                }
                lock (CMTypes)
                {
                    if (CMType.Name != "" && CMType.BaseType > 0){
		    if (CMType.Transformation == null)
		     CMType.Transformation = new Transformation();
                        CMTypes.Add(CMType);
			}
                }
            }





        }

        private static void LoadAllCustomMonsters()
        {
            if (!Directory.Exists(Path.Combine(TShock.SavePath, "Custom Monsters")))
                Directory.CreateDirectory(Path.Combine(TShock.SavePath, "Custom Monsters"));
            Console.WriteLine("Loading Custom Monsters");
            LoadCustomMonstersFromText();
            SetupConfig();
        }
	private static void RemoveInactive()
	{
           for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (!Main.npc[i].active)
                {
		   for (int a = 0 ; a < CustomMonsters.Count ; a++)
		   {
			if (CustomMonsters[a].ID==i)
			{
		   	CustomMonsters.RemoveAt(a);
			break;
			}
		   }
		}
	    }
	}
}
}
