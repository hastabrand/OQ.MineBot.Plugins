﻿using System.Threading;
using OQ.MineBot.PluginBase.Base.Plugin.Tasks;
using OQ.MineBot.PluginBase.Pathfinding.Sub;

namespace CropFarmerPlugin.Tasks
{
    public class Store : ITask, ITickListener
    {
        public static readonly int[] FOOD = { 364, 412, 320, 424, 366, 393, 297 };

        private bool busy;
        private IChestMap chestMap;

        public override bool Exec() {
            return !status.entity.isDead && inventory.IsFull() && !status.eating &&
                   !busy && player.status.containers.GetWindow("minecraft:chest") == null;
        }

        public void OnTick() {

            // Check if this tick we should scan the
            // map for chests.
            if (chestMap == null) {
                Scan();
                return;
            }

            Deposite();
        }

        private void Deposite() {

            busy = true;
            ThreadPool.QueueUserWorkItem(obj => {
                var window = chestMap.Open(player, token);
                if (window != null) {
                    inventory.Deposite(window, FOOD);
                    player.tickManager.Register(3, () => {
                        actions.CloseContainer(window.id);
                        busy = false;
                    });
                } else {
                    player.tickManager.Register(3, () => { // Put a delay on chest open for 3 ticks.
                        busy = false;
                    });
                }
            });
        }

        private void Scan() {
            busy = true;
            chestMap = player.functions.CreateChestMap();
            chestMap.UpdateChestList(player, () => {
                busy = false;
            });
        }
    }
}