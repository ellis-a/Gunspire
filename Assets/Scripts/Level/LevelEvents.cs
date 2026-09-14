using System;

namespace Gunspire
{
    /// <summary>
    /// The moments a floor begins and ends. Anything that remembers a floor subscribes rather than being
    /// told by the director one at a time: rewind history, reality shards, plague zombies, Blink's outer
    /// boundary, recent deaths.
    ///
    /// Every room is its own floor in this tower, so these fire once per room.
    /// </summary>
    public static class LevelEvents
    {
        /// <summary>Raised before the old room is torn down, with the floor being left.</summary>
        public static event Action<int> FloorLeaving;

        /// <summary>Raised once the new room is built, the player placed and everything summoned.</summary>
        public static event Action<RoomRuntime> FloorEntered;

        public static void RaiseFloorLeaving(int floor) => FloorLeaving?.Invoke(floor);

        public static void RaiseFloorEntered(RoomRuntime room) => FloorEntered?.Invoke(room);
    }
}
