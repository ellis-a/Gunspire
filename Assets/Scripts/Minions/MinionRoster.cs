using System.Collections.Generic;

namespace Gunspire
{
    /// <summary>
    /// The minions that follow the player from floor to floor, kept as how many of each survived the
    /// floor. The live ones are rebuilt from this on arrival; anything not persistent, such as plague
    /// zombies, stays behind with the floor it was raised on. A monstrosity slot or a beast tier is one
    /// more id in the same count; their limits belong to the spells that summon them.
    /// </summary>
    public class MinionRoster
    {
        public sealed class Kept
        {
            public string Id;
            public int Count;
        }

        private readonly List<Kept> _kept = new List<Kept>();

        public IReadOnlyList<Kept> Entries => _kept;

        public int CountOf(string id)
        {
            for (int i = 0; i < _kept.Count; i++)
                if (_kept[i].Id == id) return _kept[i].Count;
            return 0;
        }

        /// <summary>
        /// Replaces the record with what is out now. A knocked-down minion still counts, since it would
        /// have got back up; one that is simply dead does not.
        /// </summary>
        public void Remember(IReadOnlyList<MinionController> live)
        {
            _kept.Clear();
            if (live == null) return;

            for (int i = 0; i < live.Count; i++)
            {
                MinionController minion = live[i];
                if (minion == null || minion.Definition == null || !minion.Definition.Persistent) continue;
                if (minion.Health != null && !minion.Health.IsAlive && !minion.IsDown) continue;

                Kept entry = null;
                for (int k = 0; k < _kept.Count; k++)
                    if (_kept[k].Id == minion.Definition.Id) entry = _kept[k];

                if (entry == null)
                {
                    entry = new Kept { Id = minion.Definition.Id };
                    _kept.Add(entry);
                }

                entry.Count++;
            }
        }

        public void Clear() => _kept.Clear();
    }
}
