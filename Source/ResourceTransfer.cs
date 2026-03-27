// ResourceTransfer.cs
// Red Frontier - Custom Contract Configurator VesselParameter
//
// Validates that a resource quantity on a vessel has changed by a specified
// delta or fraction since the parameter was first evaluated against the tracked
// vessel. Designed for supply chain sequencing -- proves that extraction,
// processing, or delivery actually occurred rather than testing a point-in-time
// absolute quantity.
//
// Unlike HasResource (which checks current amount against a fixed threshold),
// ResourceTransfer snapshots the resource amount on first vessel evaluation and
// measures the *change* on each subsequent update. This correctly validates
// operations regardless of initial tank state or vessel capacity.
//
// CC discovers ResourceTransferFactory by reflection at startup.
// The class name minus "Factory" maps to: type = ResourceTransfer in .cfg files.
//
// CONFIG USAGE:
//
//   // Prove ore was extracted -- quantity increased by 300+
//   PARAMETER
//   {
//       name     = OreExtracted
//       type     = ResourceTransfer
//       resource = Ore
//       deltaMin = 300.0
//       title    = Extract 300+ units of ore from surface
//   }
//
//   // Prove tanks were emptied before landing -- capacity-agnostic
//   PARAMETER
//   {
//       name        = TanksEmpty
//       type        = ResourceTransfer
//       resource    = Ore
//       fractionMax = 0.05
//       title       = Land with near-empty ore tanks
//   }
//
//   // Prove fuel was produced -- LiquidFuel increased by 400+
//   PARAMETER
//   {
//       name     = FuelProduced
//       type     = ResourceTransfer
//       resource = LiquidFuel
//       deltaMin = 400.0
//       title    = Produce 400+ units of Liquid Fuel
//   }
//
// FIELDS:
//   resource     -- Resource definition name (e.g. "Ore", "LiquidFuel", "Oxidizer")
//   deltaMin     -- Minimum increase in resource amount since first vessel evaluation.
//                   Optional. Use negative value to require depletion.
//   deltaMax     -- Maximum increase (use negative to require minimum depletion). Optional.
//   fractionMin  -- Minimum amount/capacity fraction (0.0-1.0). Optional.
//                   Evaluated against current state, not delta. Use for fill validation.
//   fractionMax  -- Maximum amount/capacity fraction. Optional.
//                   Use for empty validation (e.g. fractionMax = 0.05).
//
// EVALUATION:
//   All specified fields must pass simultaneously. Unspecified fields are ignored.
//   deltaMin/deltaMax are relative to the snapshot taken on first vessel evaluation.
//   fractionMin/fractionMax are absolute, evaluated against current state.
//
// REAL-TIME UPDATE ARCHITECTURE:
//   VesselParameter.CheckVessel() is the correct entry point for re-evaluation.
//   It calls VesselMeetsCondition(), updates per-vessel state, and notifies the
//   parent VesselParameterGroup via UpdateState() -- which is what drives the
//   contract window refresh.
//
//   The base class already subscribes to vessel change events and calls CheckVessel.
//   For continuous resource monitoring we add a 1-second coroutine that calls
//   CheckVessel on the group's TrackedVessel. This drives real-time UI updates
//   during mining and ISRU conversion without any manual state management.
//
//   GetParameterGroupHost() and CurrentVessel() are inherited from VesselParameter
//   and return the correct parent group and its TrackedVessel respectively.
//
// SNAPSHOT BEHAVIOR:
//   The baseline snapshot is taken on the first VesselMeetsCondition call against
//   the tracked vessel. Deferred from OnRegister so the snapshot reflects the
//   correct vessel's state, not whatever ActiveVessel was at KSC acceptance.
//   Persisted through save/load so mid-mission reloads preserve the baseline.

using System;
using System.Collections;
using ContractConfigurator;
using ContractConfigurator.Parameters;
using Contracts;
using UnityEngine;

namespace RedFrontier.ContractParameters
{
    public class ResourceTransfer : VesselParameter
    {
        // Config fields -- populated by factory, persisted via save/load
        protected string resource    = null;
        protected float  deltaMin    = float.NegativeInfinity;
        protected float  deltaMax    = float.PositiveInfinity;
        protected float  fractionMin = float.NegativeInfinity;
        protected float  fractionMax = float.PositiveInfinity;

        // Runtime state -- snapshot of resource amount on first vessel evaluation.
        // Persisted so save/load mid-mission does not reset the baseline.
        protected float snapshot      = float.NaN;
        protected bool  snapshotTaken = false;

        // Coroutine reference for clean shutdown in OnUnregister.
        // Owned by HighLogic.fetch (the host MonoBehaviour).
        private Coroutine rearmCoroutine = null;

        // -----------------------------------------------------------------------
        // Constructors
        // -----------------------------------------------------------------------

        public ResourceTransfer() : base() { }

        public ResourceTransfer(
            string resource,
            float  deltaMin,
            float  deltaMax,
            float  fractionMin,
            float  fractionMax,
            string title)
            : base(title)
        {
            this.resource    = resource;
            this.deltaMin    = deltaMin;
            this.deltaMax    = deltaMax;
            this.fractionMin = fractionMin;
            this.fractionMax = fractionMax;
        }

        // -----------------------------------------------------------------------
        // Persistence (save file round-trip)
        // -----------------------------------------------------------------------

