#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;

namespace Gunspire.EditorTools
{
    /// <summary>
    /// Attacks gained a reach, melee or ranged, which silence and disarm read. Enemy assets written
    /// before it store nothing and would all read as ranged, which leaves the hound's lunge immune to
    /// disarm. Copies each attack's reach from the built-in with the same enemy id and attack name.
    /// Only assets with no reach stored at all are touched, and saving one writes a reach for every
    /// attack, so a second run finds nothing to do.
    /// </summary>
    public class EnemyAttackReach : ObjectMigration
    {
        private static readonly Regex HasReach = new Regex(@"^[ \t]+Reach:", RegexOptions.Multiline);

        public override string Name => "Enemy attack reach written from the code roster";

        public override void Migrate(bool apply, List<string> changes)
        {
            var builtIn = new Dictionary<string, EnemyDefinition>();
            foreach (EnemyDefinition def in EnemyLibrary.BuiltIn()) builtIn[def.Id] = def;

            foreach (EnemyAsset asset in AssetsOf<EnemyAsset>.All())
            {
                EnemyDefinition enemy = asset.Enemy;
                if (enemy == null) continue;

                string path = AssetDatabase.GetAssetPath(asset);
                if (HasReach.IsMatch(File.ReadAllText(path))) continue;

                builtIn.TryGetValue(enemy.Id, out EnemyDefinition source);

                int melee = 0;
                foreach (AttackDefinition attack in enemy.Attacks)
                {
                    if (attack == null || ReachFor(source, attack.Name) != AttackReach.Melee) continue;

                    changes.Add(enemy.Id + ": \"" + attack.Name + "\" is melee");
                    if (apply) attack.Reach = AttackReach.Melee;
                    melee++;
                }

                if (melee == 0) changes.Add(enemy.Id + ": every attack is ranged");
                if (apply) EditorUtility.SetDirty(asset);
            }
        }

        private static AttackReach ReachFor(EnemyDefinition source, string attackName)
        {
            if (source == null) return AttackReach.Ranged;

            foreach (AttackDefinition attack in source.Attacks)
                if (attack != null && attack.Name == attackName) return attack.Reach;

            return AttackReach.Ranged;
        }
    }
}
#endif
