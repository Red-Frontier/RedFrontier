using System;
using System.Linq;
using Contracts;
using ContractConfigurator;
using ContractConfigurator.Behaviour;
using ContractConfigurator.Parameters;
using FinePrint;
using UnityEngine;

namespace RedFrontier
{
    /// <summary>
    /// Contract parameter that detects a deployed Breaking Ground science vessel
    /// (Central Station or individual instrument) within a specified distance of a
    /// WaypointGenerator waypoint.
    ///
    /// ARCHITECTURE: Subclasses VesselParameter rather than ContractConfiguratorParameter.
    /// This is the critical architectural decision that fixes the reliability problems
    /// present in the prototype DeployedScienceNearWaypoint extension:
    ///
    ///   1. OnUpdate suppression — CC container parameters (Sequence, VesselParameterGroup,
    ///      All) correctly register and update VesselParameter children. Non-VesselParameter
    ///      children using ContractConfiguratorParameter are not guaranteed to receive OnUpdate
    ///      calls when nested inside containers, producing the ~60% completion rate observed
    ///      in Pathfinder 2 with four competing instances.
    ///
    ///   2. Sequence nesting — VesselParameter children are correctly hidden/shown by the
    ///      Sequence container. ContractConfiguratorParameter children are not, preventing
    ///      the progressive UI disclosure pattern used throughout the campaign.
    ///
    ///   3. Polling vs. events — the prototype polled FlightGlobals.Vessels every 3 seconds
    ///      via OnUpdate. This extension subscribes to GameEvents.onVesselCreate and
    ///      GameEvents.onVesselLoaded instead. Detection is immediate and not subject to
    ///      timer drift or missed frames.
    ///
    /// VesselMeetsCondition is overridden to always return false. This parameter is a
    /// world-state query, not an active-vessel query. The deployed station is never the
    /// active vessel — it is a separate vessel the kerbal has walked away from. We ignore
    /// VesselParameter's vessel-tracking machinery entirely and use only its lifecycle
    /// hooks (OnRegister / OnUnregister) and its correct container registration behaviour.
    ///
    /// VESSEL TYPE DETECTION:
    ///   - Default (partName omitted): matches VesselType.DeployedScienceController.
    ///     This is the Central Station vessel — the primary hub of any deployed outpost.
    ///
    ///   - With partName specified: matches VesselType.DeployedSciencePart vessels whose
    ///     first ProtoPartSnapshot has a partName equal to the configured value. This
    ///     detects individual instruments (seismic sensor, solar panel, etc.) by their
    ///     internal part name from the PART{} config node.
    ///     Example: partName = DeployedSeismicSensor
    ///
    /// OPTIONAL VESSEL BINDING (define field):
    ///   When define is set, the first matching vessel is registered with
    ///   ContractVesselTracker.Instance.AssociateVessel(define, vessel) before the
    ///   parameter completes. The vessel becomes addressable as @/defineName in downstream
    ///   DATA blocks and as vessel = defineName in Rendezvous parameters, within the same
    ///   contract or in future contracts that reference the tracker.
    ///   define is optional — omit it when vessel identity is not needed downstream.
    ///
    /// CONFIG USAGE:
    ///   PARAMETER
    ///   {
    ///       name  = StationAlphaDetected
    ///       type  = DeployedScienceStation
    ///       waypointIndex = 0          // zero-based; matches WaypointGenerator order
    ///       distance      = 2500       // metres from waypoint centre; default 500
    ///       // partName = DeployedSeismicSensor  // optional; omit to detect Central Station
    ///       // define   = alphaStation           // optional; names the vessel for downstream use
    ///       title         = Deploy Experiment Control Station
    ///       hidden        = true
    ///   }
    ///
    /// KNOWN LIMITATIONS:
    ///   - partName matching reads protoVessel.protoPartSnapshots[0].partName. Instruments
    ///     deployed by a kerbal become unloaded vessels almost immediately; the proto
    ///     snapshot is authoritative for unloaded vessels and is always present.
    ///   - The define binding calls ContractVesselTracker.Instance.AssociateVessel, which
    ///     is the same API used by VesselParameterGroup. If ContractVesselTracker.Instance
    ///     is null at completion time (should not happen during normal play), the binding
    ///     is skipped with a log warning and the parameter still completes.
    ///   - One parameter instance per waypoint. For multi-site missions requiring both a
    ///     Central Station and a specific instrument at each site, use two instances with
    ///     matching waypointIndex values — one without partName (controller) and one with.
    /// </summary>
    public class DeployedScienceStation : VesselParameter
    {
        // ── Configurable fields ──────────────────────────────────────────────

