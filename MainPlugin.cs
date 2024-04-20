using System.Reflection;
using Newtonsoft.Json;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;
using System;
using System.Timers;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using System.Linq;

namespace RegionSpawn
{
    [ApiVersion(2, 1)]
    public class Plugin : TerrariaPlugin
    {
        public Plugin(Main game) : base(game)
        {
        }
        public override string Name
        {
            get
            {
                return "RegionSpawn";
            }
        }

        public override Version Version
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }
        public override string Author
        {
            get
            {
                return "Cjx";
            }
        }

        public override string Description
        {
            get
            {
                return "副本模式插件";
            }
        }
        public override void Initialize()
        {
            Setting.InitConfig();
            ServerApi.Hooks.ServerLeave.Register(this, new HookHandler<LeaveEventArgs>(this.OnPlayerLeave));
            ServerApi.Hooks.GameUpdate.Register(this, new HookHandler<EventArgs>(this.OnGameUpdate));
            TShockAPI.GetDataHandlers.PlayerSpawn += OnPlayerSpawn;
            TShockAPI.GetDataHandlers.KillMe += OnKillMe;
            ServerApi.Hooks.NpcKilled.Register(this, OnNpcKilled);
            Commands.ChatCommands.Add(new Command("regionspawn.use", PlayerCommand, "rs","副本","regionspawn"));
            Update.Elapsed += OnUpdate;
            Update.Start();
        }
        static readonly Timer Update = new Timer(1000);//秒时钟
        public static List<SpawnRegion> GetRegionList()
        {
            return Setting.GetConfig().SpawnRegions;
        }
        public bool Lock = false;//计时锁
        public void OnUpdate(object Sender, EventArgs e)
        {
            if (Lock) return;
            Lock = true;
            foreach (SpawnRegion region in GetRegionList())
            {
                if (region.Start)
                {
                    if (region.Players.Count >= region.MinStartPlayer)
                    {
                        var currentround = region.Rounds[region.CurrentRoundIndex];
                        region.SecondCounter--;
                        currentround.SecondCounter--;
                        bool flag = false;//判断转场
                        if (currentround.SecondCounter <= 0 && region.Rounds.Count != region.CurrentRoundIndex+1)
                        {
                            region.CurrentRoundIndex++;
                            region.SecondCounter = region.Rounds[region.CurrentRoundIndex].Rate;
                            foreach (var plr in region.Players)
                            {
                                plr.TSPlayer.Teleport(region.SpawnX * 16, region.SpawnY * 16);
                                plr.TSPlayer.SendErrorMessage($"第{region.CurrentRoundIndex}波终止!");
                                plr.TSPlayer.SendMessage($"第{region.CurrentRoundIndex+1}波开始!将持续{region.Rounds[region.CurrentRoundIndex].Time}秒!", Color.Blue);
                                if(region.Rounds.Count != region.CurrentRoundIndex + 1)
                                {
                                    plr.TSPlayer.SendErrorMessage($"最后一波!");
                                }
                            }
                            flag = true;
                            //换波数
                        }
                        else if (currentround.SecondCounter <= 0 && region.Rounds.Count == region.CurrentRoundIndex + 1)//结束
                        {
                            flag = true;
                            region.Players.Sort((max, min) => min.Kills.CompareTo(max.Kills));
                            foreach (var plr in region.Players)
                            {
                                plr.TSPlayer.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);//传送回畜生点
                                plr.TSPlayer.SendErrorMessage($"副本[{region.Name}]结束!");
                                plr.TSPlayer.SendInfoMessage($"你的击杀数:{plr.Kills}");
                                if (plr.Deaths > 0)
                                {
                                    plr.TSPlayer.SendInfoMessage($"你的死亡数:{plr.Deaths}");
                                }
                                plr.TSPlayer.SendInfoMessage($"Mvp击杀玩家[{region.Players.First().TSPlayer.Name}]数量:{region.Players.First().Kills}");
                            }
                            foreach (var cmd in region.Commands)
                            {
                                Commands.HandleCommand(TSPlayer.Server, cmd);
                                //执行指令部分
                            }
                            //正常副本结束部分
                            region.Restore();
                        }
                        else if(currentround.SecondCounter <= 5)//倒计时
                        {
                            foreach (var plr in region.Players)
                            {
                                plr.TSPlayer.SendErrorMessage($"[倒计时]第{region.CurrentRoundIndex+1}波还剩{currentround.SecondCounter}秒!");
                            }
                        }
                        if (region.SecondCounter <= 0 && !flag)//正常召唤
                        {
                            region.SecondCounter = currentround.Rate;
                            region.SpawnMob();
                        }
                    }
                    else
                    {
                        //开始后人数不足
                        foreach (var plr in region.Players)
                        {
                            plr.TSPlayer.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);//传送回畜生点
                            plr.TSPlayer.SendErrorMessage($"副本[{region.Name}]因人数不足而终止!");
                        }
                        region.Restore();
                    }
                }
                else
                {
                    if (region.Players.Count >= region.MinStartPlayer)
                    {
                        if (region.PreStart)
                        {
                            if(region.SecondCounter >= 0)
                            {
                                region.SecondCounter--;//开始倒计时
                            }
                            else
                            {
                                region.Start = true;
                                region.SecondCounter = region.Rounds[0].Rate;
                                foreach(var plr in region.Players)
                                {
                                    plr.TSPlayer.Teleport(region.SpawnX * 16, region.SpawnY * 16);
                                    plr.TSPlayer.SendInfoMessage($"副本[{region.Name}]开始!已将您传送至指定位置!");
                                    plr.TSPlayer.SendMessage($"第1波开始!将持续{region.Rounds[0].Time}秒!", Color.Blue);
                                }
                                //开启副本
                            }
                        }
                        else
                        {
                            TSPlayer.All.SendMessage($"副本[{region.Name}]将在{region.StartSeconds}后启动!", Color.Blue);
                            region.PreStart = true;
                            //开启副本开始倒计时
                        }
                    }
                    else
                    {
                        if (region.PreStart)
                        {
                            region.PreStart = false;
                            region.SecondCounter = region.StartSeconds;
                            //中断倒计时并初始化
                        }
                    }
                }
            }
            Lock = false;
        }
        private void OnNpcKilled(NpcKilledEventArgs args)//Npc被击杀
        {
            try
            {
                var tsplr = TShock.Players[args.npc.lastInteraction];
                if (Setting.CanFindPlayerInRegion(tsplr.Name))
                {
                    foreach (var region in Setting.GetConfig().SpawnRegions)
                    {
                        var plr = region.Players.Find(plr => plr.TSPlayer.Name == tsplr.Name);
                        if (plr != null)
                        {
                            plr.Kills++;
                            return;
                        }
                    }
                }
            }
            catch
            {

            }
        }
        private void OnKillMe(object sender, GetDataHandlers.KillMeEventArgs args)//玩家死亡
        {
            var tsplr = args.Player;
            int respawntime = 0;
            if (Setting.CanFindPlayerInRegion(tsplr.Name))
            {
                foreach (var region in Setting.GetConfig().SpawnRegions)
                {
                    tsplr.SendErrorMessage($"玩家[{tsplr.Name}]在挑战副本[{region.Name}]时被击败了");
                    var plr = region.Players.Find(plr => plr.TSPlayer.Name == tsplr.Name);
                    if (plr != null)
                    {
                        if (!region.Respawn)
                        {
                            tsplr.SendErrorMessage("你失败了!自动退出该副本!");
                            region.Players.Remove(plr);
                        }
                        else
                        {
                            plr.Deaths++;
                        }
                    }
                }
            }
            else
            {
                TSPlayer.All.SendErrorMessage($"玩家[{tsplr}]突然斯了");
            }
            args.Player.Spawn(0, respawntime);
            args.Handled = true;
        }
        private void OnPlayerSpawn(object sender, GetDataHandlers.SpawnEventArgs args)
        {
            var tsplr = args.Player;
            if (Setting.CanFindPlayerInRegion(tsplr.Name))
            {
                if (args.SpawnContext == PlayerSpawnContext.ReviveFromDeath)
                {
                    var region = Setting.FindRegionByPlayer(tsplr.Name);
                    tsplr.Teleport(region.SpawnX * 16, region.SpawnY * 16);
                    args.Handled = true;
                }
            }
        }
        private void OnGameUpdate(EventArgs args)//判断玩家是否在副本区域
        {
            foreach (TSPlayer tsplr in TShock.Players)
            {
                foreach (SpawnRegion region in GetRegionList())
                {
                    var find = region.Players.Find(p => p.TSPlayer.Name == tsplr.Name);
                    if (find != null && !region.IsInside(tsplr.X, tsplr.Y))
                    {
                        tsplr.Teleport(region.SpawnX * 16, region.SpawnY * 16);
                        tsplr.SendErrorMessage("检测到你离开副本范围!已将您送回副本重生点!");
                    }
                    if(find == null && region.IsInside(tsplr.X, tsplr.Y))
                    {
                        tsplr.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);
                        tsplr.SendErrorMessage("不得擅自进入副本区域!");
                    }
                }
            }
        }
        public static void PlayerCommand(CommandArgs args)//玩家指令部分
        {
            if(args.Parameters.Count < 1)
            {
                args.Player.SendInfoMessage("[i:4956]RegionSpawn副本系统[i:4956]\n-/rs(副本) list(列表),查看当前副本点列表\n-/rs(副本) <副本名称>,加入一个副本\n-/rs(副本) quit(退出),退出当前副本");
            }
            else
            {
                switch(args.Parameters[0])
                {
                    case "list"://副本列表指令
                    case "列表":
                        args.Player.SendInfoMessage($"[i:4956]RegionSpawn副本列表:[i:4956]");
                        foreach(var region in GetRegionList())
                        {
                            if (args.Player.HasPermission(region.Permission))
                            {
                                args.Player.SendInfoMessage($"[i:149]{region.Name}");
                            }
                        }
                        break;
                    case "quit"://退出副本指令
                    case "退出":
                        if (Setting.CanFindPlayerInRegion(args.Player.Name)) 
                        {
                            var currentregion = Setting.FindRegionByPlayer(args.Player.Name);
                            args.Player.Teleport(Main.spawnTileX * 16, Main.spawnTileY * 16);
                            currentregion.Players.Remove(Setting.GetPlayer(args.Player.Name));
                            args.Player.SendSuccessMessage("成功退出副本!");
                        }
                        else
                        {
                            args.Player.SendErrorMessage("你还未在一个副本当中!");
                        }
                        break;
                    default://加入副本指令

                        if (Setting.CanFindPlayerInRegion(args.Player.Name))
                        {
                            args.Player.SendErrorMessage("你已在一个副本中!");
                        }
                        else
                        {
                            string name = args.Parameters[0];
                            var region = GetRegionList().Find(r=> r.Name == name);
                            if(region != null && args.Player.HasPermission(region.Permission))
                            {
                                region.Players.Add(new RPlayer
                                {
                                    TSPlayer = args.Player,
                                    Kills = 0, 
                                    Deaths = 0
                                });
                                args.Player.SendSuccessMessage($"成功加入副本[{region.Name}]!");
                            }
                            else
                            {
                                args.Player.SendErrorMessage("不存在此副本!");
                            }
                        }
                        break;
                }
            }
        }
        public void OnPlayerLeave(LeaveEventArgs args)//玩家退服判断
        {
            var tsplr = TShock.Players[args.Who];
            try
            {
                foreach (SpawnRegion region in GetRegionList())
                {
                    var find = region.Players.Find(p => p.TSPlayer.Name == tsplr.Name);
                    if (find != null)
                    {
                        region.Players.Remove(find);
                        return;
                    }
                }
            }
            catch
            {
                TShock.Log.Error($"[RegionSpawn]玩家[{tsplr.Name}]退出异常!");
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
              
            }
            base.Dispose(disposing);
        }
    }
}