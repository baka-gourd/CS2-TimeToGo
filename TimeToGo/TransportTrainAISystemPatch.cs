using System;
using Game.Common;
using Game.Objects;
using Game.Simulation;
using Game.Tools;
using Game.Vehicles;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Colossal.Logging.Utils;
using Unity.Collections;
using Unity.Entities;
using System.Reflection;
using Game.City;
using Game.Prefabs;
using Unity.Jobs;
using Game;
using Game.Creatures;
using Game.Economy;
using Game.Net;
using Game.Pathfind;
using Game.Routes;
using CargoTransport = Game.Vehicles.CargoTransport;
using Color = Game.Routes.Color;
using Edge = Game.Net.Edge;
using PublicTransport = Game.Vehicles.PublicTransport;
using StorageCompany = Game.Companies.StorageCompany;
using SubLane = Game.Net.SubLane;
using TransportStation = Game.Buildings.TransportStation;

namespace TimeToGo
{
    [HarmonyPatch(typeof(TransportTrainAISystem))]
    [HarmonyPatch("OnUpdate")]
    public class TransportTrainAISystemPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            var index = codes.FindIndex(c =>
                c.opcode == OpCodes.Initobj && c.operand is Type operandType && operandType.GetFriendlyName() ==
                "Game.Simulation.TransportTrainAISystem/TransportTrainTickJob") - 1;

