﻿using AmeisenBotX.Core.Character;
using AmeisenBotX.Core.Character.Inventory.Enums;
using AmeisenBotX.Core.Character.Inventory.Objects;
using AmeisenBotX.Core.Common;
using AmeisenBotX.Core.Common.Enums;
using AmeisenBotX.Core.Data;
using AmeisenBotX.Core.Data.Objects.WowObject;
using AmeisenBotX.Core.Hook;
using AmeisenBotX.Core.Movement;
using AmeisenBotX.Pathfinding;
using AmeisenBotX.Pathfinding.Objects;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmeisenBotX.Core.StateMachine.States
{
    public class StateSelling : State
    {
        public StateSelling(AmeisenBotStateMachine stateMachine, AmeisenBotConfig config, ObjectManager objectManager, HookManager hookmanager, CharacterManager characterManager, IPathfindingHandler pathfindingHandler, IMovementEngine movementEngine) : base(stateMachine)
        {
            Config = config;
            ObjectManager = objectManager;
            HookManager = hookmanager;
            CharacterManager = characterManager;
            PathfindingHandler = pathfindingHandler;
            MovementEngine = movementEngine;
        }

        private CharacterManager CharacterManager { get; }

        private AmeisenBotConfig Config { get; }

        private IMovementEngine MovementEngine { get; set; }

        private ObjectManager ObjectManager { get; }

        private HookManager HookManager { get; }

        private IPathfindingHandler PathfindingHandler { get; }

        private int TryCount { get; set; }

        public override void Enter()
        {

        }

        public override void Execute()
        {
            if (HookManager.GetFreeBagSlotCount() > 4
               || !CharacterManager.Inventory.Items.Any(e => e.Price > 0))
            {
                AmeisenBotStateMachine.SetState(AmeisenBotState.Idle);
                return;
            }

            WowUnit selectedUnit = ObjectManager.WowObjects.OfType<WowUnit>()
                .OrderBy(e => e.Position.GetDistance(ObjectManager.Player.Position))
                .FirstOrDefault(e => e.GetType() != typeof(WowPlayer) && e.IsRepairVendor && e.Position.GetDistance(ObjectManager.Player.Position) < 50);

            if (selectedUnit != null && !selectedUnit.IsDead)
            {
                double distance = ObjectManager.Player.Position.GetDistance(selectedUnit.Position);
                if (distance > 5.0)
                {
                    if (MovementEngine.CurrentPath?.Count < 2 || TryCount > 2)
                    {
                        TryCount = 0;
                        CharacterManager.Jump();
                        BuildNewPath(selectedUnit.Position);
                    }

                    if (MovementEngine.CurrentPath?.Count > 0)
                    {
                        if (MovementEngine.GetNextStep(ObjectManager.Player.Position, ObjectManager.Player.Rotation, out Vector3 positionToGoTo, out bool needToJump))
                        {
                            CharacterManager.MoveToPosition(positionToGoTo, 10f, 1.2f);

                            if (needToJump)
                            {
                                CharacterManager.Jump();
                                DoRandomUnstuckMovement();
                                TryCount++;
                            }
                        }
                    }
                }
                else
                {
                    if (distance > 4)
                    {
                        CharacterManager.InteractWithUnit(selectedUnit, 20.9f, 2f);
                    }
                    else
                    {
                        HookManager.RightClickUnit(selectedUnit);
                        Task.Delay(1000).GetAwaiter().GetResult();

                        HookManager.SellAllGrayItems();
                        foreach (IWowItem item in CharacterManager.Inventory.Items.Where(e => e.Price > 0))
                        {
                            IWowItem itemToSell = item;
                            if (CharacterManager.IsItemAnImprovement(item, out IWowItem itemToReplace))
                            {
                                itemToSell = itemToReplace;
                                HookManager.ReplaceItem(null, item);
                            }

                            HookManager.UseItemByBagAndSlot(itemToSell.BagId, itemToSell.BagSlot);
                        }
                    }
                }
            }
            else
            {
                AmeisenBotStateMachine.SetState(AmeisenBotState.Idle);
            }
        }

        private void DoRandomUnstuckMovement()
        {
            Random rnd = new Random();
            if (rnd.Next(10) >= 5)
            {
                BotUtils.SendKey(AmeisenBotStateMachine.XMemory.Process.MainWindowHandle, new IntPtr((int)VirtualKeys.VK_A), 300, 600);
            }
            else
            {
                BotUtils.SendKey(AmeisenBotStateMachine.XMemory.Process.MainWindowHandle, new IntPtr((int)VirtualKeys.VK_S), 300, 600);
            }

            if (rnd.Next(10) >= 5)
            {
                BotUtils.SendKey(AmeisenBotStateMachine.XMemory.Process.MainWindowHandle, new IntPtr((int)VirtualKeys.VK_Q), 300, 600);
            }
            else
            {
                BotUtils.SendKey(AmeisenBotStateMachine.XMemory.Process.MainWindowHandle, new IntPtr((int)VirtualKeys.VK_E), 300, 600);
            }
        }

        public override void Exit()
        {
            MovementEngine.CurrentPath.Clear();
        }

        private void BuildNewPath(Vector3 corpsePosition)
        {
            List<Vector3> path = PathfindingHandler.GetPath(ObjectManager.MapId, ObjectManager.Player.Position, corpsePosition);
            MovementEngine.LoadPath(path);
            MovementEngine.PostProcessPath();
        }
    }
}