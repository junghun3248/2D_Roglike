using System;
using Roglike.Data;
using Roglike.Player;
using UnityEditor;
using UnityEngine;

namespace Roglike.EditorTools
{
    public static class MovesetLibraryBuilder
    {
        private const string Folder = "Assets/_Game/Data/Player";

        public static void Configure(PlayerCombat combat)
        {
            var serialized = new SerializedObject(combat);
            serialized.FindProperty("_slash").objectReferenceValue = EnsureNormalSlash("Attack1", false);
            serialized.FindProperty("_airSlash").objectReferenceValue = EnsureNormalSlash("Air1", true);
            serialized.FindProperty("_heavy").objectReferenceValue = Ensure("Heavy1", 1.8f, a => Heavy(a, false));
            serialized.FindProperty("_dashAttack").objectReferenceValue = Ensure("DashAttack", 1.6f, a =>
            {
                a.startup = 0.025f; a.active = 0.18f; a.recovery = 0.08f;
                a.lungeSpeed = 21f; a.hitboxSize = new Vector2(2.4f, 1.4f);
                a.knockback = new Vector2(8f, 2f); a.hitStop = 0.055f; a.shake = 0.25f;
                Motion(a, Vector2.zero, true, true);
            });
            serialized.FindProperty("_launcher").objectReferenceValue = Ensure("Launcher", 1.4f, a =>
            {
                a.startup = 0.035f; a.active = 0.1f; a.recovery = 0.12f;
                a.knockback = new Vector2(1.5f, 14f); a.hitStun = 0.65f;
                a.hitboxOffset = new Vector2(0.9f, 0.25f); a.hitboxSize = new Vector2(2f, 2.5f);
                a.slashAngle = 75f; a.hitStop = 0.07f; a.shake = 0.3f;
                Motion(a, new Vector2(0f, 8f));
            });
            serialized.FindProperty("_plunge").objectReferenceValue = Ensure("Plunge", 2f, a =>
            {
                a.startup = 0f; a.active = 0.12f; a.recovery = 0.22f;
                a.hitboxOffset = new Vector2(0f, -0.65f); a.hitboxSize = new Vector2(1.2f, 1.5f);
                a.knockback = new Vector2(3f, 11f); a.hitStun = 0.45f;
                a.hitStop = 0.1f; a.shake = 0.5f; a.slashAngle = -90f;
                Motion(a, new Vector2(0f, -32f), false, true);
            });
            serialized.FindProperty("_chargedStrike").objectReferenceValue = Ensure("ChargedStrike", 4.5f, a =>
            {
                Heavy(a, true);
                a.startup = 0.1f; a.active = 0.15f; a.recovery = 0.3f;
                a.hitboxOffset = new Vector2(1.7f, 0.1f); a.hitboxSize = new Vector2(4f, 2.4f);
                a.knockback = new Vector2(15f, 8f); a.hitStop = 0.16f; a.shake = 0.75f;
                a.overdriveGain = 14f;
            });
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Heavy(AttackData attack, bool finisher)
        {
            attack.startup = 0.15f; attack.active = 0.09f; attack.recovery = 0.12f; attack.cancelTime = 0.24f;
            attack.lungeSpeed = 3f; attack.knockback = new Vector2(finisher ? 10f : 4f, 3f);
            attack.hitboxSize = new Vector2(2.8f, 1.8f); attack.hitboxOffset = new Vector2(1.2f, 0.1f);
            attack.hitStun = 0.4f; attack.hitStop = finisher ? 0.11f : 0.09f;
            attack.shake = finisher ? 0.5f : 0.35f; attack.isFinisher = finisher;
        }

        internal static AttackData EnsureNormalSlash(string name, bool airborne)
        {
            string path = $"{Folder}/{name}.asset";
            var attack = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            bool created = attack == null;
            if (created) attack = ScriptableObject.CreateInstance<AttackData>();
            attack.damageMultiplier = 1f;
            attack.startup = 0.04f; attack.active = 0.08f; attack.recovery = 0.15f;
            attack.cancelTime = 0.27f;
            attack.lungeSpeed = airborne ? 0f : 5f;
            attack.knockback = new Vector2(6f, 2f); attack.hitStun = 0.3f;
            attack.hitStop = airborne ? 0.06f : 0.075f;
            attack.shake = airborne ? 0.18f : 0.22f;
            attack.overdriveGain = 5f; attack.slashAngle = -15f;
            attack.isFinisher = false;
            attack.hitboxOffset = new Vector2(1f, 0.1f);
            attack.hitboxSize = new Vector2(2f, airborne ? 1.8f : 1.3f);
            Motion(attack, Vector2.zero);
            if (created) AssetDatabase.CreateAsset(attack, path);
            EditorUtility.SetDirty(attack);
            AssetDatabase.SaveAssets();
            return attack;
        }

        private static void Motion(AttackData attack, Vector2 velocity, bool keepMomentum = false, bool movingHits = false)
        {
            var serialized = new SerializedObject(attack);
            serialized.FindProperty("_selfVelocity").vector2Value = velocity;
            serialized.FindProperty("_keepMomentum").boolValue = keepMomentum;
            serialized.FindProperty("_hitsWhileMoving").boolValue = movingHits;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static AttackData Ensure(string name, float damage, Action<AttackData> configure)
        {
            string path = $"{Folder}/{name}.asset";
            AttackData existing = AssetDatabase.LoadAssetAtPath<AttackData>(path);
            if (existing != null) return existing;
            var attack = ScriptableObject.CreateInstance<AttackData>();
            attack.damageMultiplier = damage;
            configure(attack);
            AssetDatabase.CreateAsset(attack, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<AttackData>(path);
        }
    }
}
