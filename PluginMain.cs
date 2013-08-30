using System;
using System.Collections.Generic;
using System.Threading;
using Terraria;
using Hooks;
using TShockAPI;
using TShockAPI.DB;
using System.ComponentModel;
using System.IO;
using System.Reflection;

namespace CustomMonsters
{
    [APIVersion(1, 12)]
    public class CustomMonstersPlugin : TerrariaPlugin
    {
        private static CustomMonsterConfigFile CMConfig { get; set; }

        internal static string CMConfigPath
        {
            get { return Path.Combine(TShock.SavePath, "cmconfig.json"); }
        }

        internal static string CustomMonstersDataDirectory
        {
            get { return Path.Combine(TShock.SavePath, "Custom Monsters"); }
        }

        internal static List<CMPlayer> CMPlayers = new List<CMPlayer>();
        internal static List<CustomMonster> CustomMonsters = new List<CustomMonster>();
        internal static List<CustomMonsterType> CMTypes = new List<CustomMonsterType>();
        public static DateTime Init;

        #region Shot tiles

        private static ShotTile TopLeft
        {
            get { return new ShotTile((float) (-Math.Sqrt(2)*8), (float) (-Math.Sqrt(2)*8)); }
        }

        private static ShotTile Top
        {
            get { return new ShotTile(0, -16); }
        }

        private static ShotTile TopRight
        {
            get { return new ShotTile((float) (Math.Sqrt(2)*8), (float) (-Math.Sqrt(2)*8)); }
        }

        private static ShotTile Left
        {
            get { return new ShotTile(-16, 0); }
        }

        private static ShotTile Center
        {
            get { return new ShotTile(0, 0); }
        }

        private static ShotTile Right
        {
            get { return new ShotTile(16, 0); }
        }

        private static ShotTile BottomLeft
        {
            get { return new ShotTile((float) (-Math.Sqrt(2)*8), (float) (Math.Sqrt(2)*8)); }
        }

        private static ShotTile Bottom
        {
            get { return new ShotTile(0, 16); }
        }

        private static ShotTile BottomRight
        {
            get { return new ShotTile((float) (Math.Sqrt(2)*8), (float) (Math.Sqrt(2)*8)); }
        }

        #endregion

        public override string Name
        {
            get { return "Custom Monsters Plugin"; }
        }

        public override string Author
        {
            get { return "Created by Vharonftw - Updated by IcyPhoenix"; }
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
                    int actualdmg = (dmg - Main.npc[npcid].defense/2)*critmultiply;
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
                                int monstersplit = SpawnCustomMonsterExactPosition(DeadMonster.CMType,
                                                                                   TShock.Players[killer].TileX + 1,
                                                                                   TShock.Players[killer].TileY,
                                                                                   (DeadMonster.MODLevel + 1));
                                int monstersplit2 = SpawnCustomMonsterExactPosition(DeadMonster.CMType,
                                                                                    TShock.Players[killer].TileX + 2,
                                                                                    TShock.Players[killer].TileY,
                                                                                    (DeadMonster.MODLevel + 1));
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
            lock (CMPlayers)
            {
                CMPlayers.Add(new CMPlayer(who));
            }
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
                CustomMonsterType CMType =
                    CMTypes.Find(cmt => cmt.Name.ToLower().StartsWith(args.Parameters[0].ToLower()));
                if (CMType != null)
                {
                    int count = 1;
                    if (args.Parameters.Count > 1)
                        Int32.TryParse(args.Parameters[1], out count);
                    int i = 0;
                    while (i < count)
                    {
                        SpawnCustomMonster(CMType, (int) args.Player.X + 48, (int) args.Player.Y);
                        i++;
                    }
                    TShock.Utils.Broadcast(args.Player.Name + " spawned " + count + " " + CMType.Name + "s",
                                           Color.Yellow);
                }
                else
                    args.Player.SendMessage("no Custom Monster Matched", Color.Red);
            }
        }

        private static void CMReload(CommandArgs args)
        {
            CMTypes.Clear();
            LoadAllCustomMonsters();
        }