        /// <summary>Zero-based index into the contract's WaypointGenerator waypoint list.</summary>
        private int waypointIndex;

        /// <summary>Maximum surface distance in metres from the waypoint centre.</summary>
        private float distance;

        /// <summary>
        /// Optional internal part name filter (e.g. "DeployedSeismicSensor").
        /// When null or empty, the parameter matches VesselType.DeployedScienceController.
        /// When set, matches VesselType.DeployedSciencePart with a matching proto part name.
        /// </summary>
        private string partName;

        /// <summary>
        /// Optional vessel binding name. When set, the matched vessel is registered with
        /// ContractVesselTracker under this key so downstream parameters can reference it.
        /// </summary>
        private string defineVessel;

        // ── Constructors ─────────────────────────────────────────────────────

        /// <summary>No-arg constructor for CC deserialisation path.</summary>
        public DeployedScienceStation() : base() { }

        /// <summary>Full constructor called by DeployedScienceStationFactory.Generate().</summary>
        public DeployedScienceStation(
            int    waypointIndex,
            float  distance,
            string partName,
            string defineVessel,
            string title)
            : base(title)
        {
            this.waypointIndex = waypointIndex;
            this.distance      = distance;
            this.partName      = partName;
            this.defineVessel  = defineVessel;

            if (string.IsNullOrEmpty(this.title))
                this.title = BuildAutoTitle();
        }

        // ── Persistence ──────────────────────────────────────────────────────

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            node.AddValue("waypointIndex", waypointIndex);
            node.AddValue("distance",      distance);
            if (!string.IsNullOrEmpty(partName))     node.AddValue("partName",     partName);
            if (!string.IsNullOrEmpty(defineVessel)) node.AddValue("defineVessel", defineVessel);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            waypointIndex = ConfigNodeUtil.ParseValue<int>   (node, "waypointIndex", 0);
            distance      = ConfigNodeUtil.ParseValue<float> (node, "distance",      500f);
            partName      = ConfigNodeUtil.ParseValue<string>(node, "partName",      (string)null);
            defineVessel  = ConfigNodeUtil.ParseValue<string>(node, "defineVessel",  (string)null);
        }

        // ── Title ────────────────────────────────────────────────────────────

        protected override string GetParameterTitle()
        {
            return string.IsNullOrEmpty(title) ? BuildAutoTitle() : title;
        }

        private string BuildAutoTitle()
        {
            return string.IsNullOrEmpty(partName)
                ? $"Deploy Experiment Control Station near waypoint {waypointIndex}"
                : $"Deploy {partName} near waypoint {waypointIndex}";
        }

        // ── Lifecycle — event subscription ───────────────────────────────────

        protected override void OnRegister()
        {
            base.OnRegister();

            // Event-driven detection replaces the 3-second polling loop used in the
            // prototype. onVesselCreate fires when KSP creates the new vessel entry
            // immediately after EVA deployment. onVesselLoaded covers the case where
            // the player saves and reloads mid-mission — the vessel already exists in
            // the save file and is loaded into FlightGlobals on scene load.
            GameEvents.onVesselCreate.Add(OnVesselEvent);
            GameEvents.onVesselLoaded.Add(OnVesselEvent);

            // Run an immediate check on registration. If the player already deployed
            // the station before the contract was accepted (edge case), or if the
            // contract is reloaded from a save where deployment already happened, this
            // catches it without waiting for the next vessel event.
            CheckAllVessels();
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.onVesselCreate.Remove(OnVesselEvent);
            GameEvents.onVesselLoaded.Remove(OnVesselEvent);
        }

        // ── VesselParameter contract — intentionally unused ──────────────────

