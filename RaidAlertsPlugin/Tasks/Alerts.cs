﻿using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq;
using System.Text;
using OQ.MineBot.PluginBase;
using OQ.MineBot.PluginBase.Base.Plugin.Tasks;
using OQ.MineBot.PluginBase.Classes;
using OQ.MineBot.PluginBase.Classes.Entity;
using OQ.MineBot.PluginBase.Classes.Entity.Mob;
using OQ.MineBot.PluginBase.Classes.Entity.Player;
using OQ.MineBot.PluginBase.Classes.Physics;
using OQ.MineBot.PluginBase.Utility;
using OQ.MineBot.Protocols.Classes.Base;

namespace RaidAlertsPlugin.Tasks
{
    public class Alerts : ITask
    {
        private const int DLAMP = 123; //Disabled lamp.
        private const int ELAMP = 124; //Enabled lamp.            

        private readonly ulong              discord;
        private readonly bool               local;
        private readonly bool               explosion;
        private readonly bool               wither;
        private readonly bool               creeper;
        private readonly bool               players;
        private readonly string             friendly;
        private readonly ILocation[]        lamps;
        private readonly DiscordHelper.Mode mode;
        
        public Alerts(ulong discord, bool local, bool explosion, bool wither, bool creeper, bool players, string friendly, ILocation[] lamps, DiscordHelper.Mode mode) {
            this.discord   = discord;
            this.local     = local;
            this.explosion = explosion;
            this.wither    = wither;
            this.creeper   = creeper;
            this.players   = players;
            this.friendly  = friendly;
            this.lamps     = lamps;
            this.mode      = mode;
        }

        public override bool Exec() { return true; }
        public override void Start() {
            player.events.onExplosion     += OnExplosion;
            player.entities.onEntityAdded += OnEntityAdded;
            player.events.onBlockChanged  += OnBlockChanged;
        }
        public override void Stop() {
            player.events.onExplosion     -= OnExplosion;
            player.entities.onEntityAdded -= OnEntityAdded;
            player.events.onBlockChanged  -= OnBlockChanged;
        }

        private void OnBlockChanged(IPlayer player, ILocation location, ushort oldId, ushort newId)  {
            
            //Check if it is a lamp.
            if (newId != ELAMP) {
                //Check if this location is tracked.
                var tracked = lamps.Any(x => x.Compare(location));
                if (!tracked) return;

                //Notify the user as the this block is a disabled lamp
                //and is tracked.
                NotifyUser(
                    ApplyVariables("Unpowered lamp detected", player.status.entity.location.ToLocation(0), location, ""), 10,
                    4);
            }
        }

        private void OnEntityAdded(IEntity entity, EventCancelToken token) {
            
            // Check for errors.
            if (entity == null) return;

            //Check if it's a player.
            var isPlayer = entity.GetType().GetInterfaces().Contains(typeof (IPlayerEntity));
            if (isPlayer && players) {
                var playerEntity = (IPlayerEntity) entity;
                var friendlyFixed = this.friendly.Replace("-", "");
                var friendly = friendlyFixed.Split(' ');
                if (!friendly.Contains(playerEntity.uuid, StringComparer.CurrentCultureIgnoreCase)) {
                    var name = player.entities.FindNameByUuid(playerEntity?.uuid);
                    if(name?.Name != null && !friendly.Contains(name.Name))
                        NotifyUser(ApplyVariables("A player has been detected.", player.status.entity.location.ToLocation(0), playerEntity.location.ToLocation(0), name.Name), 4, 3);
                }
            }
            else {
                var mobEntity = entity as IMobEntity;
                if (mobEntity == null) return;

                if(mobEntity.type == MobType.Wither && wither)
                    NotifyUser(ApplyVariables("A wither has been detected.", player.status.entity.location.ToLocation(0), mobEntity.location.ToLocation(0), "Wither"), 10, 2);
                else if(mobEntity.type == MobType.Creeper && creeper)
                    NotifyUser(ApplyVariables("A creeper has been detected.", player.status.entity.location.ToLocation(0), mobEntity.location.ToLocation(0), "Creeper"), 4, 1);
            }
        }

        private void OnExplosion(IPlayer player, float X, float Y, float Z) {
            if (explosion)
                NotifyUser(ApplyVariables("An explosion has been detected.", player.status.entity.location.ToLocation(0), new Location((int)Math.Round(X), Y, (int)Math.Round(Z)), ""), 10, 0);
        }

        private static readonly ConcurrentDictionary<int, DateTime> IDLIMIT = new ConcurrentDictionary<int, DateTime>();
        private void NotifyUser(string[] body, int priority, int id) {
            
            if (IDLIMIT.ContainsKey(id) && IDLIMIT[id].Subtract(DateTime.Now).TotalMilliseconds > 0) return;
            IDLIMIT.AddOrUpdate(id, i => DateTime.Now.AddSeconds(30 / priority), (i, time) => DateTime.Now.AddSeconds(30 / priority));

            if (local) DiscordHelper.AlertMessage(discord, "Raid Alert!", body[0], body[1], priority >= 5, priority, mode);
            else       DiscordHelper.SendMessage (discord, "Raid Alert!", body[0], body[1], priority >= 5, mode);
        }

        private string[] ApplyVariables(string text, ILocation playerLocation, ILocation targetLocation, string targetName) {

            StringBuilder builder = new StringBuilder();
            builder.AppendLine("**"+text+"**");
            builder.AppendLine();
            if (!string.IsNullOrWhiteSpace(targetName)) builder.AppendLine("Name: *'" + targetName + "'*");
            if (targetLocation != null) builder.AppendLine("Location: *'" + targetLocation + "' ("+ Math.Floor(Math.Abs(targetLocation.Distance(playerLocation))) + " blocks away)*");
            builder.AppendLine("Bot name: *'" + status.username + "'*");
            return new string[] {text, builder.ToString()};
        }
    }
}