        private static void CustomizeMonster(int npcid, CustomMonsterType CMType, int modlevel, int life = -1)
        {
            NPC Custom = Main.npc[npcid];
            Custom.netDefaults(CMType.BaseType);

            Custom.name = CMType.Name;
            Custom.displayName = CMType.Name;
            Custom.lifeMax = CMType.Life ?? Custom.lifeMax;
            Custom.life = life <= 0 ? (CMType.Life ?? Custom.life) : life;

            Custom.aiStyle = CMType.CustomAIStyle ?? Custom.aiStyle;
            Custom.dontTakeDamage = CMType.dontTakeDamage ?? Custom.dontTakeDamage;
            Custom.lavaImmune = CMType.lavaImmune ?? Custom.lavaImmune;
            Custom.boss = CMType.Boss ?? Custom.boss;
            Custom.noGravity = CMType.noGravity ?? Custom.noGravity;
            Custom.noTileCollide = CMType.noTileCollide ?? Custom.noTileCollide;

            Custom.value = CMType.Value ?? Custom.value;
            var fire = CMType.OnFire ?? Custom.onFire;
            var poison = CMType.Poisoned ?? Custom.poisoned;

            if( fire )
            {
                Custom.AddBuff(24, 6000);
                NetMessage.SendData(53, -1, -1, "", npcid, 24, 6000, 0.0f, 0);
                NetMessage.SendData(54, -1, -1, "", npcid, 0.0f, 0.0f, 0.0f, 0);
            }
            if (poison)
            {
                Custom.AddBuff(20, 6000);
                NetMessage.SendData(53, -1, -1, "", npcid, 20, 6000, 0.0f, 0);
                NetMessage.SendData(54, -1, -1, "", npcid, 0.0f, 0.0f, 0.0f, 0);
            }

            NetMessage.SendData(23, -1, -1, "", npcid, 0, 0, 0.0f, 0);

            if (modlevel == 0 && CMType.SpawnMessage != "")
                TShockAPI.TShock.Utils.Broadcast(CMType.SpawnMessage, Color.MediumPurple);
            CustomMonsters.Add(new CustomMonster(npcid, modlevel, CMType));
        }

