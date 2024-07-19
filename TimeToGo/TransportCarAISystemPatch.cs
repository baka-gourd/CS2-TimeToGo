using System;
using Game.Simulation;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using Colossal.Logging.Utils;
using Game;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using System.Runtime.CompilerServices;
using Game.Vehicles;
using Game.Common;
using Game.Creatures;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Pathfind;
using Game.Routes;
using Game.Tools;
using CargoTransport = Game.Vehicles.CargoTransport;
using Color = Game.Routes.Color;
using PublicTransport = Game.Vehicles.PublicTransport;
using StorageCompany = Game.Companies.StorageCompany;
using SubLane = Game.Net.SubLane;
using TransportStation = Game.Buildings.TransportStation;

namespace TimeToGo
{
    [HarmonyPatch(typeof(TransportCarAISystem))]
    [HarmonyPatch("OnUpdate")]
    public class TransportCarAISystemPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            var codes = new List<CodeInstruction>(instructions);

            var index = codes.FindIndex(c =>
                c.opcode == OpCodes.Initobj && c.operand is Type operandType && operandType.GetFriendlyName() ==
                "Game.Simulation.TransportCarAISystem/TransportCarTickJob") - 1;

            if (index >= 0)
            {
                var newCode = codes.Take(index)
                    .AddItem(new CodeInstruction(OpCodes.Ldarg_0))
                    .AddItem(new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(TransportCarAISystemPatch), nameof(ScheduleNewJob))))
                    .AddItem(new CodeInstruction(OpCodes.Ret))
                    .ToArray();
                return newCode;
            }

            return codes;
        }

        public static void ScheduleNewJob(TransportCarAISystem system)
        {
            if (!TransportCarAISystemPatchHelper.Init)
            {
                TransportCarAISystemPatchHelper.InitHandle(system);
                TransportCarAISystemPatchHelper.Init = true;
            }

            var entityManager = (EntityManager)typeof(SystemBase).GetProperty("EntityManager",
                BindingFlags.Public | BindingFlags.Instance)!.GetValue(system);
            foreach (var entity in TransportCarAISystemPatchHelper.Query.ToEntityArray(Allocator.Temp))
            {
                if (entityManager.HasComponent<TransportVehicleStopTimer>(entity))
                {
                    continue;
                }

                entityManager.AddComponent<TransportVehicleStopTimer>(entity);
            }

            var query = typeof(TransportCarAISystem).GetField("m_VehicleQuery",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var cityStatisticsSystem = typeof(TransportCarAISystem).GetField("m_CityStatisticsSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var boardingLookupData = typeof(TransportCarAISystem).GetField("m_BoardingLookupData",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var simulationSystem = typeof(TransportCarAISystem).GetField("m_SimulationSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var pathFindSetupSystem = typeof(TransportCarAISystem).GetField("m_PathfindSetupSystem",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var transportVehicleRequestArchetype = typeof(TransportCarAISystem).GetField(
                "m_TransportVehicleRequestArchetype",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var evacuationRequestArchetype = typeof(TransportCarAISystem).GetField("m_EvacuationRequestArchetype",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var prisonerTransportRequestArchetype = typeof(TransportCarAISystem).GetField(
                "m_PrisonerTransportRequestArchetype",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var handleRequestArchetype = typeof(TransportCarAISystem).GetField("m_HandleRequestArchetype",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var endFrameBarrier = typeof(TransportCarAISystem).GetField("m_EndFrameBarrier",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);


            var typeHandle = typeof(TransportCarAISystem).GetField("__TypeHandle",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(system);
            var dependency = typeof(SystemBase).GetProperty("Dependency",
                BindingFlags.NonPublic | BindingFlags.Instance);

            #region TYPEHANDLE_REFLECTION

            var m_EntityType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Unity_Entities_Entity_TypeHandle", BindingFlags.Public | BindingFlags.Instance);
            var m_OwnerType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Common_Owner_RO_ComponentTypeHandle", BindingFlags.Public | BindingFlags.Instance);
            var m_UnspawnedType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Objects_Unspawned_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PathInformationType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathInformation_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PrefabRefType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CurrentRouteType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_CurrentRoute_RO_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PassengerType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_Passenger_RO_BufferTypeHandle", BindingFlags.Public | BindingFlags.Instance);
            var m_CargoTransportType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_CargoTransport_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PublicTransportType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_PublicTransport_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CarType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_Car_RW_ComponentTypeHandle", BindingFlags.Public | BindingFlags.Instance);
            var m_CurrentLaneType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_CarCurrentLane_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TargetType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Common_Target_RW_ComponentTypeHandle", BindingFlags.Public | BindingFlags.Instance);
            var m_PathOwnerType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathOwner_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_OdometerType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_Odometer_RW_ComponentTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CarNavigationLaneType = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_CarNavigationLane_RW_BufferTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_ServiceDispatchType = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_ServiceDispatch_RW_BufferTypeHandle",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TransformData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Objects_Transform_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_OwnerData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Common_Owner_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_PathInformationData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathInformation_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PrefabCarData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_CarData_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_PrefabRefData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_PrefabRef_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_PublicTransportVehicleData = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_PublicTransportVehicleData_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_CargoTransportVehicleData = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Prefabs_CargoTransportVehicleData_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_ServiceRequestData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_ServiceRequest_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TransportVehicleRequestData = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_TransportVehicleRequest_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_EvacuationRequestData = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_EvacuationRequest_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_PrisonerTransportRequestData = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Simulation_PrisonerTransportRequest_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_WaypointData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_Waypoint_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_ConnectedData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_Connected_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_BoardingVehicleData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_BoardingVehicle_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_RouteLaneData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_RouteLane_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_RouteColorData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_Color_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_StorageCompanyData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Companies_StorageCompany_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_TransportStationData = typeof(TransportCarAISystem)
                .GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Buildings_TransportStation_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_LaneData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_Lane_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_SlaveLaneData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_SlaveLane_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_CurveData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_Curve_RO_ComponentLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_CurrentVehicleData = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Creatures_CurrentVehicle_RO_ComponentLookup",
                    BindingFlags.Public | BindingFlags.Instance);
            var m_RouteWaypoints = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Routes_RouteWaypoint_RO_BufferLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_SubLanes = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Net_SubLane_RO_BufferLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_PathElements = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Pathfind_PathElement_RW_BufferLookup", BindingFlags.Public | BindingFlags.Instance);
            var m_LoadingResources = typeof(TransportCarAISystem).GetNestedType("TypeHandle", BindingFlags.NonPublic)
                .GetField("__Game_Vehicles_LoadingResources_RW_BufferLookup",
                    BindingFlags.Public | BindingFlags.Instance);

            #endregion

            var dep = dependency.GetValue(system);
            var boardingData = new TransportBoardingHelpers.BoardingData(Allocator.TempJob);
            var job = new TransportCarTickJobWithTimer()
            {
                m_EntityType = (EntityTypeHandle)m_EntityType.GetValue(typeHandle),
                m_OwnerType = (ComponentTypeHandle<Owner>)m_OwnerType.GetValue(typeHandle),
                m_UnspawnedType = (ComponentTypeHandle<Unspawned>)m_UnspawnedType.GetValue(typeHandle),
                m_PathInformationType =
                    (ComponentTypeHandle<PathInformation>)m_PathInformationType.GetValue(typeHandle),
                m_PrefabRefType = (ComponentTypeHandle<PrefabRef>)m_PrefabRefType.GetValue(typeHandle),
                m_CurrentRouteType = (ComponentTypeHandle<CurrentRoute>)m_CurrentRouteType.GetValue(typeHandle),
                m_PassengerType = (BufferTypeHandle<Passenger>)m_PassengerType.GetValue(typeHandle),
                m_CargoTransportType = (ComponentTypeHandle<CargoTransport>)m_CargoTransportType.GetValue(typeHandle),
                m_PublicTransportType =
                    (ComponentTypeHandle<PublicTransport>)m_PublicTransportType.GetValue(typeHandle),
                m_CarType = (ComponentTypeHandle<Car>)m_CarType.GetValue(typeHandle),
                m_CurrentLaneType = (ComponentTypeHandle<CarCurrentLane>)m_CurrentLaneType.GetValue(typeHandle),
                m_TargetType = (ComponentTypeHandle<Target>)m_TargetType.GetValue(typeHandle),
                m_PathOwnerType = (ComponentTypeHandle<PathOwner>)m_PathOwnerType.GetValue(typeHandle),
                m_OdometerType = (ComponentTypeHandle<Odometer>)m_OdometerType.GetValue(typeHandle),
                m_CarNavigationLaneType =
                    (BufferTypeHandle<CarNavigationLane>)m_CarNavigationLaneType.GetValue(typeHandle),
                m_ServiceDispatchType = (BufferTypeHandle<ServiceDispatch>)m_ServiceDispatchType.GetValue(typeHandle),
                m_TransformData = (ComponentLookup<Transform>)m_TransformData.GetValue(typeHandle),
                m_OwnerData = (ComponentLookup<Owner>)m_OwnerData.GetValue(typeHandle),
                m_PathInformationData = (ComponentLookup<PathInformation>)m_PathInformationData.GetValue(typeHandle),
                m_PrefabCarData = (ComponentLookup<CarData>)m_PrefabCarData.GetValue(typeHandle),
                m_PrefabRefData = (ComponentLookup<PrefabRef>)m_PrefabRefData.GetValue(typeHandle),
                m_PublicTransportVehicleData =
                    (ComponentLookup<PublicTransportVehicleData>)m_PublicTransportVehicleData.GetValue(typeHandle),
                m_CargoTransportVehicleData =
                    (ComponentLookup<CargoTransportVehicleData>)m_CargoTransportVehicleData.GetValue(typeHandle),
                m_ServiceRequestData = (ComponentLookup<ServiceRequest>)m_ServiceRequestData.GetValue(typeHandle),
                m_TransportVehicleRequestData =
                    (ComponentLookup<TransportVehicleRequest>)m_TransportVehicleRequestData.GetValue(typeHandle),
                m_EvacuationRequestData =
                    (ComponentLookup<EvacuationRequest>)m_EvacuationRequestData.GetValue(typeHandle),
                m_PrisonerTransportRequestData =
                    (ComponentLookup<PrisonerTransportRequest>)m_PrisonerTransportRequestData.GetValue(typeHandle),
                m_WaypointData = (ComponentLookup<Waypoint>)m_WaypointData.GetValue(typeHandle),
                m_ConnectedData = (ComponentLookup<Connected>)m_ConnectedData.GetValue(typeHandle),
                m_BoardingVehicleData = (ComponentLookup<BoardingVehicle>)m_BoardingVehicleData.GetValue(typeHandle),
                m_RouteLaneData = (ComponentLookup<RouteLane>)m_RouteLaneData.GetValue(typeHandle),
                m_RouteColorData = (ComponentLookup<Color>)m_RouteColorData.GetValue(typeHandle),
                m_StorageCompanyData = (ComponentLookup<StorageCompany>)m_StorageCompanyData.GetValue(typeHandle),
                m_TransportStationData =
                    (ComponentLookup<TransportStation>)m_TransportStationData.GetValue(typeHandle),
                m_LaneData = (ComponentLookup<Lane>)m_LaneData.GetValue(typeHandle),
                m_SlaveLaneData = (ComponentLookup<SlaveLane>)m_SlaveLaneData.GetValue(typeHandle),
                m_CurveData = (ComponentLookup<Curve>)m_CurveData.GetValue(typeHandle),
                m_CurrentVehicleData = (ComponentLookup<CurrentVehicle>)m_CurrentVehicleData.GetValue(typeHandle),
                m_RouteWaypoints = (BufferLookup<RouteWaypoint>)m_RouteWaypoints.GetValue(typeHandle),
                m_SubLanes = (BufferLookup<SubLane>)m_SubLanes.GetValue(typeHandle),
                m_PathElements = (BufferLookup<PathElement>)m_PathElements.GetValue(typeHandle),
                m_LoadingResources = (BufferLookup<LoadingResources>)m_LoadingResources.GetValue(typeHandle),
                m_SimulationFrameIndex = ((SimulationSystem)simulationSystem).frameIndex,
                m_TransportVehicleRequestArchetype = (EntityArchetype)transportVehicleRequestArchetype,
                m_EvacuationRequestArchetype = (EntityArchetype)evacuationRequestArchetype,
                m_PrisonerTransportRequestArchetype = (EntityArchetype)prisonerTransportRequestArchetype,
                m_HandleRequestArchetype = (EntityArchetype)handleRequestArchetype,
                m_CommandBuffer = ((EndFrameBarrier)endFrameBarrier).CreateCommandBuffer().AsParallelWriter(),
                m_PathfindQueue = ((PathfindSetupSystem)pathFindSetupSystem).GetQueue(system, 64).AsParallelWriter(),
                m_BoardingData = boardingData.ToConcurrent(),
                m_Timer = TransportCarAISystemPatchHelper.ExternalInjectTypeHandle.m_Timer,
                m_TimerData = TransportCarAISystemPatchHelper.ExternalInjectTypeHandle.m_TimerLookup
            };
            var handle = job.ScheduleParallel((EntityQuery)query, (JobHandle)dep);
            var inputDeps = boardingData.ScheduleBoarding(system,
                (CityStatisticsSystem)cityStatisticsSystem,
                (TransportBoardingHelpers.BoardingLookupData)boardingLookupData,
                ((SimulationSystem)simulationSystem).frameIndex,
                handle);
            boardingData.Dispose(inputDeps);
            ((PathfindSetupSystem)pathFindSetupSystem).AddQueueWriter(handle);
            ((EndFrameBarrier)endFrameBarrier).AddJobHandleForProducer(handle);
            dependency.SetValue(system, handle);
        }
    }

    public class TransportCarAISystemPatchHelper
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

        public static void InitHandle(TransportCarAISystem system)
        {
            Query = system.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<PublicTransport, CarCurrentLane>()
                .WithAbsent<TransportVehicleStopTimer>()
                .WithNone<Deleted, Temp, TripSource, OutOfControl>());
            ExternalInjectTypeHandle.__AssignHandles(ref system.CheckedStateRef);
        }
    }
}