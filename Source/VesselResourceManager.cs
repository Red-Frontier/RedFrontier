// VesselResourceManager.cs
// Red Frontier - Vessel Resource Utility
//
// Read-only MVP for Chapter 1. Provides resource queries against both loaded
// vessels (live Part.Resources) and unloaded vessels (ProtoPartSnapshot.resources),
// branching transparently so callers never need to care about vessel load state.
//
// CHAPTER 1 SCOPE: GetAmount, GetCapacity, GetFraction — read path only.
// CHAPTER 2 SCOPE: SetAmount, Transfer — write path for logistics automation.
//                  Stubs are present but throw NotImplementedException to prevent
//                  accidental use before the logistics system is validated.
//
// USAGE (from ResourceTransfer parameter or any other consumer):
//
//   float ore     = VesselResourceManager.GetAmount(vessel, "Ore");
//   float cap     = VesselResourceManager.GetCapacity(vessel, "Ore");
//   float fraction = VesselResourceManager.GetFraction(vessel, "Ore");
//
// Thread safety: KSP is single-threaded on the Unity main thread.
// All methods are safe to call from VesselParameter.VesselMeetsCondition and
// ContractParameter.OnUpdate without additional locking.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RedFrontier
{
    public static class VesselResourceManager
    {
        // -----------------------------------------------------------------------
        // Public API — Chapter 1 (read path)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the current amount of <paramref name="resource"/> across all
        /// parts of <paramref name="vessel"/>. Works whether the vessel is loaded
        /// or on rails (unloaded).
        /// Returns 0 if the vessel is null or carries no matching resource.
        /// </summary>
        public static float GetAmount(Vessel vessel, string resource)
        {
            if (vessel == null || string.IsNullOrEmpty(resource)) return 0f;

            return vessel.loaded
                ? GetAmountLoaded(vessel, resource)
                : GetAmountProto(vessel.protoVessel, resource);
        }

        /// <summary>
        /// Returns the total storage capacity for <paramref name="resource"/>
        /// across all parts of <paramref name="vessel"/>.
        /// Returns 0 if the vessel has no tanks for that resource.
        /// </summary>
        public static float GetCapacity(Vessel vessel, string resource)
        {
            if (vessel == null || string.IsNullOrEmpty(resource)) return 0f;

            return vessel.loaded
                ? GetCapacityLoaded(vessel, resource)
                : GetCapacityProto(vessel.protoVessel, resource);
        }

        /// <summary>
        /// Returns amount / capacity as a 0–1 fraction.
        /// Returns 0 if capacity is zero (avoids divide-by-zero).
        /// Useful for empty/full validation without hardcoded quantities:
        ///   GetFraction(vessel, "Ore") &lt;= 0.05f  →  tanks are essentially empty
        ///   GetFraction(vessel, "Ore") &gt;= 0.90f  →  tanks are essentially full
        /// </summary>
        public static float GetFraction(Vessel vessel, string resource)
        {
            float capacity = GetCapacity(vessel, resource);
            if (capacity <= 0f) return 0f;
            return Mathf.Clamp01(GetAmount(vessel, resource) / capacity);
        }

        // -----------------------------------------------------------------------
        // Public API — Chapter 2 stubs (write path, not yet active)
        // -----------------------------------------------------------------------

        /// <summary>
        /// [CHAPTER 2] Sets the amount of <paramref name="resource"/> on
        /// <paramref name="vessel"/> to <paramref name="amount"/>, clamped to
        /// [0, capacity]. Handles loaded and unloaded vessels.
        /// NOT IMPLEMENTED — logistics automation system not yet validated.
        /// </summary>
        public static void SetAmount(Vessel vessel, string resource, float amount)
        {
            // Chapter 2: implement loaded and proto write paths.
            // Loaded path:  iterate Part.Resources, set PartResource.amount,
            //               call vessel.resourcePartSet.RebuildInPlace() after.
            // Proto path:   iterate ProtoPartSnapshot.resources,
            //               set ProtoPartResourceSnapshot.amount directly.
            //               KSP reads proto state on scene load — no reload needed
            //               for off-scene vessels.
            throw new NotImplementedException(
                "VesselResourceManager.SetAmount is a Chapter 2 feature. " +
                "The logistics automation system must be validated before enabling writes.");
        }

        /// <summary>
        /// [CHAPTER 2] Transfers <paramref name="amount"/> of
        /// <paramref name="resource"/> from <paramref name="from"/> to
        /// <paramref name="to"/>, clamped to available amount and remaining
        /// capacity on the destination.
        /// NOT IMPLEMENTED — logistics automation system not yet validated.
        /// </summary>
        public static void Transfer(Vessel from, Vessel to, string resource, float amount)
        {
            // Chapter 2: call SetAmount on both vessels after resolving
            // actual transferable quantity:
            //   float available  = GetAmount(from, resource);
            //   float space      = GetCapacity(to, resource) - GetAmount(to, resource);
            //   float actual     = Mathf.Min(amount, available, space);
            //   SetAmount(from, resource, GetAmount(from, resource) - actual);
            //   SetAmount(to,   resource, GetAmount(to,   resource) + actual);
            throw new NotImplementedException(
                "VesselResourceManager.Transfer is a Chapter 2 feature. " +
                "The logistics automation system must be validated before enabling writes.");
        }

        // -----------------------------------------------------------------------
        // Loaded vessel — reads live Part.Resources
        // -----------------------------------------------------------------------

        private static float GetAmountLoaded(Vessel vessel, string resource)
        {
            float total = 0f;
            foreach (Part part in vessel.parts)
            {
                if (part == null) continue;
                foreach (PartResource pr in part.Resources)
                {
                    if (pr != null &&
                        string.Equals(pr.resourceName, resource, StringComparison.OrdinalIgnoreCase))
                    {
                        total += (float)pr.amount;
                    }
                }
            }
            return total;
        }

        private static float GetCapacityLoaded(Vessel vessel, string resource)
        {
            float total = 0f;
            foreach (Part part in vessel.parts)
            {
                if (part == null) continue;
                foreach (PartResource pr in part.Resources)
                {
                    if (pr != null &&
                        string.Equals(pr.resourceName, resource, StringComparison.OrdinalIgnoreCase))
                    {
                        total += (float)pr.maxAmount;
                    }
                }
            }
            return total;
        }

        // -----------------------------------------------------------------------
        // Unloaded vessel — reads ProtoPartSnapshot.resources
        //
        // KSP serializes resource state into ProtoPartResourceSnapshot when a
        // vessel goes off-rails. These snapshots are the authoritative source
        // for unloaded vessel resource values — not PartResource, which only
        // exists on loaded parts.
        //
        // ProtoPartResourceSnapshot fields used:
        //   resourceName  — string, matches resource definition name
        //   amount        — double, current quantity
        //   maxAmount     — double, storage capacity
        // -----------------------------------------------------------------------

        private static float GetAmountProto(ProtoVessel proto, string resource)
        {
            if (proto == null) return 0f;

            float total = 0f;
            foreach (ProtoPartSnapshot pps in proto.protoPartSnapshots)
            {
                if (pps?.resources == null) continue;
                foreach (ProtoPartResourceSnapshot pprs in pps.resources)
                {
                    if (pprs != null &&
                        string.Equals(pprs.resourceName, resource, StringComparison.OrdinalIgnoreCase))
                    {
                        total += (float)pprs.amount;
                    }
                }
            }
            return total;
        }

        private static float GetCapacityProto(ProtoVessel proto, string resource)
        {
            if (proto == null) return 0f;

            float total = 0f;
            foreach (ProtoPartSnapshot pps in proto.protoPartSnapshots)
            {
                if (pps?.resources == null) continue;
                foreach (ProtoPartResourceSnapshot pprs in pps.resources)
                {
                    if (pprs != null &&
                        string.Equals(pprs.resourceName, resource, StringComparison.OrdinalIgnoreCase))
                    {
                        total += (float)pprs.maxAmount;
                    }
                }
            }
            return total;
        }
    }
}