        protected override void OnParameterSave(ConfigNode node)
        {
            base.OnParameterSave(node);
            if (resource != null) node.AddValue("resource", resource);

            if (!float.IsNegativeInfinity(deltaMin))    node.AddValue("deltaMin",    deltaMin);
            if (!float.IsPositiveInfinity(deltaMax))    node.AddValue("deltaMax",    deltaMax);
            if (!float.IsNegativeInfinity(fractionMin)) node.AddValue("fractionMin", fractionMin);
            if (!float.IsPositiveInfinity(fractionMax)) node.AddValue("fractionMax", fractionMax);

            node.AddValue("snapshotTaken", snapshotTaken);
            if (snapshotTaken) node.AddValue("snapshot", snapshot);
        }

        protected override void OnParameterLoad(ConfigNode node)
        {
            base.OnParameterLoad(node);
            resource      = ConfigNodeUtil.ParseValue<string>(node, "resource",      (string)null);
            deltaMin      = ConfigNodeUtil.ParseValue<float> (node, "deltaMin",      float.NegativeInfinity);
            deltaMax      = ConfigNodeUtil.ParseValue<float> (node, "deltaMax",      float.PositiveInfinity);
            fractionMin   = ConfigNodeUtil.ParseValue<float> (node, "fractionMin",   float.NegativeInfinity);
            fractionMax   = ConfigNodeUtil.ParseValue<float> (node, "fractionMax",   float.PositiveInfinity);
            snapshotTaken = ConfigNodeUtil.ParseValue<bool>  (node, "snapshotTaken", false);
            snapshot      = ConfigNodeUtil.ParseValue<float> (node, "snapshot",      float.NaN);
        }

        // -----------------------------------------------------------------------
        // Registration -- start polling coroutine
        //
        // The base class already subscribes to vessel change GameEvents in its
        // own OnRegister and calls CheckVessel accordingly. We only need to add
        // the continuous polling path for within-session resource changes.
        // -----------------------------------------------------------------------

        protected override void OnRegister()
        {
            base.OnRegister();
            rearmCoroutine = HighLogic.fetch.StartCoroutine(PollCoroutine());
        }

        protected override void OnUnregister()
        {
            base.OnUnregister();
            if (rearmCoroutine != null)
            {
                HighLogic.fetch.StopCoroutine(rearmCoroutine);
                rearmCoroutine = null;
            }
        }

        // -----------------------------------------------------------------------
        // Polling coroutine -- drives real-time resource monitoring
        //
        // Calls CheckVessel() on the group's TrackedVessel every second.
        // CheckVessel is the correct inherited entry point -- it calls
        // VesselMeetsCondition, updates per-vessel state, and notifies the
        // parent VesselParameterGroup via UpdateState(), which triggers the
        // contract window refresh.
        //
        // CurrentVessel() is inherited from VesselParameter and returns
        // GetParameterGroupHost().TrackedVessel -- the vessel the parent
        // VesselParameterGroup is currently tracking. This is the authoritative
        // vessel reference, not FlightGlobals.ActiveVessel.
        // -----------------------------------------------------------------------

        private IEnumerator PollCoroutine()
        {
            while (true)
            {
                yield return new WaitForSeconds(1f);

                Vessel v = CurrentVessel();
                if (v != null)
                {
                    CheckVessel(v);
                }
            }
        }

        // -----------------------------------------------------------------------
        // Title (auto-generated fallback when no 'title' field in .cfg)
        // -----------------------------------------------------------------------

        protected override string GetParameterTitle()
        {
            if (!string.IsNullOrEmpty(title)) return title;
            if (resource == null) return "Resource Transfer";

            if (!float.IsNegativeInfinity(deltaMin))
                return $"Transfer {deltaMin}+ units of {resource}";
            if (!float.IsPositiveInfinity(fractionMax) && fractionMax < 1f)
                return $"{resource} tanks <= {fractionMax * 100f:F0}% full";
            if (!float.IsNegativeInfinity(fractionMin))
                return $"{resource} tanks >= {fractionMin * 100f:F0}% full";

            return $"Resource Transfer: {resource}";
        }

        // -----------------------------------------------------------------------
        // Condition check -- pure predicate, no side effects except snapshot init
        //
        // Snapshot is taken on first call where vessel is valid. Deferred from
        // OnRegister to guarantee the snapshot is against the tracked vessel,
        // not ActiveVessel at KSC contract acceptance.
        //
        // snapshotTaken persists through save/load -- a reloaded snapshot is
        // never overwritten, preserving the original mission baseline.
        // -----------------------------------------------------------------------

        protected override bool VesselMeetsCondition(Vessel vessel)
        {
            if (vessel == null || resource == null) return false;

            if (!snapshotTaken)
            {
                snapshot      = VesselResourceManager.GetAmount(vessel, resource);
                snapshotTaken = true;
            }

            float current  = VesselResourceManager.GetAmount(vessel, resource);
            float delta    = current - snapshot;
            float fraction = VesselResourceManager.GetFraction(vessel, resource);

            if (!float.IsNegativeInfinity(deltaMin)    && delta    < deltaMin)    return false;
            if (!float.IsPositiveInfinity(deltaMax)    && delta    > deltaMax)    return false;
            if (!float.IsNegativeInfinity(fractionMin) && fraction < fractionMin) return false;
            if (!float.IsPositiveInfinity(fractionMax) && fraction > fractionMax) return false;

            return true;
        }
    }
}