            if (index >= 0)
            {
                var newCode = codes.Take(index)
                    .AddItem(new CodeInstruction(OpCodes.Ldarg_0))
                    .AddItem(new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(TransportTrainAISystemPatch), nameof(ScheduleNewJob))))
                    .AddItem(new CodeInstruction(OpCodes.Ret))
                    .ToArray();
                return newCode;
            }

            return codes;
        }

        public static void ScheduleNewJob(TransportTrainAISystem system)
        {
            if (!TransportTrainAISystemPatchHelper.Init)
            {
                TransportTrainAISystemPatchHelper.InitHandle(system);
                TransportTrainAISystemPatchHelper.Init = true;
            }

            var entityManager = (EntityManager) typeof(SystemBase).GetProperty("EntityManager",
                BindingFlags.Public | BindingFlags.Instance)!.GetValue(system);
            foreach (var entity in TransportTrainAISystemPatchHelper.Query.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.HasComponent<TransportVehicleStopTimer>(entity))
                {
                    continue;
                }

                entityManager.AddComponent<TransportVehicleStopTimer>(entity);
            }

            var transportTrainCarriageSelectData = typeof(TransportTrainAISystem).GetField(
                "m_TransportTrainCarriageSelectData",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var cityConfigurationSystem = typeof(TransportTrainAISystem).GetField("m_CityConfigurationSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var query = typeof(TransportTrainAISystem).GetField("m_CarriagePrefabQuery",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var cityStatisticsSystem = typeof(TransportTrainAISystem).GetField("m_CityStatisticsSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var boardingLookupData = typeof(TransportTrainAISystem).GetField("m_BoardingLookupData",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var simulationSystem = typeof(TransportTrainAISystem).GetField("m_SimulationSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var pathFindSetupSystem = typeof(TransportTrainAISystem).GetField("m_PathfindSetupSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var endFrameBarrier = typeof(TransportTrainAISystem).GetField("m_EndFrameBarrier",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var handleRequestArchetype = typeof(TransportTrainAISystem).GetField("m_HandleRequestArchetype",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var transportVehicleRequestArchetype = typeof(TransportTrainAISystem).GetField(
                "m_TransportVehicleRequestArchetype",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);

            var typeHandle = typeof(TransportTrainAISystem).GetField("__TypeHandle",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var dependency = typeof(SystemBase).GetProperty("Dependency",
                BindingFlags.NonPublic | BindingFlags.Instance);

            #region TYPEHANDLE_REFLECTION

            var m_EntityType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Unity_Entities_Entity_TypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_OwnerType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Common_Owner_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_UnspawnedType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Objects_Unspawned_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PrefabRefType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CurrentRouteType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_CurrentRoute_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CargoTransportType = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_CargoTransport_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PublicTransportType = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_PublicTransport_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TargetType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Common_Target_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PathOwnerType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathOwner_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_OdometerType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_Odometer_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_LayoutElementType = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_LayoutElement_RW_BufferTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_NavigationLaneType = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_TrainNavigationLane_RW_BufferTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_ServiceDispatchType = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_ServiceDispatch_RW_BufferTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TransformData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Objects_Transform_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_OwnerData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Common_Owner_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PathInformationData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathInformation_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TransportVehicleRequestData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_TransportVehicleRequest_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CurveData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_Curve_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_LaneData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_Lane_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_EdgeLaneData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_EdgeLane_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_EdgeData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_Edge_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PrefabTrainData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_TrainData_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PrefabRefData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_PrefabRef_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PublicTransportVehicleData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_PublicTransportVehicleData_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CargoTransportVehicleData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_CargoTransportVehicleData_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_WaypointData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_Waypoint_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_ConnectedData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_Connected_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_BoardingVehicleData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_BoardingVehicle_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_RouteColorData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_Color_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_StorageCompanyData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Companies_StorageCompany_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TransportStationData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Buildings_TransportStation_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CurrentVehicleData = typeof(TransportTrainAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Creatures_CurrentVehicle_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_Passengers = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_Passenger_RO_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_EconomyResources = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Economy_Resources_RO_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_RouteWaypoints = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_RouteWaypoint_RO_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_ConnectedEdges = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_ConnectedEdge_RO_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_SubLanes = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_SubLane_RO_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TrainData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_Train_RW_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CurrentLaneData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_TrainCurrentLane_RW_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_NavigationData = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_TrainNavigation_RW_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PathElements = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathElement_RW_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_LoadingResources = typeof(TransportTrainAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_LoadingResources_RW_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);

            #endregion

            var dep = dependency.GetValue(system);
            var boardingData = new TransportBoardingHelpers.BoardingData(Allocator.TempJob);

            ((TransportTrainCarriageSelectData) transportTrainCarriageSelectData).PreUpdate(system,
                (CityConfigurationSystem) cityConfigurationSystem, (EntityQuery) query, Allocator.TempJob,
                out var currentJob);

            var job = new TransportTrainTickJobWithTimer()
            {
                m_EntityType = (EntityTypeHandle) m_EntityType.GetValue(typeHandle),
                m_OwnerType = (ComponentTypeHandle<Owner>) m_OwnerType.GetValue(typeHandle),
                m_UnspawnedType = (ComponentTypeHandle<Unspawned>) m_UnspawnedType.GetValue(typeHandle),
                m_PrefabRefType = (ComponentTypeHandle<PrefabRef>) m_PrefabRefType.GetValue(typeHandle),
                m_CurrentRouteType = (ComponentTypeHandle<CurrentRoute>) m_CurrentRouteType.GetValue(typeHandle),
                m_CargoTransportType = (ComponentTypeHandle<CargoTransport>) m_CargoTransportType.GetValue(typeHandle),
                m_PublicTransportType =
                    (ComponentTypeHandle<PublicTransport>) m_PublicTransportType.GetValue(typeHandle),
                m_TargetType = (ComponentTypeHandle<Target>) m_TargetType.GetValue(typeHandle),
                m_PathOwnerType = (ComponentTypeHandle<PathOwner>) m_PathOwnerType.GetValue(typeHandle),
                m_OdometerType = (ComponentTypeHandle<Odometer>) m_OdometerType.GetValue(typeHandle),
                m_LayoutElementType = (BufferTypeHandle<LayoutElement>) m_LayoutElementType.GetValue(typeHandle),
                m_NavigationLaneType =
                    (BufferTypeHandle<TrainNavigationLane>) m_NavigationLaneType.GetValue(typeHandle),
                m_ServiceDispatchType = (BufferTypeHandle<ServiceDispatch>) m_ServiceDispatchType.GetValue(typeHandle),
                m_TransformData = (ComponentLookup<Transform>) m_TransformData.GetValue(typeHandle),
                m_OwnerData = (ComponentLookup<Owner>) m_OwnerData.GetValue(typeHandle),
                m_PathInformationData = (ComponentLookup<PathInformation>) m_PathInformationData.GetValue(typeHandle),
                m_TransportVehicleRequestData =
                    (ComponentLookup<TransportVehicleRequest>) m_TransportVehicleRequestData.GetValue(typeHandle),
                m_CurveData = (ComponentLookup<Curve>) m_CurveData.GetValue(typeHandle),
                m_LaneData = (ComponentLookup<Lane>) m_LaneData.GetValue(typeHandle),
                m_EdgeLaneData = (ComponentLookup<EdgeLane>) m_EdgeLaneData.GetValue(typeHandle),
                m_EdgeData = (ComponentLookup<Edge>) m_EdgeData.GetValue(typeHandle),
                m_PrefabTrainData = (ComponentLookup<TrainData>) m_PrefabTrainData.GetValue(typeHandle),
                m_PrefabRefData = (ComponentLookup<PrefabRef>) m_PrefabRefData.GetValue(typeHandle),
                m_PublicTransportVehicleData =
                    (ComponentLookup<PublicTransportVehicleData>) m_PublicTransportVehicleData.GetValue(typeHandle),
                m_CargoTransportVehicleData =
                    (ComponentLookup<CargoTransportVehicleData>) m_CargoTransportVehicleData.GetValue(typeHandle),
                m_WaypointData = (ComponentLookup<Waypoint>) m_WaypointData.GetValue(typeHandle),
                m_ConnectedData = (ComponentLookup<Connected>) m_ConnectedData.GetValue(typeHandle),
                m_BoardingVehicleData = (ComponentLookup<BoardingVehicle>) m_BoardingVehicleData.GetValue(typeHandle),
                m_RouteColorData = (ComponentLookup<Color>) m_RouteColorData.GetValue(typeHandle),
                m_StorageCompanyData = (ComponentLookup<StorageCompany>) m_StorageCompanyData.GetValue(typeHandle),
                m_TransportStationData =
                    (ComponentLookup<TransportStation>) m_TransportStationData.GetValue(typeHandle),
                m_CurrentVehicleData = (ComponentLookup<CurrentVehicle>) m_CurrentVehicleData.GetValue(typeHandle),
                m_Passengers = (BufferLookup<Passenger>) m_Passengers.GetValue(typeHandle),
                m_EconomyResources = (BufferLookup<Resources>) m_EconomyResources.GetValue(typeHandle),
                m_RouteWaypoints = (BufferLookup<RouteWaypoint>) m_RouteWaypoints.GetValue(typeHandle),
                m_ConnectedEdges = (BufferLookup<ConnectedEdge>) m_ConnectedEdges.GetValue(typeHandle),
                m_SubLanes = (BufferLookup<SubLane>) m_SubLanes.GetValue(typeHandle),
                m_TrainData = (ComponentLookup<Train>) m_TrainData.GetValue(typeHandle),
                m_CurrentLaneData = (ComponentLookup<TrainCurrentLane>) m_CurrentLaneData.GetValue(typeHandle),
                m_NavigationData = (ComponentLookup<TrainNavigation>) m_NavigationData.GetValue(typeHandle),
                m_PathElements = (BufferLookup<PathElement>) m_PathElements.GetValue(typeHandle),
                m_LoadingResources = (BufferLookup<LoadingResources>) m_LoadingResources.GetValue(typeHandle),
                m_SimulationFrameIndex = ((SimulationSystem) simulationSystem).frameIndex,
                m_RandomSeed = RandomSeed.Next(),
                m_TransportVehicleRequestArchetype = (EntityArchetype) transportVehicleRequestArchetype,
                m_HandleRequestArchetype = (EntityArchetype) handleRequestArchetype,
                m_TransportTrainCarriageSelectData =
                    (TransportTrainCarriageSelectData) transportTrainCarriageSelectData,
                m_CommandBuffer = ((EndFrameBarrier) endFrameBarrier).CreateCommandBuffer().AsParallelWriter(),
                m_PathfindQueue = ((PathfindSetupSystem) pathFindSetupSystem).GetQueue(system, 64).AsParallelWriter(),
                m_BoardingData = boardingData.ToConcurrent(),
                m_Timer = TransportTrainAISystemPatchHelper.ExternalInjectTypeHandle.m_Timer,
                m_TimerData = TransportTrainAISystemPatchHelper.ExternalInjectTypeHandle.m_TimerLookup
            };
            var handle = job.ScheduleParallel((EntityQuery) query,
                JobHandle.CombineDependencies((JobHandle) dep, currentJob));

            var inputDeps = boardingData.ScheduleBoarding(system,
                (CityStatisticsSystem) cityStatisticsSystem,
                (TransportBoardingHelpers.BoardingLookupData) boardingLookupData,
                ((SimulationSystem) simulationSystem).frameIndex,
                handle);

            ((TransportTrainCarriageSelectData) transportTrainCarriageSelectData).PostUpdate(handle);
            boardingData.Dispose(inputDeps);
            ((PathfindSetupSystem) pathFindSetupSystem).AddQueueWriter(handle);
            ((EndFrameBarrier) endFrameBarrier).AddJobHandleForProducer(handle);
            dependency.SetValue(system, handle);
        }
    }

    public class TransportTrainAISystemPatchHelper
    {
        public static bool Init { get; set; }
        public static InjectTypeHandle ExternalInjectTypeHandle;
        public static EntityQuery Query;

        public struct InjectTypeHandle
        {
            public ComponentTypeHandle<TransportVehicleStopTimer> m_Timer;
            public ComponentLookup<TransportVehicleStopTimer> m_TimerLookup;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                m_Timer = state.GetComponentTypeHandle<TransportVehicleStopTimer>();
                m_TimerLookup = state.GetComponentLookup<TransportVehicleStopTimer>();
            }
        }

        public static void InitHandle(TransportTrainAISystem system)
        {
            Query = system.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PublicTransport, TrainCurrentLane>()
                .WithAbsent<TransportVehicleStopTimer>()
                .WithNone<Deleted, Temp, TripSource, OutOfControl>());
            ExternalInjectTypeHandle.__AssignHandles(ref system.CheckedStateRef);
        }
    }
}