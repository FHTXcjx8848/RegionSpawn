using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using TShockAPI;
using JsonTool;
using System.IO;
using MySqlX.XDevAPI.Common;

namespace RegionSpawn
{
    public class Setting
    {
        public static bool CanFindPlayerInRegion(string name)
        {
            foreach(var region in GetConfig().SpawnRegions)
            {
                var plr = region.Players.Find(plr=>plr.TSPlayer.Name == name);
                if(plr != null)
                    return true;
            }
            return false;
        }
        public static RPlayer GetPlayer(string name)
        {
            foreach (var region in GetConfig().SpawnRegions)
            {
                var plr = region.Players.Find(plr => plr.TSPlayer.Name == name);
                if (plr != null)
                    return plr;
            }
            return null;
        }
        public static SpawnRegion FindRegionByPlayer(string name)
        {
            foreach (var region in GetConfig().SpawnRegions)
            {
                var plr = region.Players.Find(plr => plr.TSPlayer.Name == name);
                if (plr != null)
                    return region;
            }
            return null;
        }
        public List<SpawnRegion> SpawnRegions { get; set; }
        public static JsonRw<Setting> Config;
        public static Setting GetConfig()
        {
            return Config.ConfigObj;
        }
        public static readonly string ConfigPath = Path.Combine(AppContext.BaseDirectory, "Conifg.json");
        public static void InitConfig()
        {
            var setting = new Setting();
            var json = new JsonRw<Setting>(ConfigPath, setting);
            json.OnError += OnError;
            json.OnCreating += OnCreating;
            json.ConfigObj.SpawnRegions.ForEach(region =>
            {
                region.Restore();
            });
        }
        static void OnError(object sender, ErrorEventArgs e)
        {
            TShock.Log.ConsoleError("RegionSpawn插件错误配置读取错误:" + e.ToString());

        }
        static void OnCreating(object sender, CreatingEvent e)
        {
            TShock.Log.ConsoleError("正在创建RigionSpawn插件配置文件...");
        }
    }
    public class RPlayer
    {
        public TSPlayer TSPlayer { get; set; }
        public int Kills = 0;
        public int Deaths = 0;
    }
    public class Npc
    {
        [JsonProperty("生物ID")]
        public int ID = 0;
        //[JsonProperty("血量")]
        //public int Health = 0;
        [JsonProperty("数量")]
        public int Count = 0;
    }
    public class Round
    {
        [JsonProperty("生成点X")]
        public float X;
        [JsonProperty("生成点Y")]
        public float Y;
        [JsonProperty("生成范围")]
        public int Size;
        [JsonProperty("生成速率")]
        public int Rate;
        [JsonProperty("怪物列表")]
        public List<Npc> Npcs;
        [JsonProperty("时间")]
        public int Time = 0;
        [JsonIgnore]
        public int SecondCounter = 10;//倒计时
    }
    public class SpawnRegion
    {
        public bool IsInside(float x, float y)
        {
            return x >= RegionLeftX && x <= RegionRightX && y >= RegionLeftY && y <= RegionRightY;
        }
        public void Restore()
        {
            Start = false;
            PreStart = false;
            SecondCounter = StartSeconds;
            Players = new List<RPlayer>();
            CurrentRoundIndex = 0;
            Rounds.ForEach(round =>
            {
                round.SecondCounter = round.Time;
            });
            //CurrentMobIndex = 0;
        }
        public void SpawnMob()
        {
            var currentround = Rounds[CurrentRoundIndex];
            float currentx = (int)currentround.X << 4;//求出相对X点位置
            float currenty = (int)currentround.Y << 4;//求出相对Y点位置
            float distance = -1f;//距离
            foreach (var plr in Players)
            {
                if (plr.TSPlayer.Active && !plr.TSPlayer.Dead)
                {
                    float currentdistance = Math.Abs(plr.TSPlayer.TPlayer.position.X + (float)(plr.TSPlayer.TPlayer.width / 2) - (currentx + (float)(5 / 2))) + Math.Abs(plr.TSPlayer.TPlayer.position.Y + (float)(plr.TSPlayer.TPlayer.height / 2) - (currenty + (float)(5 / 2)));
                    if (distance == -1f || currentdistance < distance)
                    {
                        distance = currentdistance;
                        float x;
                        float y;
                        double angle;//角度

                        x = currentx - plr.TSPlayer.TPlayer.position.X;//求出生成点相对于玩家的位置
                        y = currenty - plr.TSPlayer.TPlayer.position.Y;//求出生成点相对于玩家的位置
                        if (!(x == 0 && y == 0))
                            if (Math.Abs(x) > (100 << 4) || Math.Abs(y) > (50 << 4))
                            {
                                angle = Math.Atan2(y, x) * 180 / Math.PI;//求出角度
                                x = (float)((100 << 4) * Math.Cos(angle * Math.PI / 180));//转为弧度
                                y = (float)((100 << 4) * Math.Sin(angle * Math.PI / 180));

                                currentx = x + plr.TSPlayer.TPlayer.position.X;
                                currenty = y + plr.TSPlayer.TPlayer.position.Y;
                            }
                    }
                }
            }
            Random random = new Random();
            var npc = currentround.Npcs[random.Next(currentround.Npcs.Count)];
            currentx = currentx / 16;
            currenty = currenty / 16;
            var tsnpc = TShock.Utils.GetNPCById(npc.ID);
            TSPlayer.Server.SpawnNPC(tsnpc.type, tsnpc.FullName, npc.Count, (int)currentx, (int)currenty, currentround.Size, currentround.Size);
        }
        [JsonIgnore]
        public List<RPlayer> Players;
        [JsonIgnore]
        public bool Start = false;//副本是否开启
        [JsonIgnore]
        public bool PreStart = false;//预开启倒计时
        [JsonIgnore]
        public int SecondCounter = 10;//倒计时
        [JsonIgnore]
        public int CurrentRoundIndex = 0;//当前round
        //[JsonIgnore]
        //public int CurrentMobIndex = 0;//当前Mob参数

        [JsonProperty("人数到后启动时间")]
        public int StartSeconds = 10;
        [JsonProperty("名称")]
        public string Name = "";
        [JsonProperty("权限")]
        public string Permission = "";
        [JsonProperty("出生点X")]
        public float SpawnX = 0;
        [JsonProperty("出生点Y")]
        public float SpawnY = 0;
        [JsonProperty("左上X")]
        public float RegionLeftX = 0;
        [JsonProperty("左下Y")]
        public float RegionLeftY = 0;
        [JsonProperty("右下X")]
        public float RegionRightX = 0;
        [JsonProperty("右下Y")]
        public float RegionRightY = 0;
        [JsonProperty("最小开启人数")]
        public int MinStartPlayer = 2;
        [JsonProperty("重生时间")]
        public int RespawnTime = 5;
        [JsonProperty("完成后执行")]
        public List<string> Commands = new List<string>();
        [JsonProperty("死亡可复活")]
        public bool Respawn = true;
        [JsonProperty("进度限制")]
        public int Progress = 0;
        [JsonProperty("波次")]
        public List<Round> Rounds = new List<Round>();
    }
}
