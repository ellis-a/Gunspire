using UnityEngine;

namespace Gunspire
{
    /// <summary>Something the player can walk up to and press the interact key on.</summary>
    public interface IInteractable
    {
        /// <summary>Text shown under the crosshair when looked at, e.g. "Take Frost Lance".</summary>
        string Prompt { get; }
        bool CanInteract(GameObject interactor);
        void Interact(GameObject interactor);
    }
}