        private static int SpawnCustomMonster(CustomMonsterType CMType, int X, int Y, int modlevel = 0)
        {
            int CID = -1;
            if (modlevel <= CMType.MODMaxLevel)
            {
                int npcid = NPC.NewNPC(X, Y, CMType.BaseType);
		        //Console.WriteLine(String.Format("id is {0} X {1} Y {2} - compare {3} and {4}",npcid,X,Y,Main.npc[1].position.X,Main.npc[1].position.Y));

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
                int npcid = NPC.NewNPC(spawnTileX*16, spawnTileY*16, CMType.BaseType);
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
                    DateTime now = DateTime.Now;
                    if (((now - shooter.LastShot[shooter.CMType.ShooterData.IndexOf(sd)]).TotalMilliseconds / 100) >= sd.ShootTime)
                    {
                        ShootProjectile(shooter.MainNPC.position.X, shooter.MainNPC.position.Y, sd.ShootStyle,
                                        sd.ProjectileDamage, shooter.ID, sd.ProjectileType);
                        shooter.LastShot[shooter.CMType.ShooterData.IndexOf(sd)] = now;                        
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
                    DateTime now = DateTime.Now;
                    if (((now - blitzer.LastBlitz[blitzer.CMType.BlitzData.IndexOf(bd)]).TotalMilliseconds / 100) >= bd.BlitzTime)
                    {
                        Blitz(blitzer.MainNPC.position.X, blitzer.MainNPC.position.Y, bd.BlitzStyle, bd.BlitzerType);
                        blitzer.LastBlitz[blitzer.CMType.BlitzData.IndexOf(bd)] = now;
                        
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
                    DateTime now = DateTime.Now;
                    if (cbd.CBlitzTime > 0 && (int)((now - Cblitzer.LastCblitz[Cblitzer.CMType.CBlitzData.IndexOf(cbd)]).TotalMilliseconds / 100) >= cbd.CBlitzTime)
                    {
                        CBlitz(Cblitzer.MainNPC.position.X, Cblitzer.MainNPC.position.Y, cbd.CBlitzStyle,
                                cbd.CBlitzerType);
                        Cblitzer.LastCblitz[Cblitzer.CMType.CBlitzData.IndexOf(cbd)] = now;
                    }
                }
            }
        }

        private static void HandleTransFormations()
        {
            List<CustomMonster> Transformers =
                CustomMonsters.FindAll(cm => cm.CMType.Transformation != null && cm.CMType.Transformation.transform);
            foreach (CustomMonster CM in Transformers)
            {
                if (CM.MainNPC.life <= CM.CMType.Transformation.HP)
                    try
                    {
                        CustomizeMonster(CM.ID, CM.CMType.Transformation.TransToType, 0, CM.MainNPC.life);
                    }
                    catch (NullReferenceException e)
                    {
                        Log.ConsoleError(e.ToString());
                        // TODO: track down this error and fix it for real.
                    }
            }
        }

        //Overall LaserGrid.Add
        //ShootStyle = 1
        //Top
        //ShootStyle = 2
        //Top, Bottom
        //ShootStyle = 3
        //Top, Bottom, Centre
        //ShootStyle = 4
        //Top, Bottom, Left, Right
        //ShootStyle = 5
        //Top, Bottom, Centre
        //ShootStyle = 6
        //Top, Bottom, TopLeft, TopRight, BottomLeft, BottomRight
        //ShootStyle = 7
        //Top, Bottom, Centre, TopLeft, TopRight, BottomLeft, BottomRight
        //ShootStyle = 8
        //Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight
        //ShootStyle = 9
        //Top, Bottom, Left, Right, TopLeft, TopRight, BottomLeft, BottomRight
        //ShootStyle = 10
        //Centre
        //ShootStyle = 11
        //Left, Right
        private static void ShootProjectile(float X, float Y, int ShootStyle, int ProjectileDamage, int npcid,
                                            int ProjectileType)
        {            
            if (ShootStyle > 0)
            {
                List<ShotTile> LaserGrid = new List<ShotTile>();
                if (ShootStyle >= 1 && ShootStyle <= 9)
                {
                    LaserGrid.Add(Top);
                    //ShootStyle > 1 | negates 1 from ShootStyle
                    //ShootStyle = 2, 3, 4, 5, 6, 7, 8 or 9 = True
                    //ShootStyle = 1 = False
                    if (ShootStyle > 1)
                        LaserGrid.Add(Bottom);
                    //ShootStyle%4 <= 1 | only ever true if its (1), 4, 8 or 9
                    //ShootStyle = {(1), 2, 3, 4, 5, 6, 7, 8, 9}
                    //ShootStyle%4 = {(1), 2, 3, 0, 1, 2, 3, 0, 1}
                    //ShootStyle > 1 | negates 1 from ShootStyle
                    //ShootStyle = 4, 8 or 9 = True
                    //ShootStyle = 1, 2, 3, 5, 6 or 7 = False
                    if ((ShootStyle%4) <= 1 && ShootStyle > 1)
                    {
                        LaserGrid.Add(Left);
                        LaserGrid.Add(Right);
                    }
                    //ShootStyle%2 == 1 | only ever true if its (1), 3, 5, 7 or 9
                    //ShootStyle = {(1), 2, 3, 4, 5, 6, 7, 8, 9}
                    //ShootStyle%2 = {(1), 0, 1, 0, 1, 0, 1, 0, 1}
                    //ShootStyle > 1 | negates 1 from ShootStyle
                    //ShootStyle = 3, 5, 7 or 9 = True
                    //ShootStyle = 1, 2, 4, 6 or 8 = False
                    if (ShootStyle > 1 && ShootStyle%2 == 1)
                        LaserGrid.Add(Center);
                    //ShootStyle > 1 | negates 1, 2, 3, 4 from ShootStyle
                    //ShootStyle = 6, 7, 8 or 9 = True
                    //ShootStyle = 1, 2, 3, 4 or 5 = False
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
                        Vector2 Start = new Vector2(X + (2*bt.X) + 10, Y + (2*bt.Y) + 23);
                        //Vector2 Target = Main.player[targetid].position;
                        //Vector2 Start = new Vector2(X + (2 * bt.X) + 10, Y + (2 * bt.Y) + 23);
                        float initY = Target.Y - Start.Y;
                        float initX = Target.X - Start.X;
                        int parityX = initX < 0 ? -1 : 1;
                        int parityY = initY < 0 ? -1 : 1;
                        float VelocityY = 
                            (float) (10*Math.Sqrt(1 - (Math.Pow(initX, 2)/(Math.Pow(initX, 2) + Math.Pow(initY, 2)))))*
                            parityY;
                        float VelocityX =
                            (float) (10*Math.Sqrt(1 - (Math.Pow(initY, 2)/(Math.Pow(initX, 2) + Math.Pow(initY, 2)))))*
                            parityX;
                        //TShock.Utils.Broadcast("VelocityX = " + VelocityX.ToString());
                        //TShock.Utils.Broadcast("VelocityY = " + VelocityY.ToString());

                        if (Collision.CanHit(Start, 4, 4, Target, Main.player[targetid].width,
                                             Main.player[targetid].height))
                        {                            
                            int New = Projectile.NewProjectile(Start.X, Start.Y, VelocityX, VelocityY, ProjectileType, ProjectileDamage, (float) 0.5);
                            //doesn't actually work on client side - stops monsters from shooting themselves thou.
                            Main.projectile[New].friendly = false;
                            Main.projectile[New].hostile = true;
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
                if ((blitzstyle%4) <= 1 && blitzstyle > 1)
                {
                    BlitzGrid.Add(Left);
                    BlitzGrid.Add(Right);
                }
                if (blitzstyle > 1 && blitzstyle%2 == 1)
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
                    int blitzshot = NPC.NewNPC((int) (X + (2*bt.X) + 10), (int) (Y + (2*bt.Y) + 23), blitztype, 0);
                    Main.npc[blitzshot].SetDefaults(blitztype);
                }
            }
        }

        private static void CBlitz(float X, float Y, int blitzstyle, string SCMType)
        {
            CustomMonsterType CMType = CMTypes.Find(cmt => cmt.Name == SCMType);
            if (CMType == null)
            {
                Log.ConsoleError("The cmtype could not be found\n");
                return;
            }

            if (blitzstyle > 0)
            {
                List<ShotTile> BlitzGrid = new List<ShotTile>();
                BlitzGrid.Add(Top);
                if (blitzstyle > 1)
                    BlitzGrid.Add(Bottom);
                if ((blitzstyle%4) <= 1 && blitzstyle > 1)
                {
                    BlitzGrid.Add(Left);
                    BlitzGrid.Add(Right);
                }
                if (blitzstyle > 1 && blitzstyle%2 == 1)
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
                        int blitzshot = NPC.NewNPC((int) (X + (2*bt.X) + 10), (int) (Y + (2*bt.Y) + 23), CMType.BaseType,
                                                   0);
                        CustomizeMonster(blitzshot, CMType, 0);
                    }
                }
            }
        }