        /// <summary>
        /// Not used. This parameter is a world-state query, not an active-vessel query.
        /// VesselParameter.CheckVessel() is never called for deployed science vessels
        /// because they are never the active vessel. Return false unconditionally so that
        /// any accidental calls from the base class machinery do not falsely complete the
        /// parameter against the player's current craft.
        /// </summary>
        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            return false;
        }

        // ── Detection ────────────────────────────────────────────────────────

        /// <summary>
        /// GameEvent handler. Called when any vessel is created or loaded.
        /// Runs the spatial query and completes the parameter if a match is found.
        /// </summary>
        private void OnVesselEvent(Vessel vessel)
        {
            if (state == ParameterState.Complete) return;
            if (VesselMatchesTarget(vessel) && IsNearWaypoint(vessel))
                Complete(vessel);
        }

        /// <summary>
        /// Scans all current vessels. Called once on registration to handle the
        /// save-reload case where the vessel already exists when the parameter activates.
        /// </summary>
        private void CheckAllVessels()
        {
            if (state == ParameterState.Complete) return;

            foreach (Vessel v in FlightGlobals.Vessels)
            {
                if (VesselMatchesTarget(v) && IsNearWaypoint(v))
                {
                    Complete(v);
                    return;
                }
            }
        }

        /// <summary>
        /// Returns true if the vessel matches the configured type and optional part filter.
        /// </summary>
        private bool VesselMatchesTarget(Vessel vessel)
        {
            if (vessel == null) return false;

            // No partName configured — match Central Station controller vessels only.
            if (string.IsNullOrEmpty(partName))
                return vessel.vesselType == VesselType.DeployedScienceController;

            // partName configured — match DeployedSciencePart vessels with a matching
            // part name in their proto snapshot. Instruments become unloaded vessels
            // almost immediately after deployment; the proto snapshot is the correct
            // place to read part identity for unloaded vessels.
            if (vessel.vesselType != VesselType.DeployedSciencePart)
                return false;

            // Check proto snapshot first (covers unloaded vessels).
            if (vessel.protoVessel?.protoPartSnapshots != null &&
                vessel.protoVessel.protoPartSnapshots.Count > 0)
            {
                string protoName = vessel.protoVessel.protoPartSnapshots[0].partName;
                if (string.Equals(protoName, partName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Fallback: check live parts if vessel happens to be loaded.
            if (vessel.loaded && vessel.parts != null && vessel.parts.Count > 0)
            {
                string liveName = vessel.parts[0].partInfo?.name;
                if (string.Equals(liveName, partName, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns true if the vessel is within the configured distance of the target waypoint.
        /// Resolves the WaypointGenerator lazily from the contract's behaviour list.
        /// </summary>
        private bool IsNearWaypoint(Vessel vessel)
        {
            // ── Resolve WaypointGenerator ────────────────────────────────────
            ConfiguredContract configuredContract = Root as ConfiguredContract;
            if (configuredContract == null)
            {
                LogError("Root is not a ConfiguredContract — cannot access behaviours.");
                return false;
            }

            WaypointGenerator wpGen = configuredContract.Behaviours
                .OfType<WaypointGenerator>()
                .FirstOrDefault();

            if (wpGen == null)
            {
                LogError("No WaypointGenerator behaviour found on contract.");
                return false;
            }

            // ── Resolve waypoint ─────────────────────────────────────────────
            Waypoint wp = wpGen.GetWaypoint(waypointIndex);
            if (wp == null)
            {
                LogError($"WaypointGenerator returned null for index {waypointIndex}.");
                return false;
            }

            // ── Resolve celestial body ───────────────────────────────────────
            CelestialBody body = FlightGlobals.Bodies
                .FirstOrDefault(b => b.name == wp.celestialName);

            if (body == null)
            {
                LogError($"CelestialBody '{wp.celestialName}' not found.");
                return false;
            }

            // Vessel must be on the same body as the waypoint.
            if (vessel.mainBody != body) return false;

            // ── Distance check ───────────────────────────────────────────────
            // Altitude 0: both the waypoint and the deployed instrument rest on the
            // surface. Terrain relief error at ≤2500m on Minmus/Mun is negligible.
            Vector3d wpWorldPos = body.GetWorldSurfacePosition(wp.latitude, wp.longitude, 0.0);
            return Vector3d.Distance(vessel.GetWorldPos3D(), wpWorldPos) <= distance;
        }

        /// <summary>
        /// Completes the parameter, optionally binding the matched vessel to the define name.
        /// </summary>
        private void Complete(Vessel vessel)
        {
            // Optional vessel binding for downstream Rendezvous or DATA references.
            if (!string.IsNullOrEmpty(defineVessel))
            {
                if (ContractVesselTracker.Instance != null)
                {
                    ContractVesselTracker.Instance.AssociateVessel(defineVessel, vessel);
                    LogInfo($"Vessel '{vessel.vesselName}' associated as '{defineVessel}'.");
                }
                else
                {
                    LogWarning("ContractVesselTracker.Instance is null — define binding skipped.");
                }
            }

            SetState(ParameterState.Complete);
        }

        // ── Logging helpers ──────────────────────────────────────────────────

        private string LogPrefix => $"[RedFrontier] DeployedScienceStation (index={waypointIndex}" +
                                    $"{(string.IsNullOrEmpty(partName) ? "" : $", part={partName}")})";

        private void LogInfo(string message)    => Debug.Log($"{LogPrefix}: {message}");
        private void LogWarning(string message) => Debug.LogWarning($"{LogPrefix}: {message}");
        private void LogError(string message)   => Debug.LogError($"{LogPrefix}: {message}");
    }
}
