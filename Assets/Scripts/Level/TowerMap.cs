using System.Collections.Generic;
using UnityEngine;

namespace WizardGun
{
    /// <summary>One selectable room on a floor of the tower.</summary>
    public class RoomNode
    {
        public RoomKind Kind;
        public int Floor;
        public int Seed;
        public string Title;
        public string Description;

        public Color Accent
        {
            get
            {
                switch (Kind)
                {
                    case RoomKind.Elite: return new Color(1f, 0.55f, 0.3f);
                    case RoomKind.Treasure: return new Color(1f, 0.85f, 0.4f);
                    case RoomKind.Shrine: return new Color(0.6f, 0.8f, 1f);
                    case RoomKind.Forge: return new Color(0.85f, 0.6f, 0.95f);
                    case RoomKind.Boss: return new Color(1f, 0.35f, 0.45f);
                    default: return new Color(0.75f, 0.75f, 0.8f);
                }
            }
        }
    }

    /// <summary>
    /// The route up the tower. Each floor offers a couple of rooms; picking one is the
    /// between-fights decision that shapes a run alongside the boons.
    /// </summary>
    public class TowerMap
    {
        public int FloorCount { get; private set; }

        private readonly List<List<RoomNode>> _floors = new List<List<RoomNode>>();

        public TowerMap(int floorCount, Rng rng)
        {
            FloorCount = floorCount;

            for (int floor = 1; floor <= floorCount; floor++)
                _floors.Add(BuildFloor(floor, floorCount, rng));
        }

        public IReadOnlyList<RoomNode> ChoicesForFloor(int floor)
        {
            int index = Mathf.Clamp(floor - 1, 0, _floors.Count - 1);
            return _floors[index];
        }

        private static List<RoomNode> BuildFloor(int floor, int floorCount, Rng rng)
        {
            var nodes = new List<RoomNode>();

            if (floor == 1)
            {
                nodes.Add(MakeNode(RoomKind.Combat, floor, rng));
                return nodes;
            }

            if (floor == floorCount)
            {
                nodes.Add(MakeNode(RoomKind.Boss, floor, rng));
                return nodes;
            }

            // Two or three doors, always distinct kinds so the choice means something.
            int options = rng.Chance(0.55f) ? 3 : 2;
            var used = new HashSet<RoomKind>();

            // A pure fight is always on the table so a run cannot stall for resources.
            nodes.Add(MakeNode(RoomKind.Combat, floor, rng));
            used.Add(RoomKind.Combat);

            while (nodes.Count < options)
            {
                RoomKind kind = RollKind(floor, floorCount, rng);
                if (!used.Add(kind)) continue;
                nodes.Add(MakeNode(kind, floor, rng));
            }

            rng.Shuffle(nodes);
            return nodes;
        }

        private static RoomKind RollKind(int floor, int floorCount, Rng rng)
        {
            float roll = rng.Value;

            // Elites become more common as the tower gets taller.
            float eliteChance = Mathf.Lerp(0.12f, 0.34f, (floor - 1f) / Mathf.Max(1f, floorCount - 1f));

            if (roll < eliteChance) return RoomKind.Elite;
            if (roll < eliteChance + 0.22f) return RoomKind.Treasure;
            if (roll < eliteChance + 0.44f) return RoomKind.Shrine;
            if (roll < eliteChance + 0.62f) return RoomKind.Forge;
            return RoomKind.Combat;
        }

        private static RoomNode MakeNode(RoomKind kind, int floor, Rng rng)
        {
            var node = new RoomNode
            {
                Kind = kind,
                Floor = floor,
                Seed = rng.Range(0, int.MaxValue)
            };

            switch (kind)
            {
                case RoomKind.Combat:
                    node.Title = "Barracks Hall";
                    node.Description = "A room full of the tower guard. Ordinary risk, ordinary pay.";
                    break;
                case RoomKind.Elite:
                    node.Title = "Warded Sanctum";
                    node.Description = "Fewer enemies, all of them empowered. Better boon on the far side.";
                    break;
                case RoomKind.Treasure:
                    node.Title = "Vault";
                    node.Description = "No guards. A weapon on a plinth and something worth breaking open.";
                    break;
                case RoomKind.Shrine:
                    node.Title = "Rune Shrine";
                    node.Description = "A spell waiting to be studied, and a stone that grants raw stats.";
                    break;
                case RoomKind.Forge:
                    node.Title = "Arms Forge";
                    node.Description = "Two guns, one choice, and a handful of guards who object.";
                    break;
                case RoomKind.Boss:
                    node.Title = "The Warden of the Spire";
                    node.Description = "The tower keeper. Everything it does is telegraphed, and all of it hurts.";
                    break;
            }
            return node;
        }
    }
}
