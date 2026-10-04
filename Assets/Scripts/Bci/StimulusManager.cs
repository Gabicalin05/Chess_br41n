using System;
using System.Collections.Generic;

namespace BciChess.Bci
{
    /// <summary>
    /// The single place where stimuli are assigned to targets. Configured with the available ERP class ids;
    /// nothing else in the project hard-codes stimulus values.
    /// </summary>
    public sealed class StimulusManager
    {
        private readonly StimulusSlot[] _slots;

        /// <param name="classIds">Available ERP class ids, in assignment order. Duplicates are ignored.</param>
        /// <param name="maxSimultaneousTargets">Upper bound on targets shown at once (&lt;= 0 means use all slots).</param>
        public StimulusManager(IEnumerable<int> classIds, int maxSimultaneousTargets)
        {
            if (classIds == null)
                throw new ArgumentNullException(nameof(classIds));

            var distinct = new List<int>();
            foreach (int id in classIds)
            {
                if (!distinct.Contains(id))
                    distinct.Add(id);
            }

            int count = maxSimultaneousTargets > 0 ? Math.Min(maxSimultaneousTargets, distinct.Count) : distinct.Count;
            if (count < 1)
                throw new ArgumentException("At least one stimulus class id is required.", nameof(classIds));

            _slots = new StimulusSlot[count];
            for (int i = 0; i < count; i++)
                _slots[i] = new StimulusSlot(i, distinct[i]);
        }

        /// <summary>How many targets can be presented in one selection.</summary>
        public int Capacity => _slots.Length;

        public IReadOnlyList<StimulusSlot> Slots => _slots;

        /// <summary>
        /// Gives every candidate its own stimulus, deterministically in list order. Fails when there are
        /// more candidates than slots; the caller must then reduce or group the candidates.
        /// </summary>
        public bool TryAssign(IReadOnlyList<BciTarget> candidates, out IReadOnlyList<BciTarget> assigned)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            if (candidates.Count > _slots.Length)
            {
                assigned = Array.Empty<BciTarget>();
                return false;
            }

            var result = new BciTarget[candidates.Count];
            for (int i = 0; i < candidates.Count; i++)
                result[i] = candidates[i].WithStimulus(_slots[i]);
            assigned = result;
            return true;
        }
    }
}
