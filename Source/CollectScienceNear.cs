// CollectScienceNear.cs
// Red Frontier - Custom Contract Configurator Parameter
//
// Completes when a specific science experiment is collected while the active
// vessel is within a specified radius of a WaypointGenerator waypoint.
//
// Hooks GameEvents.OnExperimentDeployed (the scan moment) rather than
// OnScienceRecieved (the award moment — fires late and may not fire at all
// for exhausted subjects). Proximity is checked at event time.
//
// Must NOT be placed inside a VesselParameterGroup. Place as a sibling of
// VisitWaypoint and Duration inside an All container.

using System;
using System.Linq;
using Contracts;
using ContractConfigurator;
using ContractConfigurator.Behaviour;
using ContractConfigurator.Parameters;
using UnityEngine;
using FinePrint;

namespace RedFrontier.ContractParameters
{
    public class CollectScienceNear : ContractConfiguratorParameter
    {
        // ── Config fields ─────────────────────────────────────────────────────

        /// <summary>Experiment ID substring to match against subject.id.</summary>
        private string experiment;

        /// <summary>Zero-based index into the contract's WaypointGenerator list.</summary>
        private int waypointIndex;

        /// <summary>Radius in metres. Active vessel must be within this distance.</summary>
        private double distance;

        // ── Constructors ──────────────────────────────────────────────────────

        public CollectScienceNear() : base() { }

        public CollectScienceNear(
            string experiment,
            int    waypointIndex,
            double distance,
            string title)
            : base(title)
        {
            this.experiment  = experiment;
            this.waypointIndex = waypointIndex;
            this.distance      = distance;
        }

        // ── Persistence ───────────────────────────────────────────────────────

        protected override void OnParameterSave(ConfigNode node)
        {
            node.AddValue("experiment",  experiment);
            node.AddValue("waypointIndex", waypointIndex);
            node.AddValue("distance",      distance);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            experiment  = ConfigNodeUtil.ParseValue<string>(node, "experiment",  string.Empty);
            waypointIndex = ConfigNodeUtil.ParseValue<int>   (node, "waypointIndex", 0);
            distance      = ConfigNodeUtil.ParseValue<double>(node, "distance",      500.0);
        }

        // ── Title ─────────────────────────────────────────────────────────────

        protected override string GetTitle()
        {
            if (!string.IsNullOrEmpty(title)) return title;
            return $"Collect {experiment} within {distance}m of waypoint {waypointIndex}";
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void OnRegister()
        {
            base.OnRegister();
            // OnExperimentDeployed fires at scan time — correct for ROC arm scans.
            // OnScienceRecieved fires at award time which may be later or never
            // (exhausted subjects still fire OnExperimentDeployed).
            GameEvents.OnExperimentDeployed.Add(
                new EventData<ScienceData>.OnEvent(OnExperimentDeployed));
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            GameEvents.OnExperimentDeployed.Remove(
                new EventData<ScienceData>.OnEvent(OnExperimentDeployed));
        }

        // ── Event handler ─────────────────────────────────────────────────────

        private void OnExperimentDeployed(ScienceData scienceData)
        {
            if (State == ParameterState.Complete) return;
            if (scienceData == null) return;

            // subject.id format: "ROCScience_MinmusGreenSandstone@Minmus<biome>"
            // Contains() matches the experiment ID prefix regardless of biome suffix.
            if (!scienceData.subjectID.Contains(experiment)) return;

            Vessel vessel = FlightGlobals.ActiveVessel;
            if (vessel == null) return;

            // Debug: log the raw subjectID so we can verify the format in KSP.log
            LoggingUtil.LogDebug(this,
                "CollectScienceNear: OnExperimentDeployed subjectID = '{0}'",
                new object[] { scienceData.subjectID });

            if (!IsVesselNearWaypoint(vessel)) return;

            SetComplete();
        }

        // ── Proximity check ───────────────────────────────────────────────────

        private bool IsVesselNearWaypoint(Vessel vessel)
        {
            FinePrint.Waypoint wp = FetchWaypoint();
            if (wp == null) return false;

            if (vessel.mainBody.name != wp.celestialName) return false;

            // WaypointUtil.GetDistanceToWaypoint is the same call VisitWaypoint uses.
            // It handles surface/altitude geometry correctly. The ref height parameter
            // is a cache for repeated calls — we pass a discard since we call once.
            double height = double.MaxValue;
            double actualDistance = WaypointUtil.GetDistanceToWaypoint(vessel, wp, ref height);

            LoggingUtil.LogDebug(this,
                "CollectScienceNear: distance to waypoint {0} = {1}m (limit {2}m)",
                new object[] { waypointIndex, actualDistance, distance });

            return actualDistance <= distance;
        }

        private FinePrint.Waypoint FetchWaypoint()
        {
            if (Root == null) return null;

            // Mirrors VisitWaypoint.FetchWaypoint() exactly.
            var waypointGenerators = ((ConfiguredContract)Root)
                .Behaviours
                .OfType<WaypointGenerator>();

            Waypoint wp = waypointGenerators
                .SelectMany(wg => wg.Waypoints())
                .ElementAtOrDefault(waypointIndex);

            if (wp == null)
            {
                LoggingUtil.LogError(this,
                    "CollectScienceNear: no waypoint at index {0}.",
                    new object[] { waypointIndex });
            }

            return wp;
        }

        // ContractConfiguratorParameter requires this override. No polling needed —
        // we're entirely event-driven.
        protected override void OnUpdate() { }
    }
}
