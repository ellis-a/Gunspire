#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Convenience menu for creating a play scene. The game bootstraps itself in any scene,
    /// so this only exists to give you a saved scene to put in the build settings.
    /// </summary>
    public static class SceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Tower.unity";

        [MenuItem("Gunspire/Create Play Scene")]
        public static void CreatePlayScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var boot = new GameObject("[Gunspire]");
            boot.AddComponent<GameBootstrap>();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();

            Debug.Log("Created " + ScenePath + ". Press Play.");
        }

        [MenuItem("Gunspire/Add Bootstrap To Current Scene")]
        public static void AddBootstrap()
        {
            if (Object.FindAnyObjectByType<GameBootstrap>() != null)
            {
                Debug.Log("This scene already has a bootstrap object.");
                return;
            }

            var boot = new GameObject("[Gunspire]");
            boot.AddComponent<GameBootstrap>();
            Undo.RegisterCreatedObjectUndo(boot, "Add Wizard bootstrap");
            EditorSceneManager.MarkSceneDirty(boot.scene);
        }
    }
}
#endif