        private static void SpawnInZones()
        {
            List<CustomMonsterType> Corruption = new List<CustomMonsterType>();
            List<BiomeData> CorruptionData = new List<BiomeData>();
            List<CustomMonsterType> Dungeon = new List<CustomMonsterType>();
            List<BiomeData> DungeonData = new List<BiomeData>();
            List<CustomMonsterType> Meteor = new List<CustomMonsterType>();
            List<BiomeData> MeteorData = new List<BiomeData>();
            List<CustomMonsterType> Jungle = new List<CustomMonsterType>();
            List<BiomeData> JungleData = new List<BiomeData>();
            List<CustomMonsterType> Hallow = new List<CustomMonsterType>();
            List<BiomeData> HallowData = new List<BiomeData>();
            List<CustomMonsterType> Forest = new List<CustomMonsterType>();
            List<BiomeData> ForestData = new List<BiomeData>();

            List<CustomMonsterType> cmtypes = CMTypes.FindAll(cmtype => cmtype.BiomeData.Count > 0);
            foreach (CustomMonsterType cmtype in cmtypes)
            {
                CustomMonsterType tempcmtype = cmtype;
                foreach (BiomeData bd in cmtype.BiomeData)
                {                    
                    switch (bd.biomeName.ToLower())
                    {
                        case "corruption":
                        {
                            Corruption.Add(cmtype);
                            CorruptionData.Add(bd);
                            break;
                        }
                        case "dungeon":
                        {
                            Dungeon.Add(cmtype);
                            DungeonData.Add(bd);
                            break;
                        }
                        case "meteor":
                        {
                            Meteor.Add(cmtype);
                            MeteorData.Add(bd);
                            break;
                        }case "jungle":
                        {
                            Jungle.Add(cmtype);
                            JungleData.Add(bd);
                            break;
                        }case "hallow":
                        {
                            Hallow.Add(cmtype);
                            HallowData.Add(bd);
                            break;
                        }case "forest":
                        {
                            Forest.Add(tempcmtype);  
                            ForestData.Add(bd);                                                
                            break;
                        }
                    }
                }
            }
            List<CMPlayer> CorruptionPlayers =
                CMPlayers.FindAll(player => player.TSPlayer != null && player.TSPlayer.TPlayer.zoneEvil == true);
            List<CMPlayer> DungeonPlayers =
                CMPlayers.FindAll(player => player.TSPlayer != null && player.TSPlayer.TPlayer.zoneDungeon == true);
            List<CMPlayer> MeteorPlayers =
                CMPlayers.FindAll(player => player.TSPlayer != null && player.TSPlayer.TPlayer.zoneMeteor == true);
            List<CMPlayer> HallowPlayers =
                CMPlayers.FindAll(player => player.TSPlayer != null && player.TSPlayer.TPlayer.zoneHoly == true);
            List<CMPlayer> JunglePlayers =
                CMPlayers.FindAll(player => player.TSPlayer != null && player.TSPlayer.TPlayer.zoneJungle == true);
            List<CMPlayer> ForestPlayers =
                CMPlayers.FindAll(
                    player =>
                    player.TSPlayer != null &&
                    (player.TSPlayer.TPlayer.zoneJungle == false && player.TSPlayer.TPlayer.zoneDungeon == false &&
                        player.TSPlayer.TPlayer.zoneMeteor == false && player.TSPlayer.TPlayer.zoneEvil == false));

            BiomeSpawn(Corruption, CorruptionData, CorruptionPlayers);
            BiomeSpawn(Dungeon, DungeonData, DungeonPlayers);
            BiomeSpawn(Meteor, MeteorData, MeteorPlayers);
            BiomeSpawn(Jungle, JungleData, JunglePlayers);
            BiomeSpawn(Hallow, HallowData, HallowPlayers);
            BiomeSpawn(Forest, ForestData, ForestPlayers);
        }

