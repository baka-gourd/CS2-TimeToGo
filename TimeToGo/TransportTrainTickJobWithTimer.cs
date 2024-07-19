using Game.Common;
using Game.Creatures;
using Game.Economy;
using Game.Net;
using Game.Objects;
using Game.Pathfind;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Vehicles;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace TimeToGo
{
    [BurstCompile]
    public struct TransportTrainTickJobWithTimer : IJobChunk
    {
        public EntityTypeHandle m_EntityType;
        public ComponentTypeHandle<Owner> m_OwnerType;
        public ComponentTypeHandle<Unspawned> m_UnspawnedType;
        public ComponentTypeHandle<PrefabRef> m_PrefabRefType;
        public ComponentTypeHandle<CurrentRoute> m_CurrentRouteType;
        public ComponentTypeHandle<Game.Vehicles.CargoTransport> m_CargoTransportType;
        public ComponentTypeHandle<Game.Vehicles.PublicTransport> m_PublicTransportType;
        public ComponentTypeHandle<Target> m_TargetType;
        public ComponentTypeHandle<PathOwner> m_PathOwnerType;
        public ComponentTypeHandle<Odometer> m_OdometerType;
        public BufferTypeHandle<LayoutElement> m_LayoutElementType;
        public BufferTypeHandle<TrainNavigationLane> m_NavigationLaneType;
        public BufferTypeHandle<ServiceDispatch> m_ServiceDispatchType;
        public ComponentLookup<Transform> m_TransformData;
        public ComponentLookup<Owner> m_OwnerData;
        public ComponentLookup<PathInformation> m_PathInformationData;
        public ComponentLookup<TransportVehicleRequest> m_TransportVehicleRequestData;
        public ComponentLookup<Curve> m_CurveData;
        public ComponentLookup<Lane> m_LaneData;
        public ComponentLookup<EdgeLane> m_EdgeLaneData;
        public ComponentLookup<Game.Net.Edge> m_EdgeData;
        public ComponentLookup<TrainData> m_PrefabTrainData;
        public ComponentLookup<PrefabRef> m_PrefabRefData;
        public ComponentLookup<PublicTransportVehicleData> m_PublicTransportVehicleData;
        public ComponentLookup<CargoTransportVehicleData> m_CargoTransportVehicleData;
        public ComponentLookup<Waypoint> m_WaypointData;
        public ComponentLookup<Connected> m_ConnectedData;
        public ComponentLookup<BoardingVehicle> m_BoardingVehicleData;
        public ComponentLookup<Game.Routes.Color> m_RouteColorData;
        public ComponentLookup<Game.Companies.StorageCompany> m_StorageCompanyData;
        public ComponentLookup<Game.Buildings.TransportStation> m_TransportStationData;
        public ComponentLookup<CurrentVehicle> m_CurrentVehicleData;
        public BufferLookup<Passenger> m_Passengers;
        public BufferLookup<Resources> m_EconomyResources;
        public BufferLookup<RouteWaypoint> m_RouteWaypoints;
        public BufferLookup<ConnectedEdge> m_ConnectedEdges;
        public BufferLookup<Game.Net.SubLane> m_SubLanes;
        [NativeDisableParallelForRestriction] public ComponentLookup<Train> m_TrainData;
        [NativeDisableParallelForRestriction] public ComponentLookup<TrainCurrentLane> m_CurrentLaneData;
        [NativeDisableParallelForRestriction] public ComponentLookup<TrainNavigation> m_NavigationData;
        [NativeDisableParallelForRestriction] public BufferLookup<PathElement> m_PathElements;
        [NativeDisableParallelForRestriction] public BufferLookup<LoadingResources> m_LoadingResources;
        public uint m_SimulationFrameIndex;
        public RandomSeed m_RandomSeed;
        public EntityArchetype m_TransportVehicleRequestArchetype;
        public EntityArchetype m_HandleRequestArchetype;
        public TransportTrainCarriageSelectData m_TransportTrainCarriageSelectData;
        public EntityCommandBuffer.ParallelWriter m_CommandBuffer;
        public NativeQueue<SetupQueueItem>.ParallelWriter m_PathfindQueue;
        public TransportBoardingHelpers.BoardingData.Concurrent m_BoardingData;
        public ComponentTypeHandle<TransportVehicleStopTimer> m_Timer;
        public ComponentLookup<TransportVehicleStopTimer> m_TimerData;

        public void Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            var nativeArray1 = chunk.GetNativeArray(m_EntityType);
            var nativeArray2 = chunk.GetNativeArray(ref m_OwnerType);
            var nativeArray3 = chunk.GetNativeArray(ref m_PrefabRefType);
            var nativeArray4 = chunk.GetNativeArray(ref m_CurrentRouteType);
            var nativeArray5 =
                chunk.GetNativeArray(ref m_CargoTransportType);
            var nativeArray6 =
                chunk.GetNativeArray(ref m_PublicTransportType);
            var nativeArray7 = chunk.GetNativeArray(ref m_TargetType);
            var nativeArray8 = chunk.GetNativeArray(ref m_PathOwnerType);
            var nativeArray9 = chunk.GetNativeArray(ref m_OdometerType);
            var bufferAccessor1 =
                chunk.GetBufferAccessor(ref m_LayoutElementType);
            var bufferAccessor2 =
                chunk.GetBufferAccessor(ref m_NavigationLaneType);
            var bufferAccessor3 =
                chunk.GetBufferAccessor(ref m_ServiceDispatchType);
            var random = m_RandomSeed.GetRandom(unfilteredChunkIndex);
            var isUnspawned = chunk.Has(ref m_UnspawnedType);
            var timers = chunk.GetNativeArray(ref m_Timer);
            var hasTimer = chunk.Has(ref m_Timer);
            for (var index = 0; index < nativeArray1.Length; ++index)
            {
                var vehicleEntity = nativeArray1[index];
                var owner = nativeArray2[index];
                var prefabRef = nativeArray3[index];
                var pathOwner = nativeArray8[index];
                var target = nativeArray7[index];
                var odometer = nativeArray9[index];
                var layout = bufferAccessor1[index];
                var navigationLanes = bufferAccessor2[index];
                var serviceDispatches = bufferAccessor3[index];
                var currentRoute = new CurrentRoute();
                if (nativeArray4.Length != 0)
                    currentRoute = nativeArray4[index];
                var cargoTransport = new Game.Vehicles.CargoTransport();
                if (nativeArray5.Length != 0)
                    cargoTransport = nativeArray5[index];
                var publicTransport = new Game.Vehicles.PublicTransport();
                if (nativeArray6.Length != 0)
                    publicTransport = nativeArray6[index];
                var timer = new TransportVehicleStopTimer();
                if (hasTimer)
                {
                    timer = timers[index];
                }

                Tick(unfilteredChunkIndex, ref random, vehicleEntity, owner, prefabRef, currentRoute, layout,
                    navigationLanes, serviceDispatches, isUnspawned, ref cargoTransport, ref publicTransport,
                    ref pathOwner, ref target, ref odometer, ref timer, hasTimer);
                nativeArray8[index] = pathOwner;
                nativeArray7[index] = target;
                nativeArray9[index] = odometer;
                if (nativeArray5.Length != 0)
                    nativeArray5[index] = cargoTransport;
                if (nativeArray6.Length != 0)
                    nativeArray6[index] = publicTransport;
            }
        }

        private void Tick(
            int jobIndex,
            ref Random random,
            Entity vehicleEntity,
            Owner owner,
            PrefabRef prefabRef,
            CurrentRoute currentRoute,
            DynamicBuffer<LayoutElement> layout,
            DynamicBuffer<TrainNavigationLane> navigationLanes,
            DynamicBuffer<ServiceDispatch> serviceDispatches,
            bool isUnspawned,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref PathOwner pathOwner,
            ref Target target,
            ref Odometer odometer,
            ref TransportVehicleStopTimer timer,
            bool hasTimer)
        {
            if (VehicleUtils.ResetUpdatedPath(ref pathOwner))
            {
                DynamicBuffer<LoadingResources> bufferData;

                if (((cargoTransport.m_State & CargoTransportFlags.DummyTraffic) != (CargoTransportFlags)0 ||
                     (publicTransport.m_State & PublicTransportFlags.DummyTraffic) != (PublicTransportFlags)0) &&
                    m_LoadingResources.TryGetBuffer(vehicleEntity, out bufferData))
                {
                    if (bufferData.Length != 0)
                    {
                        QuantityUpdated(jobIndex, vehicleEntity, layout);
                    }


                    if (CheckLoadingResources(jobIndex, ref random, vehicleEntity, true, layout, bufferData))
                    {
                        pathOwner.m_State |= PathFlags.Updated;
                        return;
                    }
                }

                cargoTransport.m_State &= ~CargoTransportFlags.Arriving;
                publicTransport.m_State &= ~PublicTransportFlags.Arriving;

                var pathElement = m_PathElements[vehicleEntity];


                var length = VehicleUtils.CalculateLength(vehicleEntity, layout, m_PrefabRefData,
                    m_PrefabTrainData);
                var prevElement = new PathElement();
                if ((pathOwner.m_State & PathFlags.Append) != (PathFlags)0)
                {
                    if (navigationLanes.Length != 0)
                    {
                        var navigationLane = navigationLanes[navigationLanes.Length - 1];
                        prevElement = new PathElement(navigationLane.m_Lane, navigationLane.m_CurvePosition);
                    }
                }
                else
                {
                    if (VehicleUtils.IsReversedPath(pathElement, pathOwner, vehicleEntity, layout, m_CurveData,
                            m_CurrentLaneData, m_TrainData, m_TransformData))
                    {
                        VehicleUtils.ReverseTrain(vehicleEntity, layout, m_TrainData, m_CurrentLaneData,
                            m_NavigationData);
                    }
                }


                PathUtils.ExtendReverseLocations(prevElement, pathElement, pathOwner, length, m_CurveData,
                    m_LaneData, m_EdgeLaneData, m_OwnerData, m_EdgeData, m_ConnectedEdges,
                    m_SubLanes);


                if (!m_WaypointData.HasComponent(target.m_Target) ||
                    m_ConnectedData.HasComponent(target.m_Target) &&
                    m_BoardingVehicleData.HasComponent(m_ConnectedData[target.m_Target].m_Connected))
                {
                    var distance = length * 0.5f;


                    PathUtils.ExtendPath(pathElement, pathOwner, ref distance, ref m_CurveData,
                        ref m_LaneData, ref m_EdgeLaneData, ref m_OwnerData, ref m_EdgeData,
                        ref m_ConnectedEdges, ref m_SubLanes);
                }


                UpdatePantograph(layout);
            }

            var entity = vehicleEntity;
            if (layout.Length != 0)
                entity = layout[0].m_Vehicle;

            var currentLane = m_CurrentLaneData[entity];

            VehicleUtils.CheckUnspawned(jobIndex, vehicleEntity, currentLane, isUnspawned, m_CommandBuffer);
            var num = (cargoTransport.m_State & CargoTransportFlags.EnRoute) != (CargoTransportFlags)0
                ? 0
                : ((publicTransport.m_State & PublicTransportFlags.EnRoute) == (PublicTransportFlags)0 ? 1 : 0);

            if (m_PublicTransportVehicleData.HasComponent(prefabRef.m_Prefab))
            {
                var transportVehicleData = m_PublicTransportVehicleData[prefabRef.m_Prefab];
                if ((double)odometer.m_Distance >= (double)transportVehicleData.m_MaintenanceRange &&
                    (double)transportVehicleData.m_MaintenanceRange > 0.10000000149011612 &&
                    (publicTransport.m_State & PublicTransportFlags.Refueling) == (PublicTransportFlags)0)
                    publicTransport.m_State |= PublicTransportFlags.RequiresMaintenance;
            }

            var isCargoVehicle = false;

            if (m_CargoTransportVehicleData.HasComponent(prefabRef.m_Prefab))
            {
                var transportVehicleData = m_CargoTransportVehicleData[prefabRef.m_Prefab];
                if ((double)odometer.m_Distance >= (double)transportVehicleData.m_MaintenanceRange &&
                    (double)transportVehicleData.m_MaintenanceRange > 0.10000000149011612 &&
                    (cargoTransport.m_State & CargoTransportFlags.Refueling) == (CargoTransportFlags)0)
                    cargoTransport.m_State |= CargoTransportFlags.RequiresMaintenance;
                isCargoVehicle = true;
            }

            if (num != 0)
            {
                CheckServiceDispatches(vehicleEntity, serviceDispatches, ref cargoTransport, ref publicTransport);
                if (serviceDispatches.Length == 0 &&
                    (cargoTransport.m_State & (CargoTransportFlags.RequiresMaintenance |
                                               CargoTransportFlags.DummyTraffic | CargoTransportFlags.Disabled)) ==
                    (CargoTransportFlags)0 &&
                    (publicTransport.m_State & (PublicTransportFlags.RequiresMaintenance |
                                                PublicTransportFlags.DummyTraffic | PublicTransportFlags.Disabled)) ==
                    (PublicTransportFlags)0)
                {
                    RequestTargetIfNeeded(jobIndex, vehicleEntity, ref publicTransport, ref cargoTransport);
                }
            }
            else
            {
                serviceDispatches.Clear();
                cargoTransport.m_RequestCount = 0;
                publicTransport.m_RequestCount = 0;
            }

            var flag = false;

            if (!m_PrefabRefData.HasComponent(target.m_Target) || VehicleUtils.PathfindFailed(pathOwner))
            {
                if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != (CargoTransportFlags)0 ||
                    (publicTransport.m_State & PublicTransportFlags.Boarding) != (PublicTransportFlags)0)
                {
                    flag = true;

                    StopBoarding(jobIndex, ref random, vehicleEntity, currentRoute, layout, ref cargoTransport,
                        ref publicTransport, ref target, ref odometer, isCargoVehicle, true, hasTimer);
                }

                if (VehicleUtils.IsStuck(pathOwner) ||
                    (cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) !=
                    (CargoTransportFlags)0 ||
                    (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.DummyTraffic)) !=
                    (PublicTransportFlags)0)
                {
                    VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, vehicleEntity, layout);
                    return;
                }


                ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches, ref cargoTransport,
                    ref publicTransport, ref pathOwner, ref target);
            }
            else if (VehicleUtils.PathEndReached(currentLane))
            {
                if ((cargoTransport.m_State & (CargoTransportFlags.Returning | CargoTransportFlags.DummyTraffic)) !=
                    (CargoTransportFlags)0 ||
                    (publicTransport.m_State & (PublicTransportFlags.Returning | PublicTransportFlags.DummyTraffic)) !=
                    (PublicTransportFlags)0)
                {
                    if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != (CargoTransportFlags)0 ||
                        (publicTransport.m_State & PublicTransportFlags.Boarding) != (PublicTransportFlags)0)
                    {
                        if (StopBoarding(jobIndex, ref random, vehicleEntity, currentRoute, layout,
                                ref cargoTransport, ref publicTransport, ref target, ref odometer, isCargoVehicle,
                                false, hasTimer))
                        {
                            flag = true;

                            if (!SelectNextDispatch(jobIndex, vehicleEntity, currentRoute, layout, navigationLanes,
                                    serviceDispatches, ref cargoTransport, ref publicTransport, ref currentLane,
                                    ref pathOwner, ref target))
                            {
                                VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, vehicleEntity, layout);
                                return;
                            }
                        }
                    }
                    else
                    {
                        if ((CountPassengers(vehicleEntity, layout) <= 0 || !StartBoarding(jobIndex,
                                vehicleEntity, currentRoute, prefabRef, ref cargoTransport, ref publicTransport,
                                ref target, isCargoVehicle, hasTimer)) && !SelectNextDispatch(jobIndex, vehicleEntity,
                                currentRoute, layout, navigationLanes, serviceDispatches, ref cargoTransport,
                                ref publicTransport, ref currentLane, ref pathOwner, ref target))
                        {
                            VehicleUtils.DeleteVehicle(m_CommandBuffer, jobIndex, vehicleEntity, layout);
                            return;
                        }
                    }
                }
                else if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != (CargoTransportFlags)0 ||
                         (publicTransport.m_State & PublicTransportFlags.Boarding) != (PublicTransportFlags)0)
                {
                    if (StopBoarding(jobIndex, ref random, vehicleEntity, currentRoute, layout, ref cargoTransport,
                            ref publicTransport, ref target, ref odometer, isCargoVehicle, false, hasTimer))
                    {
                        flag = true;
                        if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == (CargoTransportFlags)0 &&
                            (publicTransport.m_State & PublicTransportFlags.EnRoute) == (PublicTransportFlags)0)
                        {
                            ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches,
                                ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                        }
                        else
                        {
                            SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                        }
                    }
                }
                else
                {
                    if (!m_RouteWaypoints.HasBuffer(currentRoute.m_Route) ||
                        !m_WaypointData.HasComponent(target.m_Target))
                    {
                        ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches,
                            ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                    }
                    else
                    {
                        if (!StartBoarding(jobIndex, vehicleEntity, currentRoute, prefabRef, ref cargoTransport,
                                ref publicTransport, ref target, isCargoVehicle, hasTimer))
                        {
                            if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == (CargoTransportFlags)0 &&
                                (publicTransport.m_State & PublicTransportFlags.EnRoute) == (PublicTransportFlags)0)
                            {
                                ReturnToDepot(jobIndex, vehicleEntity, currentRoute, owner, serviceDispatches,
                                    ref cargoTransport, ref publicTransport, ref pathOwner, ref target);
                            }
                            else
                            {
                                SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                            }
                        }
                    }
                }
            }
            else if (VehicleUtils.ReturnEndReached(currentLane))
            {
                VehicleUtils.ReverseTrain(vehicleEntity, layout, m_TrainData, m_CurrentLaneData,
                    m_NavigationData);
                entity = vehicleEntity;
                if (layout.Length != 0)
                    entity = layout[0].m_Vehicle;

                currentLane = m_CurrentLaneData[entity];

                UpdatePantograph(layout);
            }
            else if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != (CargoTransportFlags)0 ||
                     (publicTransport.m_State & PublicTransportFlags.Boarding) != (PublicTransportFlags)0)
            {
                flag = true;

                StopBoarding(jobIndex, ref random, vehicleEntity, currentRoute, layout, ref cargoTransport,
                    ref publicTransport, ref target, ref odometer, isCargoVehicle, true, hasTimer);
            }


            var train = m_TrainData[entity];
            train.m_Flags &= ~(Game.Vehicles.TrainFlags.BoardingLeft | Game.Vehicles.TrainFlags.BoardingRight);
            publicTransport.m_State &= ~(PublicTransportFlags.StopLeft | PublicTransportFlags.StopRight);
            var skipWaypoint = Entity.Null;
            if ((cargoTransport.m_State & CargoTransportFlags.Boarding) != (CargoTransportFlags)0 ||
                (publicTransport.m_State & PublicTransportFlags.Boarding) != (PublicTransportFlags)0)
            {
                if (!flag)
                {
                    var controllerTrain = m_TrainData[vehicleEntity];

                    UpdateStop(entity, controllerTrain, true, ref train, ref publicTransport, ref target);
                }
            }
            else if ((cargoTransport.m_State & CargoTransportFlags.Returning) != (CargoTransportFlags)0 ||
                     (publicTransport.m_State & PublicTransportFlags.Returning) != (PublicTransportFlags)0)
            {
                if (CountPassengers(vehicleEntity, layout) == 0)
                {
                    SelectNextDispatch(jobIndex, vehicleEntity, currentRoute, layout, navigationLanes,
                        serviceDispatches, ref cargoTransport, ref publicTransport, ref currentLane, ref pathOwner,
                        ref target);
                }
            }
            else if ((cargoTransport.m_State & CargoTransportFlags.Arriving) != (CargoTransportFlags)0 ||
                     (publicTransport.m_State & PublicTransportFlags.Arriving) != (PublicTransportFlags)0)
            {
                var controllerTrain = m_TrainData[vehicleEntity];

                UpdateStop(entity, controllerTrain, false, ref train, ref publicTransport, ref target);
            }
            else
            {
                CheckNavigationLanes(currentRoute, navigationLanes, ref cargoTransport, ref publicTransport,
                    ref currentLane, ref pathOwner, ref target, out skipWaypoint);
            }


            FindPathIfNeeded(vehicleEntity, prefabRef, skipWaypoint, ref currentLane, ref cargoTransport,
                ref publicTransport, ref pathOwner, ref target);

            m_TrainData[entity] = train;

            m_CurrentLaneData[entity] = currentLane;
        }

        private void UpdatePantograph(DynamicBuffer<LayoutElement> layout)
        {
            var flag = false;
            for (var index = 0; index < layout.Length; ++index)
            {
                var vehicle = layout[index].m_Vehicle;

                var train = m_TrainData[vehicle];


                var trainData = m_PrefabTrainData[m_PrefabRefData[vehicle].m_Prefab];
                if (flag || (trainData.m_TrainFlags & Game.Prefabs.TrainFlags.Pantograph) ==
                    (Game.Prefabs.TrainFlags)0)
                {
                    train.m_Flags &= ~Game.Vehicles.TrainFlags.Pantograph;
                }
                else
                {
                    train.m_Flags |= Game.Vehicles.TrainFlags.Pantograph;
                    flag = (trainData.m_TrainFlags & Game.Prefabs.TrainFlags.MultiUnit) != 0;
                }


                m_TrainData[vehicle] = train;
            }
        }

        private void UpdateStop(
            Entity vehicleEntity,
            Train controllerTrain,
            bool isBoarding,
            ref Train train,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref Target target)
        {
            var transform = m_TransformData[vehicleEntity];
            Connected componentData1;
            Transform componentData2;


            if (!m_ConnectedData.TryGetComponent(target.m_Target, out componentData1) ||
                !m_TransformData.TryGetComponent(componentData1.m_Connected, out componentData2))
                return;
            var flag = (double)math.dot(math.mul(transform.m_Rotation, math.right()),
                componentData2.m_Position - transform.m_Position) < 0.0;
            if (isBoarding)
            {
                if (flag)
                    train.m_Flags |= Game.Vehicles.TrainFlags.BoardingLeft;
                else
                    train.m_Flags |= Game.Vehicles.TrainFlags.BoardingRight;
            }

            if (flag ^ ((controllerTrain.m_Flags ^ train.m_Flags) & Game.Vehicles.TrainFlags.Reversed) >
                (Game.Vehicles.TrainFlags)0)
                publicTransport.m_State |= PublicTransportFlags.StopLeft;
            else
                publicTransport.m_State |= PublicTransportFlags.StopRight;
        }

        private void FindPathIfNeeded(
            Entity vehicleEntity,
            PrefabRef prefabRef,
            Entity skipWaypoint,
            ref TrainCurrentLane currentLane,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref PathOwner pathOwner,
            ref Target target)
        {
            if (!VehicleUtils.RequireNewPath(pathOwner))
                return;

            var trainData = m_PrefabTrainData[prefabRef.m_Prefab];
            var parameters = new PathfindParameters()
            {
                m_MaxSpeed = (float2)trainData.m_MaxSpeed,
                m_WalkSpeed = (float2)5.555556f,
                m_Weights = new PathfindWeights(1f, 1f, 1f, 1f),
                m_Methods = PathMethod.Track,
                m_IgnoredRules = RuleFlags.ForbidCombustionEngines | RuleFlags.ForbidHeavyTraffic |
                                 RuleFlags.ForbidPrivateTraffic | RuleFlags.ForbidSlowTraffic
            };
            var setupQueueTarget = new SetupQueueTarget();
            setupQueueTarget.m_Type = SetupTargetType.CurrentLocation;
            setupQueueTarget.m_Methods = PathMethod.Track;
            setupQueueTarget.m_TrackTypes = trainData.m_TrackType;
            var origin = setupQueueTarget;
            setupQueueTarget = new SetupQueueTarget();
            setupQueueTarget.m_Type = SetupTargetType.CurrentLocation;
            setupQueueTarget.m_Methods = PathMethod.Track;
            setupQueueTarget.m_TrackTypes = trainData.m_TrackType;
            setupQueueTarget.m_Entity = target.m_Target;
            var destination = setupQueueTarget;
            if (skipWaypoint != Entity.Null)
            {
                origin.m_Entity = skipWaypoint;
                pathOwner.m_State |= PathFlags.Append;
            }
            else
                pathOwner.m_State &= ~PathFlags.Append;

            if ((cargoTransport.m_State & (CargoTransportFlags.EnRoute | CargoTransportFlags.RouteSource)) ==
                (CargoTransportFlags.EnRoute | CargoTransportFlags.RouteSource) ||
                (publicTransport.m_State & (PublicTransportFlags.EnRoute | PublicTransportFlags.RouteSource)) ==
                (PublicTransportFlags.EnRoute | PublicTransportFlags.RouteSource))
                parameters.m_PathfindFlags = PathfindFlags.Stable | PathfindFlags.IgnoreFlow;
            else if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) == (CargoTransportFlags)0 &&
                     (publicTransport.m_State & PublicTransportFlags.EnRoute) == (PublicTransportFlags)0)
            {
                cargoTransport.m_State &= ~CargoTransportFlags.RouteSource;
                publicTransport.m_State &= ~PublicTransportFlags.RouteSource;
            }

            var setupQueueItem = new SetupQueueItem(vehicleEntity, parameters, origin, destination);

            VehicleUtils.SetupPathfind(ref currentLane, ref pathOwner, m_PathfindQueue, setupQueueItem);
        }

        private void CheckNavigationLanes(
            CurrentRoute currentRoute,
            DynamicBuffer<TrainNavigationLane> navigationLanes,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref TrainCurrentLane currentLane,
            ref PathOwner pathOwner,
            ref Target target,
            out Entity skipWaypoint)
        {
            skipWaypoint = Entity.Null;
            if (navigationLanes.Length == 0 || navigationLanes.Length >= 10)
                return;
            var navigationLane = navigationLanes[navigationLanes.Length - 1];
            if ((navigationLane.m_Flags & TrainLaneFlags.EndOfPath) == (TrainLaneFlags)0)
                return;


            if (m_WaypointData.HasComponent(target.m_Target) &&
                m_RouteWaypoints.HasBuffer(currentRoute.m_Route) &&
                (!m_ConnectedData.HasComponent(target.m_Target) ||
                 !m_BoardingVehicleData.HasComponent(m_ConnectedData[target.m_Target].m_Connected)))
            {
                if ((pathOwner.m_State & (PathFlags.Pending | PathFlags.Failed | PathFlags.Obsolete)) != (PathFlags)0)
                    return;
                skipWaypoint = target.m_Target;

                SetNextWaypointTarget(currentRoute, ref pathOwner, ref target);
                navigationLane.m_Flags &= ~TrainLaneFlags.EndOfPath;
                navigationLanes[navigationLanes.Length - 1] = navigationLane;
                cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                publicTransport.m_State |= PublicTransportFlags.RouteSource;
            }
            else
            {
                cargoTransport.m_State |= CargoTransportFlags.Arriving;
                publicTransport.m_State |= PublicTransportFlags.Arriving;
            }
        }

        private void SetNextWaypointTarget(
            CurrentRoute currentRoute,
            ref PathOwner pathOwnerData,
            ref Target targetData)
        {
            var routeWaypoint = m_RouteWaypoints[currentRoute.m_Route];

            var a = m_WaypointData[targetData.m_Target].m_Index + 1;
            var index = math.select(a, 0, a >= routeWaypoint.Length);
            VehicleUtils.SetTarget(ref pathOwnerData, ref targetData, routeWaypoint[index].m_Waypoint);
        }

        private void CheckServiceDispatches(
            Entity vehicleEntity,
            DynamicBuffer<ServiceDispatch> serviceDispatches,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport)
        {
            if (serviceDispatches.Length > 1)
                serviceDispatches.RemoveRange(1, serviceDispatches.Length - 1);
            cargoTransport.m_RequestCount = math.min(1, cargoTransport.m_RequestCount);
            publicTransport.m_RequestCount = math.min(1, publicTransport.m_RequestCount);
            var index1 = cargoTransport.m_RequestCount + publicTransport.m_RequestCount;
            if (serviceDispatches.Length <= index1)
                return;
            var num = -1f;
            var request1 = Entity.Null;
            for (var index2 = index1; index2 < serviceDispatches.Length; ++index2)
            {
                var request2 = serviceDispatches[index2].m_Request;

                if (m_TransportVehicleRequestData.HasComponent(request2))
                {
                    var transportVehicleRequest = m_TransportVehicleRequestData[request2];

                    if (m_PrefabRefData.HasComponent(transportVehicleRequest.m_Route) &&
                        (double)transportVehicleRequest.m_Priority > (double)num)
                    {
                        num = transportVehicleRequest.m_Priority;
                        request1 = request2;
                    }
                }
            }

            if (request1 != Entity.Null)
            {
                serviceDispatches[index1++] = new ServiceDispatch(request1);
                ++publicTransport.m_RequestCount;
                ++cargoTransport.m_RequestCount;
            }

            if (serviceDispatches.Length <= index1)
                return;
            serviceDispatches.RemoveRange(index1, serviceDispatches.Length - index1);
        }

        private void RequestTargetIfNeeded(
            int jobIndex,
            Entity entity,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref Game.Vehicles.CargoTransport cargoTransport)
        {
            if (m_TransportVehicleRequestData.HasComponent(publicTransport.m_TargetRequest) ||
                m_TransportVehicleRequestData.HasComponent(cargoTransport.m_TargetRequest) ||
                ((int)m_SimulationFrameIndex & (int)math.max(256U, 16U) - 1) != 3)
                return;


            var entity1 = m_CommandBuffer.CreateEntity(jobIndex, m_TransportVehicleRequestArchetype);

            m_CommandBuffer.SetComponent(jobIndex, entity1, new ServiceRequest(true));

            m_CommandBuffer.SetComponent(jobIndex, entity1,
                new TransportVehicleRequest(entity, 1f));

            m_CommandBuffer.SetComponent(jobIndex, entity1, new RequestGroup(8U));
        }

        private bool SelectNextDispatch(
            int jobIndex,
            Entity vehicleEntity,
            CurrentRoute currentRoute,
            DynamicBuffer<LayoutElement> layout,
            DynamicBuffer<TrainNavigationLane> navigationLanes,
            DynamicBuffer<ServiceDispatch> serviceDispatches,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref TrainCurrentLane currentLane,
            ref PathOwner pathOwner,
            ref Target target)
        {
            if ((cargoTransport.m_State & CargoTransportFlags.Returning) == (CargoTransportFlags)0 &&
                (publicTransport.m_State & PublicTransportFlags.Returning) == (PublicTransportFlags)0 &&
                cargoTransport.m_RequestCount + publicTransport.m_RequestCount > 0 && serviceDispatches.Length > 0)
            {
                serviceDispatches.RemoveAt(0);
                cargoTransport.m_RequestCount = math.max(0, cargoTransport.m_RequestCount - 1);
                publicTransport.m_RequestCount = math.max(0, publicTransport.m_RequestCount - 1);
            }

            if ((cargoTransport.m_State & (CargoTransportFlags.RequiresMaintenance | CargoTransportFlags.Disabled)) !=
                (CargoTransportFlags)0 ||
                (publicTransport.m_State &
                 (PublicTransportFlags.RequiresMaintenance | PublicTransportFlags.Disabled)) !=
                (PublicTransportFlags)0)
            {
                cargoTransport.m_RequestCount = 0;
                publicTransport.m_RequestCount = 0;
                serviceDispatches.Clear();
                return false;
            }

            for (;
                 cargoTransport.m_RequestCount + publicTransport.m_RequestCount > 0 && serviceDispatches.Length > 0;
                 publicTransport.m_RequestCount = math.max(0, publicTransport.m_RequestCount - 1))
            {
                var request = serviceDispatches[0].m_Request;
                var route = Entity.Null;
                var destination = Entity.Null;

                if (m_TransportVehicleRequestData.HasComponent(request))
                {
                    route = m_TransportVehicleRequestData[request].m_Route;

                    if (m_PathInformationData.HasComponent(request))
                    {
                        destination = m_PathInformationData[request].m_Destination;
                    }
                }


                if (!m_PrefabRefData.HasComponent(destination))
                {
                    serviceDispatches.RemoveAt(0);
                    cargoTransport.m_RequestCount = math.max(0, cargoTransport.m_RequestCount - 1);
                }
                else
                {
                    if (m_TransportVehicleRequestData.HasComponent(request))
                    {
                        serviceDispatches.Clear();
                        cargoTransport.m_RequestCount = 0;
                        publicTransport.m_RequestCount = 0;

                        if (m_PrefabRefData.HasComponent(route))
                        {
                            if (currentRoute.m_Route != route)
                            {
                                m_CommandBuffer.AddComponent(jobIndex, vehicleEntity,
                                    new CurrentRoute(route));

                                m_CommandBuffer.AppendToBuffer(jobIndex, route,
                                    new RouteVehicle(vehicleEntity));
                                Game.Routes.Color componentData;

                                if (m_RouteColorData.TryGetComponent(route, out componentData))
                                {
                                    m_CommandBuffer.AddComponent(jobIndex, vehicleEntity,
                                        componentData);

                                    UpdateBatches(jobIndex, vehicleEntity, layout);
                                }
                            }

                            cargoTransport.m_State |= CargoTransportFlags.EnRoute;
                            publicTransport.m_State |= PublicTransportFlags.EnRoute;
                        }
                        else
                        {
                            m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                        }


                        var entity = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);

                        m_CommandBuffer.SetComponent(jobIndex, entity,
                            new HandleRequest(request, vehicleEntity, true));
                    }
                    else
                    {
                        m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);


                        var entity = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);

                        m_CommandBuffer.SetComponent(jobIndex, entity,
                            new HandleRequest(request, vehicleEntity, false, true));
                    }

                    cargoTransport.m_State &= ~CargoTransportFlags.Returning;
                    publicTransport.m_State &= ~PublicTransportFlags.Returning;

                    if (m_TransportVehicleRequestData.HasComponent(publicTransport.m_TargetRequest))
                    {
                        var entity = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);

                        m_CommandBuffer.SetComponent(jobIndex, entity,
                            new HandleRequest(publicTransport.m_TargetRequest, Entity.Null, true));
                    }


                    if (m_TransportVehicleRequestData.HasComponent(cargoTransport.m_TargetRequest))
                    {
                        var entity = m_CommandBuffer.CreateEntity(jobIndex, m_HandleRequestArchetype);

                        m_CommandBuffer.SetComponent(jobIndex, entity,
                            new HandleRequest(cargoTransport.m_TargetRequest, Entity.Null, true));
                    }


                    if (m_PathElements.HasBuffer(request))
                    {
                        var pathElement1 = m_PathElements[request];
                        if (pathElement1.Length != 0)
                        {
                            var pathElement2 = m_PathElements[vehicleEntity];
                            PathUtils.TrimPath(pathElement2, ref pathOwner);

                            var num = math.max(cargoTransport.m_PathElementTime, publicTransport.m_PathElementTime) *
                                (float)pathElement2.Length + m_PathInformationData[request].m_Duration;
                            if (PathUtils.TryAppendPath(ref currentLane, navigationLanes, pathElement2, pathElement1))
                            {
                                cargoTransport.m_PathElementTime = num / (float)math.max(1, pathElement2.Length);
                                publicTransport.m_PathElementTime = cargoTransport.m_PathElementTime;
                                target.m_Target = destination;
                                VehicleUtils.ClearEndOfPath(ref currentLane, navigationLanes);
                                cargoTransport.m_State &= ~CargoTransportFlags.Arriving;
                                publicTransport.m_State &= ~PublicTransportFlags.Arriving;


                                var length = VehicleUtils.CalculateLength(vehicleEntity, layout, m_PrefabRefData,
                                    m_PrefabTrainData);
                                var prevElement = new PathElement();
                                if (navigationLanes.Length != 0)
                                {
                                    var navigationLane = navigationLanes[navigationLanes.Length - 1];
                                    prevElement = new PathElement(navigationLane.m_Lane,
                                        navigationLane.m_CurvePosition);
                                }


                                PathUtils.ExtendReverseLocations(prevElement, pathElement2, pathOwner, length,
                                    m_CurveData, m_LaneData, m_EdgeLaneData, m_OwnerData,
                                    m_EdgeData, m_ConnectedEdges, m_SubLanes);


                                if (!m_WaypointData.HasComponent(target.m_Target) ||
                                    m_ConnectedData.HasComponent(target.m_Target) &&
                                    m_BoardingVehicleData.HasComponent(m_ConnectedData[target.m_Target]
                                        .m_Connected))
                                {
                                    var distance = length * 0.5f;


                                    PathUtils.ExtendPath(pathElement2, pathOwner, ref distance, ref m_CurveData,
                                        ref m_LaneData, ref m_EdgeLaneData, ref m_OwnerData,
                                        ref m_EdgeData, ref m_ConnectedEdges, ref m_SubLanes);
                                }

                                return true;
                            }
                        }
                    }

                    VehicleUtils.SetTarget(ref pathOwner, ref target, destination);
                    return true;
                }
            }

            return false;
        }

        private void UpdateBatches(
            int jobIndex,
            Entity vehicleEntity,
            DynamicBuffer<LayoutElement> layout)
        {
            if (layout.Length != 0)
            {
                m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex,
                    layout.Reinterpret<Entity>().AsNativeArray());
            }
            else
            {
                m_CommandBuffer.AddComponent<BatchesUpdated>(jobIndex, vehicleEntity);
            }
        }

        private void ReturnToDepot(
            int jobIndex,
            Entity vehicleEntity,
            CurrentRoute currentRoute,
            Owner ownerData,
            DynamicBuffer<ServiceDispatch> serviceDispatches,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref PathOwner pathOwner,
            ref Target target)
        {
            serviceDispatches.Clear();
            cargoTransport.m_RequestCount = 0;
            cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.Refueling |
                                        CargoTransportFlags.AbandonRoute);
            cargoTransport.m_State |= CargoTransportFlags.Returning;
            publicTransport.m_RequestCount = 0;
            publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.Refueling |
                                         PublicTransportFlags.AbandonRoute);
            publicTransport.m_State |= PublicTransportFlags.Returning;

            m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
            VehicleUtils.SetTarget(ref pathOwner, ref target, ownerData.m_Owner);
        }

        private bool StartBoarding(
            int jobIndex,
            Entity vehicleEntity,
            CurrentRoute currentRoute,
            PrefabRef prefabRef,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref Target target,
            bool isCargoVehicle,
            bool hasTimer)
        {
            if (hasTimer)
            {
                m_TimerData.GetRefRW(vehicleEntity).ValueRW.StartFrame = m_SimulationFrameIndex;
            }

            if (m_ConnectedData.HasComponent(target.m_Target))
            {
                var connected = m_ConnectedData[target.m_Target];

                if (m_BoardingVehicleData.HasComponent(connected.m_Connected))
                {
                    var transportStationFromStop = GetTransportStationFromStop(connected.m_Connected);
                    var nextStorageCompany = Entity.Null;
                    var refuel = false;

                    if (m_TransportStationData.HasComponent(transportStationFromStop))
                    {
                        var trainData = m_PrefabTrainData[prefabRef.m_Prefab];

                        refuel = (m_TransportStationData[transportStationFromStop].m_TrainRefuelTypes &
                                  trainData.m_EnergyType) != 0;
                    }

                    if (!refuel &&
                        ((cargoTransport.m_State & CargoTransportFlags.RequiresMaintenance) !=
                         (CargoTransportFlags)0 ||
                         (publicTransport.m_State & PublicTransportFlags.RequiresMaintenance) !=
                         (PublicTransportFlags)0) ||
                        (cargoTransport.m_State & CargoTransportFlags.AbandonRoute) != (CargoTransportFlags)0 ||
                        (publicTransport.m_State & PublicTransportFlags.AbandonRoute) != (PublicTransportFlags)0)
                    {
                        cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.AbandonRoute);
                        publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.AbandonRoute);
                        if (currentRoute.m_Route != Entity.Null)
                        {
                            m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
                        }
                    }
                    else
                    {
                        cargoTransport.m_State &= ~CargoTransportFlags.RequiresMaintenance;
                        publicTransport.m_State &= ~PublicTransportFlags.RequiresMaintenance;
                        cargoTransport.m_State |= CargoTransportFlags.EnRoute;
                        publicTransport.m_State |= PublicTransportFlags.EnRoute;
                        if (isCargoVehicle)
                        {
                            nextStorageCompany = GetNextStorageCompany(currentRoute.m_Route, target.m_Target);
                        }
                    }

                    cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                    publicTransport.m_State |= PublicTransportFlags.RouteSource;
                    var storageCompanyFromStop = Entity.Null;
                    if (isCargoVehicle)
                    {
                        storageCompanyFromStop = GetStorageCompanyFromStop(connected.m_Connected);
                    }


                    m_BoardingData.BeginBoarding(vehicleEntity, currentRoute.m_Route, connected.m_Connected,
                        target.m_Target, storageCompanyFromStop, nextStorageCompany, refuel);
                    return true;
                }
            }


            if (m_WaypointData.HasComponent(target.m_Target))
            {
                cargoTransport.m_State |= CargoTransportFlags.RouteSource;
                publicTransport.m_State |= PublicTransportFlags.RouteSource;
                return false;
            }

            cargoTransport.m_State &= ~(CargoTransportFlags.EnRoute | CargoTransportFlags.AbandonRoute);
            publicTransport.m_State &= ~(PublicTransportFlags.EnRoute | PublicTransportFlags.AbandonRoute);
            if (currentRoute.m_Route != Entity.Null)
            {
                m_CommandBuffer.RemoveComponent<CurrentRoute>(jobIndex, vehicleEntity);
            }

            return false;
        }

        private bool TryChangeCarriagePrefab(
            int jobIndex,
            ref Random random,
            Entity vehicleEntity,
            bool dummyTraffic,
            DynamicBuffer<LoadingResources> loadingResources)
        {
            if (m_EconomyResources.HasBuffer(vehicleEntity))
            {
                var economyResource = m_EconomyResources[vehicleEntity];

                var prefabRef = m_PrefabRefData[vehicleEntity];

                if (economyResource.Length == 0 && m_CargoTransportVehicleData.HasComponent(prefabRef.m_Prefab))
                {
                    while (loadingResources.Length > 0)
                    {
                        var loadingResource = loadingResources[0];

                        var entity = m_TransportTrainCarriageSelectData.SelectCarriagePrefab(ref random,
                            loadingResource.m_Resource, loadingResource.m_Amount);
                        if (entity != Entity.Null)
                        {
                            var transportVehicleData = m_CargoTransportVehicleData[entity];
                            var num = math.min(loadingResource.m_Amount, transportVehicleData.m_CargoCapacity);
                            loadingResource.m_Amount -= transportVehicleData.m_CargoCapacity;
                            if (loadingResource.m_Amount <= 0)
                                loadingResources.RemoveAt(0);
                            else
                                loadingResources[0] = loadingResource;
                            if (dummyTraffic)
                            {
                                m_CommandBuffer.SetBuffer<Resources>(jobIndex, vehicleEntity).Add(new Resources()
                                {
                                    m_Resource = loadingResource.m_Resource,
                                    m_Amount = num
                                });
                            }


                            m_CommandBuffer.SetComponent(jobIndex, vehicleEntity,
                                new PrefabRef(entity));

                            m_CommandBuffer.AddComponent(jobIndex, vehicleEntity, new Updated());
                            return true;
                        }

                        loadingResources.RemoveAt(0);
                    }
                }
            }

            return false;
        }

        private bool CheckLoadingResources(
            int jobIndex,
            ref Random random,
            Entity vehicleEntity,
            bool dummyTraffic,
            DynamicBuffer<LayoutElement> layout,
            DynamicBuffer<LoadingResources> loadingResources)
        {
            var flag = false;
            if (loadingResources.Length != 0)
            {
                if (layout.Length != 0)
                {
                    for (var index = 0; index < layout.Length && loadingResources.Length != 0; ++index)
                    {
                        flag |= TryChangeCarriagePrefab(jobIndex, ref random, layout[index].m_Vehicle,
                            dummyTraffic, loadingResources);
                    }
                }
                else
                {
                    flag |= TryChangeCarriagePrefab(jobIndex, ref random, vehicleEntity, dummyTraffic,
                        loadingResources);
                }

                loadingResources.Clear();
            }

            return flag;
        }

        private bool StopBoarding(
            int jobIndex,
            ref Random random,
            Entity vehicleEntity,
            CurrentRoute currentRoute,
            DynamicBuffer<LayoutElement> layout,
            ref Game.Vehicles.CargoTransport cargoTransport,
            ref Game.Vehicles.PublicTransport publicTransport,
            ref Target target,
            ref Odometer odometer,
            bool isCargoVehicle,
            bool forcedStop,
            bool hasTimer)
        {
            var flag1 = false;

            if (hasTimer)
            {
                if (m_TimerData.GetRefRW(vehicleEntity).ValueRW.ShouldStop(m_SimulationFrameIndex))
                {
                    forcedStop = true;
                    m_TimerData.GetRefRW(vehicleEntity).ValueRW.StartFrame = 0;
                }
            }

            if (m_LoadingResources.HasBuffer(vehicleEntity))
            {
                var loadingResource = m_LoadingResources[vehicleEntity];
                if (forcedStop)
                {
                    loadingResource.Clear();
                }
                else
                {
                    var dummyTraffic =
                        (cargoTransport.m_State & CargoTransportFlags.DummyTraffic) != (CargoTransportFlags)0 ||
                        (publicTransport.m_State & PublicTransportFlags.DummyTraffic) > (PublicTransportFlags)0;

                    flag1 |= CheckLoadingResources(jobIndex, ref random, vehicleEntity, dummyTraffic, layout,
                        loadingResource);
                }
            }

            if (flag1)
                return false;
            var flag2 = false;
            Connected componentData1;
            BoardingVehicle componentData2;


            if (m_ConnectedData.TryGetComponent(target.m_Target, out componentData1) &&
                m_BoardingVehicleData.TryGetComponent(componentData1.m_Connected, out componentData2))
                flag2 = componentData2.m_Vehicle == vehicleEntity;
            if (!forcedStop)
            {
                if (flag2 && (m_SimulationFrameIndex < cargoTransport.m_DepartureFrame ||
                              m_SimulationFrameIndex < publicTransport.m_DepartureFrame))
                    return false;
                if (layout.Length != 0)
                {
                    for (var index = 0; index < layout.Length; ++index)
                    {
                        if (!ArePassengersReady(layout[index].m_Vehicle))
                            return false;
                    }
                }
                else
                {
                    if (!ArePassengersReady(vehicleEntity))
                        return false;
                }
            }

            if ((cargoTransport.m_State & CargoTransportFlags.Refueling) != (CargoTransportFlags)0 ||
                (publicTransport.m_State & PublicTransportFlags.Refueling) != (PublicTransportFlags)0)
                odometer.m_Distance = 0.0f;
            if (isCargoVehicle)
            {
                QuantityUpdated(jobIndex, vehicleEntity, layout);
            }

            if (flag2)
            {
                var storageCompanyFromStop = Entity.Null;
                var nextStorageCompany = Entity.Null;
                if (isCargoVehicle && !forcedStop)
                {
                    storageCompanyFromStop = GetStorageCompanyFromStop(componentData1.m_Connected);
                    if ((cargoTransport.m_State & CargoTransportFlags.EnRoute) != (CargoTransportFlags)0)
                    {
                        nextStorageCompany = GetNextStorageCompany(currentRoute.m_Route, target.m_Target);
                    }
                }


                m_BoardingData.EndBoarding(vehicleEntity, currentRoute.m_Route, componentData1.m_Connected,
                    target.m_Target, storageCompanyFromStop, nextStorageCompany);
                return true;
            }

            cargoTransport.m_State &= ~(CargoTransportFlags.Boarding | CargoTransportFlags.Refueling);
            publicTransport.m_State &= ~(PublicTransportFlags.Boarding | PublicTransportFlags.Refueling);
            return true;
        }

        private void QuantityUpdated(
            int jobIndex,
            Entity vehicleEntity,
            DynamicBuffer<LayoutElement> layout)
        {
            if (layout.Length != 0)
            {
                for (var index = 0; index < layout.Length; ++index)
                {
                    m_CommandBuffer.AddComponent(jobIndex, layout[index].m_Vehicle, new Updated());
                }
            }
            else
            {
                m_CommandBuffer.AddComponent(jobIndex, vehicleEntity, new Updated());
            }
        }

        private int CountPassengers(Entity vehicleEntity, DynamicBuffer<LayoutElement> layout)
        {
            var num = 0;
            if (layout.Length != 0)
            {
                for (var index = 0; index < layout.Length; ++index)
                {
                    var vehicle = layout[index].m_Vehicle;

                    if (m_Passengers.HasBuffer(vehicle))
                    {
                        num += m_Passengers[vehicle].Length;
                    }
                }
            }
            else
            {
                if (m_Passengers.HasBuffer(vehicleEntity))
                {
                    num += m_Passengers[vehicleEntity].Length;
                }
            }

            return num;
        }

        private bool ArePassengersReady(Entity vehicleEntity)
        {
            if (!m_Passengers.HasBuffer(vehicleEntity))
                return true;

            var passenger1 = m_Passengers[vehicleEntity];
            for (var index = 0; index < passenger1.Length; ++index)
            {
                var passenger2 = passenger1[index].m_Passenger;


                if (m_CurrentVehicleData.HasComponent(passenger2) &&
                    (m_CurrentVehicleData[passenger2].m_Flags & CreatureVehicleFlags.Ready) ==
                    (CreatureVehicleFlags)0)
                    return false;
            }

            return true;
        }

        private Entity GetTransportStationFromStop(Entity stop)
        {
            for (; !m_TransportStationData.HasComponent(stop); stop = m_OwnerData[stop].m_Owner)
            {
                if (!m_OwnerData.HasComponent(stop))
                    return Entity.Null;
            }


            if (m_OwnerData.HasComponent(stop))
            {
                var owner = m_OwnerData[stop].m_Owner;

                if (m_TransportStationData.HasComponent(owner))
                    return owner;
            }

            return stop;
        }

        private Entity GetStorageCompanyFromStop(Entity stop)
        {
            for (; !m_StorageCompanyData.HasComponent(stop); stop = m_OwnerData[stop].m_Owner)
            {
                if (!m_OwnerData.HasComponent(stop))
                    return Entity.Null;
            }

            return stop;
        }

        private Entity GetNextStorageCompany(Entity route, Entity currentWaypoint)
        {
            var routeWaypoint = m_RouteWaypoints[route];

            var a = m_WaypointData[currentWaypoint].m_Index + 1;
            for (var index1 = 0; index1 < routeWaypoint.Length; ++index1)
            {
                var index2 = math.select(a, 0, a >= routeWaypoint.Length);
                var waypoint = routeWaypoint[index2].m_Waypoint;

                if (m_ConnectedData.HasComponent(waypoint))
                {
                    var storageCompanyFromStop =
                        GetStorageCompanyFromStop(m_ConnectedData[waypoint].m_Connected);
                    if (storageCompanyFromStop != Entity.Null)
                        return storageCompanyFromStop;
                }

                a = index2 + 1;
            }

            return Entity.Null;
        }

        void IJobChunk.Execute(
            in ArchetypeChunk chunk,
            int unfilteredChunkIndex,
            bool useEnabledMask,
            in v128 chunkEnabledMask)
        {
            Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
        }
    }
}