﻿using GTANetworkAPI;
using WiredPlayers.globals;
using WiredPlayers.model;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using System;

namespace WiredPlayers.garbage
{
    public class Garbage : Script
    {
        private static Dictionary<int, Timer> garbageTimerList = new Dictionary<int, Timer>();

        public static void OnPlayerDisconnected(Client player, DisconnectionType type, string reason)
        {
            if (garbageTimerList.TryGetValue(player.Value, out Timer garbageTimer) == true)
            {
                garbageTimer.Dispose();
                garbageTimerList.Remove(player.Value);
            }
        }

        private void RespawnGarbageVehicle(Vehicle vehicle)
        {
            NAPI.Vehicle.RepairVehicle(vehicle);
            NAPI.Entity.SetEntityPosition(vehicle, NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_POSITION));
            NAPI.Entity.SetEntityRotation(vehicle, NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_ROTATION));
        }

        private void OnGarbageTimer(object playerObject)
        {
            Client player = (Client)playerObject;
            Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
            Vehicle vehicle = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_VEHICLE);
            
            RespawnGarbageVehicle(vehicle);

            // Cancel the garbage route
            NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_VEHICLE);
            NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_CHECKPOINT);
            NAPI.Data.ResetEntityData(target, EntityData.PLAYER_JOB_CHECKPOINT);
            
            if (garbageTimerList.TryGetValue(player.Value, out Timer garbageTimer) == true)
            {
                // Remove the timer
                garbageTimer.Dispose();
                garbageTimerList.Remove(player.Value);
            }

            // Send the message to both players
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_JOB_VEHICLE_ABANDONED);
            NAPI.Chat.SendChatMessageToPlayer(target, Constants.COLOR_ERROR + Messages.ERR_JOB_VEHICLE_ABANDONED);
        }

        private void OnGarbageCollectedTimer(object playerObject)
        {
            Client player = (Client)playerObject;
            Client driver = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);

            // Get garbage bag
            GTANetworkAPI.Object garbageBag = NAPI.Data.GetEntityData(player, EntityData.PLAYER_GARBAGE_BAG);
            NAPI.Player.StopPlayerAnimation(player);
            NAPI.Entity.DeleteEntity(garbageBag);

            // Get the remaining checkpoints
            int route = NAPI.Data.GetEntityData(driver, EntityData.PLAYER_JOB_ROUTE);
            int checkPoint = NAPI.Data.GetEntityData(driver, EntityData.PLAYER_JOB_CHECKPOINT) + 1;
            int totalCheckPoints = Constants.GARBAGE_LIST.Where(x => x.route == route).Count();

            // Get the current checkpoint
            Checkpoint garbageCheckpoint = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COLSHAPE);

            if (checkPoint < totalCheckPoints)
            {
                Vector3 currentGarbagePosition = GetGarbageCheckPointPosition(route, checkPoint);
                Vector3 nextGarbagePosition = GetGarbageCheckPointPosition(route, checkPoint + 1);

                // Show the next checkpoint
                NAPI.Entity.SetEntityPosition(garbageCheckpoint, currentGarbagePosition);
                NAPI.Checkpoint.SetCheckpointDirection(garbageCheckpoint, nextGarbagePosition);
                NAPI.Data.SetEntityData(driver, EntityData.PLAYER_JOB_CHECKPOINT, checkPoint);
                NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_CHECKPOINT, checkPoint);
                NAPI.ClientEvent.TriggerClientEvent(driver, "showGarbageCheckPoint", currentGarbagePosition);
                NAPI.ClientEvent.TriggerClientEvent(player, "showGarbageCheckPoint", currentGarbagePosition);

                // Add the garbage bag
                garbageBag = NAPI.Object.CreateObject(628215202, currentGarbagePosition, new Vector3(0.0f, 0.0f, 0.0f));
                NAPI.Data.SetEntityData(player, EntityData.PLAYER_GARBAGE_BAG, garbageBag);
            }
            else
            {
                Vector3 garbagePosition = new Vector3(-339.0206f, -1560.117f, 25.23038f);
                NAPI.Entity.SetEntityModel(garbageCheckpoint, 4);
                NAPI.Entity.SetEntityPosition(garbageCheckpoint, garbagePosition);
                NAPI.Chat.SendChatMessageToPlayer(driver, Constants.COLOR_INFO + Messages.INF_ROUTE_FINISHED);
                NAPI.ClientEvent.TriggerClientEvent(driver, "showGarbageCheckPoint", garbagePosition);
                NAPI.ClientEvent.TriggerClientEvent(player, "deleteGarbageCheckPoint");
            }

            if (garbageTimerList.TryGetValue(player.Value, out Timer garbageTimer) == true)
            {
                garbageTimer.Dispose();
                garbageTimerList.Remove(player.Value);
            }
            
            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_GARBAGE_COLLECTED);
        }

        private Vector3 GetGarbageCheckPointPosition(int route, int checkPoint)
        {
            Vector3 position = new Vector3();
            foreach (GarbageModel garbage in Constants.GARBAGE_LIST)
            {
                if (garbage.route == route && garbage.checkPoint == checkPoint)
                {
                    position = garbage.position;
                    break;
                }
            }
            return position;
        }

        private void FinishGarbageRoute(Client driver, bool canceled = false)
        {
            Vehicle vehicle = NAPI.Player.GetPlayerVehicle(driver);
            Client partner = NAPI.Data.GetEntityData(driver, EntityData.PLAYER_JOB_PARTNER);
            
            RespawnGarbageVehicle(vehicle);

            // Destroy the previous checkpoint
            Checkpoint garbageCheckpoint = NAPI.Data.GetEntityData(driver, EntityData.PLAYER_JOB_COLSHAPE);
            NAPI.ClientEvent.TriggerClientEvent(driver, "deleteGarbageCheckPoint");
            NAPI.Entity.DeleteEntity(garbageCheckpoint);

            // Entity data reset
            NAPI.Data.ResetEntityData(driver, EntityData.PLAYER_JOB_PARTNER);
            NAPI.Data.ResetEntityData(partner, EntityData.PLAYER_JOB_PARTNER);
            NAPI.Data.ResetEntityData(driver, EntityData.PLAYER_JOB_COLSHAPE);
            NAPI.Data.ResetEntityData(partner, EntityData.PLAYER_GARBAGE_BAG);
            NAPI.Data.ResetEntityData(driver, EntityData.PLAYER_JOB_ROUTE);
            NAPI.Data.ResetEntityData(partner, EntityData.PLAYER_JOB_ROUTE);
            NAPI.Data.ResetEntityData(driver, EntityData.PLAYER_JOB_CHECKPOINT);
            NAPI.Data.ResetEntityData(partner, EntityData.PLAYER_JOB_CHECKPOINT);
            NAPI.Data.ResetEntityData(driver, EntityData.PLAYER_JOB_VEHICLE);
            NAPI.Data.ResetEntityData(partner, EntityData.PLAYER_JOB_VEHICLE);
            NAPI.Data.ResetEntityData(partner, EntityData.PLAYER_ANIMATION);

            if (!canceled)
            {
                // Pay the earnings to both players
                int driverMoney = NAPI.Data.GetEntitySharedData(driver, EntityData.PLAYER_MONEY);
                int partnerMoney = NAPI.Data.GetEntitySharedData(partner, EntityData.PLAYER_MONEY);
                NAPI.Data.SetEntitySharedData(driver, EntityData.PLAYER_MONEY, driverMoney + Constants.MONEY_GARBAGE_ROUTE);
                NAPI.Data.SetEntitySharedData(partner, EntityData.PLAYER_MONEY, partnerMoney + Constants.MONEY_GARBAGE_ROUTE);

                // Send the message with the earnings
                String message = String.Format(Messages.INF_GARBAGE_EARNINGS, Constants.MONEY_GARBAGE_ROUTE);
                NAPI.Chat.SendChatMessageToPlayer(driver, Constants.COLOR_INFO + message);
                NAPI.Chat.SendChatMessageToPlayer(partner, Constants.COLOR_INFO + message);
            }

            // Remove players from the vehicle
            NAPI.Player.WarpPlayerOutOfVehicle(driver);
            NAPI.Player.WarpPlayerOutOfVehicle(partner);
        }

        [ServerEvent(Event.PlayerEnterVehicle)]
        public void OnPlayerEnterVehicle(Client player, Vehicle vehicle, sbyte seat)
        {
            if (NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_FACTION) == Constants.JOB_GARBAGE + Constants.MAX_FACTION_VEHICLES)
            {
                if (NAPI.Player.GetPlayerVehicleSeat(player) == (int)VehicleSeat.Driver)
                {
                    if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_ROUTE) == false && NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_VEHICLE) == false)
                    {
                        NAPI.Player.WarpPlayerOutOfVehicle(player);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_IN_ROUTE);
                    }
                    else if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_VEHICLE) && NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_VEHICLE) != vehicle)
                    {
                        NAPI.Player.WarpPlayerOutOfVehicle(player);
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_YOUR_JOB_VEHICLE);
                    }
                    else
                    {
                        if (garbageTimerList.TryGetValue(player.Value, out Timer garbageTimer) == true)
                        {
                            garbageTimer.Dispose();
                            garbageTimerList.Remove(player.Value);
                        }

                        // Check whether route starts or he's returning to the truck
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_VEHICLE) == false)
                        {
                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_PARTNER, player);
                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_VEHICLE, vehicle);
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_PLAYER_WAITING_PARTNER);
                        }
                        else
                        {
                            // We continue with the previous route
                            Client partner = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                            int garbageRoute = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_ROUTE);
                            int checkPoint = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_CHECKPOINT);
                            Vector3 garbagePosition = GetGarbageCheckPointPosition(garbageRoute, checkPoint);

                            NAPI.ClientEvent.TriggerClientEvent(player, "showGarbageCheckPoint", garbagePosition);
                            NAPI.ClientEvent.TriggerClientEvent(partner, "showGarbageCheckPoint", garbagePosition);
                        }
                    }
                }
                else
                {
                    foreach (Client driver in NAPI.Vehicle.GetVehicleOccupants(vehicle))
                    {
                        if (NAPI.Data.HasEntityData(driver, EntityData.PLAYER_JOB_PARTNER) && NAPI.Player.GetPlayerVehicleSeat(driver) == (int)VehicleSeat.Driver)
                        {
                            Client partner = NAPI.Data.GetEntityData(driver, EntityData.PLAYER_JOB_PARTNER);
                            if (partner == driver)
                            {
                                if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_ON_DUTY) == 1)
                                {
                                    // Link both players as partners
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_PARTNER, driver);
                                    NAPI.Data.SetEntityData(driver, EntityData.PLAYER_JOB_PARTNER, player);

                                    // Set the route to the passenger
                                    int garbageRoute = NAPI.Data.GetEntityData(driver, EntityData.PLAYER_JOB_ROUTE);
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_ROUTE, garbageRoute);
                                    NAPI.Data.SetEntityData(driver, EntityData.PLAYER_JOB_CHECKPOINT, 0);
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_CHECKPOINT, 0);

                                    // Create the first checkpoint
                                    Vector3 currentGarbagePosition = GetGarbageCheckPointPosition(garbageRoute, 0);
                                    Vector3 nextGarbagePosition = GetGarbageCheckPointPosition(garbageRoute, 1);
                                    Checkpoint garbageCheckpoint = NAPI.Checkpoint.CreateCheckpoint(0, currentGarbagePosition, nextGarbagePosition, 2.5f, new Color(198, 40, 40, 200));
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_COLSHAPE, garbageCheckpoint);

                                    // Add garbage bag
                                    GTANetworkAPI.Object trashBag = NAPI.Object.CreateObject(628215202, currentGarbagePosition, new Vector3(0.0f, 0.0f, 0.0f));
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_GARBAGE_BAG, trashBag);

                                    NAPI.ClientEvent.TriggerClientEvent(driver, "showGarbageCheckPoint", currentGarbagePosition);
                                    NAPI.ClientEvent.TriggerClientEvent(player, "showGarbageCheckPoint", currentGarbagePosition);
                                }
                                else
                                {
                                    NAPI.Player.WarpPlayerOutOfVehicle(player);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ON_DUTY);
                                }
                            }
                            return;
                        }
                    }

                    // There's no player driving, kick the passenger
                    NAPI.Player.WarpPlayerOutOfVehicle(player);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_WAIT_GARBAGE_DRIVER);
                }
            }
        }

        [ServerEvent(Event.PlayerExitVehicle)]
        public void OnPlayerExitVehicle(Client player, Vehicle vehicle)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_VEHICLE) && NAPI.Data.GetEntityData(vehicle, EntityData.VEHICLE_FACTION) == Constants.JOB_GARBAGE + Constants.MAX_FACTION_VEHICLES)
            {
                if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_VEHICLE) == vehicle && NAPI.Player.GetPlayerVehicleSeat(player) == (int)VehicleSeat.Driver)
                {
                    Client target = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                    String warn = String.Format(Messages.INF_JOB_VEHICLE_LEFT, 45);
                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + warn);
                    NAPI.ClientEvent.TriggerClientEvent(player, "deleteGarbageCheckPoint");
                    NAPI.ClientEvent.TriggerClientEvent(target, "deleteGarbageCheckPoint");

                    // Create the timer for driver to get into the vehicle
                    Timer garbageTimer = new Timer(OnGarbageTimer, player, 45000, Timeout.Infinite);
                    garbageTimerList.Add(player.Value, garbageTimer);
                }
            }
        }

        [ServerEvent(Event.PlayerEnterCheckpoint)]
        public void OnPlayerEnterCheckpoint(Checkpoint checkpoint, Client player)
        {
            if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_COLSHAPE) && NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB) == Constants.JOB_GARBAGE)
            {
                // Get garbage checkpoint
                Checkpoint garbageCheckpoint = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COLSHAPE);

                if (NAPI.Player.GetPlayerVehicleSeat(player) == (int)VehicleSeat.Driver && garbageCheckpoint == checkpoint)
                {
                    NetHandle vehicle = NAPI.Player.GetPlayerVehicle(player);
                    if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_VEHICLE) == vehicle)
                    {
                        // Finish the route
                        FinishGarbageRoute(player);
                    }
                    else
                    {
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_IN_VEHICLE_JOB);
                    }
                }
            }
        }

        [Command(Messages.COM_GARBAGE, Messages.GEN_GARBAGE_JOB_COMMAND)]
        public void GarbageCommand(Client player, String action)
        {
            if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB) != Constants.JOB_GARBAGE)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_GARBAGE);
            }
            else if (NAPI.Data.GetEntityData(player, EntityData.PLAYER_ON_DUTY) == 0)
            {
                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_PLAYER_NOT_ON_DUTY);
            }
            else
            {
                switch (action.ToLower())
                {
                    case Messages.ARG_ROUTE:
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_ROUTE) == true)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Messages.ERR_ALREADY_IN_ROUTE);
                        }
                        else
                        {
                            Random random = new Random();
                            int garbageRoute = random.Next(Constants.MAX_GARBAGE_ROUTES);
                            NAPI.Data.SetEntityData(player, EntityData.PLAYER_JOB_ROUTE, garbageRoute);
                            switch (garbageRoute)
                            {
                                case 0:
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.GEN_ROUTE_NORTH);
                                    break;
                                case 1:
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.GEN_ROUTE_SOUTH);
                                    break;
                                case 2:
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.GEN_ROUTE_EAST);
                                    break;
                                case 3:
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.GEN_ROUTE_WEST);
                                    break;
                            }
                        }
                        break;
                    case Messages.ARG_PICKUP:
                        if (NAPI.Player.IsPlayerInAnyVehicle(player) == true)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_GARBAGE_IN_VEHICLE);
                        }
                        else if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_COLSHAPE) == false)
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_GARBAGE_NEAR);
                        }
                        else
                        {
                            Checkpoint garbageCheckpoint = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COLSHAPE);
                            if (player.Position.DistanceTo(garbageCheckpoint.Position) < 3.5f)
                            {
                                if (garbageTimerList.TryGetValue(player.Value, out Timer garbageTimer) == false)
                                {
                                    NAPI.Player.PlayPlayerAnimation(player, (int)(Constants.AnimationFlags.Loop | Constants.AnimationFlags.AllowPlayerControl), "anim@move_m@trash", "pickup");
                                    NAPI.Data.SetEntityData(player, EntityData.PLAYER_ANIMATION, true);

                                    // Make the timer for garbage collection
                                    garbageTimer = new Timer(OnGarbageCollectedTimer, player, 15000, Timeout.Infinite);
                                    garbageTimerList.Add(player.Value, garbageTimer);
                                }
                                else
                                {
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_ALREADY_GARBAGE);
                                }
                            }
                            else
                            {
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_GARBAGE_NEAR);
                            }
                        }
                        break;
                    case Messages.ARG_CANCEL:
                        if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_PARTNER) == true)
                        {
                            Client partner = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                            if (partner != player)
                            {
                                GTANetworkAPI.Object trashBag = null;
                                Checkpoint garbageCheckpoint = null;

                                if (NAPI.Player.GetPlayerVehicleSeat(player) == (int)VehicleSeat.Driver)
                                {
                                    // Driver canceled
                                    trashBag = NAPI.Data.GetEntityData(player, EntityData.PLAYER_GARBAGE_BAG);
                                    garbageCheckpoint = NAPI.Data.GetEntityData(player, EntityData.PLAYER_JOB_COLSHAPE);
                                    NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_ROUTE_FINISHED);
                                    NAPI.ClientEvent.TriggerClientEvent(partner, "deleteGarbageCheckPoint");
                                }
                                else
                                {
                                    // Passenger canceled
                                    trashBag = NAPI.Data.GetEntityData(partner, EntityData.PLAYER_GARBAGE_BAG);
                                    garbageCheckpoint = NAPI.Data.GetEntityData(partner, EntityData.PLAYER_JOB_COLSHAPE);
                                    trashBag = NAPI.Data.GetEntityData(partner, EntityData.PLAYER_GARBAGE_BAG);
                                    NAPI.ClientEvent.TriggerClientEvent(player, "deleteGarbageCheckPoint");
                                }
                                
                                NAPI.Entity.DeleteEntity(trashBag);

                                // Create finish checkpoint
                                NAPI.Entity.SetEntityModel(garbageCheckpoint, 4);
                                NAPI.Entity.SetEntityPosition(garbageCheckpoint, new Vector3(-339.0206f, -1560.117f, 25.23038f));
                            }
                            else
                            {
                                // Player doesn't have any partner
                                NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_ROUTE_CANCELED);
                            }

                            // Remove player from partner search
                            NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                        }
                        else if (NAPI.Data.HasEntityData(player, EntityData.PLAYER_JOB_ROUTE) == true)
                        {
                            // Cancel the route
                            NAPI.Data.ResetEntityData(player, EntityData.PLAYER_JOB_PARTNER);
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_INFO + Messages.INF_GARBAGE_ROUTE_CANCELED);
                        }
                        else
                        {
                            NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_ERROR + Messages.ERR_NOT_IN_ROUTE);
                        }
                        break;
                    default:
                        NAPI.Chat.SendChatMessageToPlayer(player, Constants.COLOR_HELP + Messages.GEN_GARBAGE_JOB_COMMAND);
                        break;
                }
            }
        }
    }
}