        private static void BiomeSpawn(List<CustomMonsterType> BiomeType, List<BiomeData> BiomeData, List<CMPlayer> BiomePlayer)
        {
            if (BiomeType.Count > 0 && BiomePlayer.Count > 0)
            {
                lock (BiomePlayer)
                {
                    foreach (CMPlayer player in BiomePlayer)
                    {
                        Random mt = new Random();
                        Random mc = new Random();
                        player.NPCIDs.RemoveAll(id => Main.npc[id].active == false);                        
                        if (player.NPCIDs.Count < CMConfig.MaxCustomSpawns)
                        {
                            if ((DateTime.Now - player.LastCustomZoneSpawn).TotalMilliseconds > CMConfig.CustomSpawnRate)
                            {
                                lock (BiomeType)
                                {
                                    int tempIndex = mt.Next() % BiomeType.Count;
                                    CustomMonsterType cmtype = BiomeType[tempIndex];
                                    if ((int)((DateTime.Now - player.LastCustomZoneSpawn).TotalMilliseconds / 100) >= BiomeData[tempIndex].biomeRate)
                                    {
                                        if (mc.Next(100) > BiomeData[tempIndex].biomeSpawnChance)
                                        {
                                            player.LastCustomZoneSpawn = DateTime.Now;
                                        }
                                        else
                                        {
                                            int spawnTileX;
                                            int spawnTileY;
                                            TShock.Utils.GetRandomClearTileWithInRange((int)player.TSPlayer.TileX, (int)player.TSPlayer.TileY, 50, 20, out spawnTileX, out spawnTileY);
                                            int NPCID = SpawnCustomMonster(cmtype, spawnTileX * 16, spawnTileY * 16);
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
            }
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
                            try
                            {
                                var name = Main.npc[i].name;
                                if (name != null && npc.name.ToLower() == name.ToLower())
                                {

                                    CustomizeMonster(i, cmt, 0);
                                    //Main.npc[i].active=true;
                                    //NetMessage.SendData(23, -1, -1, "", i, 0f, 0f, 0f, 0);
                                }
                            }
                            catch (Exception e)
                            {
                                Log.ConsoleError(e.ToString());
                            }
                        }
                    }
                    if (CMType.Name != "" && CMType.BaseType != 0)
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
                    if (r.Next(100) % SR.SpawnChance == 0)
                    {
                        if ((int)((DateTime.Now - SR.LastSpawn).TotalMilliseconds / 100) >= SR.SpawnRate)
                        {
                            List<CMPlayer> plysInRegion = SR.PlayersInRegion;
                            if (plysInRegion != null)
                            {
                                if (plysInRegion.Count > 0)
                                {
                                    if (SR.MonstersInRegion != null)
                                    {
                                        //Log.ConsoleError("monster in region: " + SR.MonstersInRegion.Count.ToString() + " Max Spawns: " + SR.MaxSpawns);
                                        if (SR.MonstersInRegion.Count < SR.MaxSpawns)
                                        {
                                            int xRandom;
                                            int yRandom;
                                            int x = (int)SR.PlayersInRegion[0].TSPlayer.TileX;
                                            int y = (int)SR.PlayersInRegion[0].TSPlayer.TileY;
                                            int outx;
                                            int outy;
                                            xRandom = x - TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Left;
                                            if (xRandom > TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Right)
                                                xRandom = TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Right - TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Left;
                                            if (xRandom > 10)
                                                xRandom = 10;
                                            yRandom = y - TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Top;
                                            if (yRandom > TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Bottom)
                                                yRandom = TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Bottom - TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Top;
                                            if (yRandom > 10)
                                                yRandom = 10;
                                            TShock.Utils.GetRandomClearTileWithInRange(x, y, xRandom, yRandom, out outx, out outy);
                                            //while (outx < TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Left && outx > TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Right && outy < TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Top + 1 && outy > TShock.Regions.GetRegionByName(SR.SpawnRegion).Area.Bottom)
                                            //{
                                            //    TShock.Utils.GetRandomClearTileWithInRange(x, y, 5, 5, out outx, out outy);
                                            //}
                                            SpawnCustomMonsterExactPosition(RS, outx, outy);
                                            SR.LastSpawn = DateTime.Now;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        SR.LastSpawn = DateTime.Now;
                    }
                }
            }
        }

        private static void HandleBuffers()
        {
            List<CustomMonster> Buffers = CustomMonsters.FindAll(CM => CM.CMType.Buffs.Count > 0);
            foreach (CustomMonster buffer in Buffers)
            {
                List<CMPlayer> BuffThese = CMPlayers.FindAll(ply => (new Rectangle((int)buffer.MainNPC.position.X, (int)buffer.MainNPC.position.Y, buffer.MainNPC.width, buffer.MainNPC.height)).Intersects(new Rectangle((int)ply.TSPlayer.TPlayer.position.X, (int)ply.TSPlayer.TPlayer.position.Y, ply.TSPlayer.TPlayer.width, ply.TSPlayer.TPlayer.height)));
                foreach (CMPlayer buffthis in BuffThese)
                {
                    foreach (BuffRateandDuration buff in buffer.CMType.Buffs)
                    {
                        Random r = new Random();
                        if (r.Next()%buff.Rate == 0)
                            buffthis.TSPlayer.SetBuff(buff.BuffType, buff.BuffTime*60);
                    }
                }
            }
        }

    private static void LoadCustomMonstersFromText()
    {
        LoadCustomMonstersFromDir(@CustomMonstersDataDirectory);
    }

    private static void LoadCustomMonstersFromDir(String directory)
    {
        string[] dirs = Directory.GetDirectories(directory);
        foreach (string dir in dirs)
        {
            LoadCustomMonstersFromDir(dir);
        }

        LoadCustomMonstersFromFile(directory);
    }

    private static void LoadCustomMonstersFromFile(String directory)
    {
        string[] CustomMonstersDataPaths = Directory.GetFiles(directory);

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
                        if (!line.StartsWith("##") && !line.StartsWith("#"))
                            MonsterData.Add(line);
                    }
                }
                CreateCustomMonster(MonsterData);
            }
            catch (Exception e)
            {
                string errormessage = string.Format("The file \"{0}\" could not be read:", CMDataPath);
                Console.WriteLine(errormessage);
                Console.WriteLine(e.Message);
            }
        }
    }

    private static void CreateCustomMonster(List<string> MonsterData)
    {
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
                                if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out type) &&
                                    Int32.TryParse(CMFieldAndVal.Split(':')[2], out style) &&
                                    Int32.TryParse(CMFieldAndVal.Split(':')[3], out time))
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
                                if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out style) &&
                                    Int32.TryParse(CMFieldAndVal.Split(':')[3], out time))
                                {
                                    CMType.CBlitzData.Add(new CBlitzData(CMFieldAndVal.Split(':')[1], style,
                                                                            time));
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
                                if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out type) &&
                                    Int32.TryParse(CMFieldAndVal.Split(':')[2], out style) &&
                                    Int32.TryParse(CMFieldAndVal.Split(':')[3], out time) &&
                                    Int32.TryParse(CMFieldAndVal.Split(':')[4], out damage))
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
                    case "biome":
                        {
                            string name;
                            int maxSpawns = -1;
                            int rate;
                            int spawnChance = 100;                            
                            bool staticSpawnRate = false;
                            if (CMFieldAndVal.Split(':').Length > 2)
                            {
                                name = CMFieldAndVal.Split(':')[1];
                                Int32.TryParse(CMFieldAndVal.Split(':')[2], out rate);
                                Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxSpawns);                                
                                Int32.TryParse(CMFieldAndVal.Split(':')[4], out spawnChance);
                                bool.TryParse(CMFieldAndVal.Split(':')[5], out staticSpawnRate);
                                CMType.BiomeData.Add(new BiomeData(name, maxSpawns, rate, spawnChance, staticSpawnRate));
                            }
                            break;
                        }
                    /*case "corruption":
                        {
                            int rate = 10;
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
                        }*/
                    case "region":
                        {
                            if (CMFieldAndVal.Split(':').Length > 5)
                            {
                                string spawnregion = CMFieldAndVal.Split(':')[1];
                                int spawnrate;
                                if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                {
                                    int spawnchance = 1;
                                    int maxspawns = 5;
                                    bool staticspawnrate = false;
                                    Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxspawns);
                                    Int32.TryParse(CMFieldAndVal.Split(':')[4], out spawnchance);
                                    bool.TryParse(CMFieldAndVal.Split(':')[5], out staticspawnrate);
                                    CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate,
                                                                                maxspawns,
                                                                                spawnchance, staticspawnrate));
                                }
                            }
                            else if (CMFieldAndVal.Split(':').Length > 4)
                            {
                                string spawnregion = CMFieldAndVal.Split(':')[1];
                                int spawnrate;
                                if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                {
                                    int spawnchance = 1;
                                    int maxspawns = 5;
                                    Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxspawns);
                                    Int32.TryParse(CMFieldAndVal.Split(':')[4], out spawnchance);
                                    CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate,
                                                                                maxspawns,
                                                                                spawnchance));
                                }
                            }
                            else if (CMFieldAndVal.Split(':').Length > 3)
                            {
                                string spawnregion = CMFieldAndVal.Split(':')[1];
                                int spawnrate;
                                if (Int32.TryParse(CMFieldAndVal.Split(':')[2], out spawnrate))
                                {
                                    int maxspawns = 5;
                                    Int32.TryParse(CMFieldAndVal.Split(':')[3], out maxspawns);
                                    CMType.SpawnRegions.Add(new RegionAndRate(spawnregion, spawnrate,
                                                                                maxspawns));
                                }
                            }
                            else if (CMFieldAndVal.Split(':').Length > 2)
                            {
                                string spawnregion = CMFieldAndVal.Split(':')[1];
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
                            if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out ai))
                                CMType.CustomAIStyle = ai;
                            break;
                        }
                    case "multiplyondeath":
                        {
                            int MODML;
                            if (Int32.TryParse(CMFieldAndVal.Split(':')[1], out MODML))
                            {
                                CMType.MODMaxLevel = MODML;
                                CMType.MultiplyOnDeath = true;
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
                                    CMType.Transformation = new Transformation(CMFieldAndVal.Split(':')[1],
                                                                                hp);
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
                        CMType.SpawnMessage = CMFieldAndVal.Split(':')[1];
                        break;
                }
            }

            #endregion

        }
        lock (CMTypes)
        {

            if (CMType.Name != "" && CMType.BaseType != 0)
            {

                if (CMType.Transformation == null)

                    CMType.Transformation = new Transformation();

                CMTypes.Add(CMType);

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
