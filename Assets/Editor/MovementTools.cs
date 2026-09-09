#if UNITY_EDITOR
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Spider Legs cannot be exercised outside a real Play session - it needs the
    /// CharacterController's actual physics. What can be checked here is the one piece
    /// everything else depends on: the quaternion trick that tips the player's whole
    /// transform onto a wall. If that math is wrong, no amount of playtesting the feel would
    /// point at it - it would just look like the player facing a strange direction on some
    /// walls and not others.
    /// </summary>
    public static class MovementTools
    {
        private const float Epsilon = 0.001f;

        [MenuItem("Gunspire/Verify Wall Zip Math")]
        public static void VerifyWallZipMath()
        {
            var problems = new System.Collections.Generic.List<string>();

            Vector3[] normals =
            {
                Vector3.up, Vector3.down, Vector3.forward, Vector3.back, Vector3.left, Vector3.right,
                new Vector3(1f, 1f, 0f).normalized, new Vector3(1f, 1f, 1f).normalized,
                new Vector3(-1f, 0.4f, 0.7f).normalized
            };

            float[] startYaws = { 0f, 37f, 90f, 145f, 180f, -60f };

            foreach (float yaw in startYaws)
            {
                Quaternion start = Quaternion.Euler(0f, yaw, 0f);

                foreach (Vector3 normal in normals)
                {
                    // Attach: the same expression AttachToWall applies to transform.rotation.
                    Quaternion attached = Quaternion.FromToRotation(start * Vector3.up, normal) * start;
                    CheckUp(problems, "attach yaw=" + yaw + " normal=" + normal, attached, normal);

                    // Detach: the same expression EndWallZip applies, run from the attached
                    // orientation. Should land back on world up regardless of which wall it
                    // came from or which way the player was facing before that.
                    Quaternion detached = Quaternion.FromToRotation(attached * Vector3.up, Vector3.up) * attached;
                    CheckUp(problems, "detach yaw=" + yaw + " normal=" + normal, detached, Vector3.up);

                    // Chained: attach to a second wall directly from the first, as a re-zip
                    // while already stuck would do. Nothing in the shipped feature triggers
                    // this yet, but BeginWallZip does not forbid it, so the composition needs
                    // to hold up before it is ever exposed.
                    Vector3 second = normal == Vector3.up ? Vector3.right : Vector3.up;
                    Quaternion rechained = Quaternion.FromToRotation(attached * Vector3.up, second) * attached;
                    CheckUp(problems, "rechain yaw=" + yaw + " normal=" + normal, rechained, second);

                    // No NaNs or zero quaternions, which is the real risk of the antiparallel
                    // case (a ceiling exactly opposite the current up) - FromToRotation has to
                    // pick an arbitrary perpendicular axis there, and picking one is fine, but
                    // failing to pick one at all is not.
                    if (IsDegenerate(attached))
                        problems.Add("attach yaw=" + yaw + " normal=" + normal + " produced a degenerate rotation");
                }
            }

            if (problems.Count == 0)
            {
                Debug.Log("Wall zip math: " + startYaws.Length + " starting yaws x " + normals.Length
                          + " wall normals, attach/detach/rechain all round-trip correctly.\n  no problems.");
                return;
            }

            var report = new StringBuilder("Wall zip math: " + problems.Count + " PROBLEMS:\n");
            for (int i = 0; i < problems.Count && i < 25; i++) report.AppendLine("    " + problems[i]);
            Debug.LogError(report.ToString());
        }

        private static void CheckUp(System.Collections.Generic.List<string> problems, string label,
            Quaternion rotation, Vector3 expectedUp)
        {
            Vector3 actualUp = rotation * Vector3.up;
            float error = Vector3.Angle(actualUp, expectedUp);
            if (error > 0.5f)
                problems.Add(label + ": up is " + error.ToString("0.00") + " degrees off target");
        }

        private static bool IsDegenerate(Quaternion q)
        {
            float sum = Mathf.Abs(q.x) + Mathf.Abs(q.y) + Mathf.Abs(q.z) + Mathf.Abs(q.w);
            return float.IsNaN(sum) || sum < Epsilon;
        }
    }
}
#endif
