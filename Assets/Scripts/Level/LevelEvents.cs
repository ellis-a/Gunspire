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

        /// <summary>Raised when the last enemy that holds a room shut is gone and the exit opens.</summary>
        public static event Action<RoomRuntime> FloorCleared;

        /// <summary>Raised when the player steps through a cleared room's exit, before any reward is offered.</summary>
        public static event Action<RoomRuntime> FloorCompleted;

        public static void RaiseFloorLeaving(int floor) => FloorLeaving?.Invoke(floor);

        public static void RaiseFloorEntered(RoomRuntime room) => FloorEntered?.Invoke(room);

        public static void RaiseFloorCleared(RoomRuntime room) => FloorCleared?.Invoke(room);

        public static void RaiseFloorCompleted(RoomRuntime room) => FloorCompleted?.Invoke(room);
    }
}
