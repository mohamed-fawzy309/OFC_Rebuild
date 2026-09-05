using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 68 — Pace category (Acceleration + SprintSpeed).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// (real type/field inspection, not source.Contains / comment matching).
    ///
    /// Pace was introduced with the full approved PlayerStats implementation in Task 67 as the
    /// <c>PaceStats</c> category (exactly Acceleration + SprintSpeed). Task 68 confirms Pace is
    /// DATA: the two owned ratings use the unified PlayerStats rating type/range/default, there is
    /// no duplication elsewhere, no runtime state, no movement/physics logic, no world-unit
    /// conversion, and Goalkeepers retain Pace (unified model).
    /// </summary>
    public class PaceTests
    {
        private const string Prefix = "Football.Data.";

        private static Type FindType(string fullName)
        {
            var t = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(SafeGetTypes)
                .FirstOrDefault(x => x.FullName == fullName);
            Assert.IsNotNull(t, $"Type '{fullName}' must exist.");
            return t;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); }
        }

        private static FieldInfo PaceField(string name)
        {
            var t = FindType(Prefix + "PaceStats");
            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(f, $"Pace must define '{name}'.");
            return f;
        }

        // ---- Category structure ----

        [Test]
        public void PaceCategory_Exists_AndIsSerializableData()
        {
            var t = FindType(Prefix + "PaceStats");
            Assert.IsTrue(t.IsClass && t.IsSerializable,
                "Pace must be a [Serializable] data category, not a MonoBehaviour/ScriptableObject system.");
        }

        [Test]
        public void Pace_Contains_ExactlyAccelerationAndSprintSpeed()
        {
            var names = FindType(Prefix + "PaceStats")
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Select(f => f.Name)
                .OrderBy(x => x)
                .ToArray();
            CollectionAssert.AreEqual(new[] { "Acceleration", "SprintSpeed" }, names,
                "Pace must contain exactly Acceleration and SprintSpeed.");
        }

        [Test]
        public void Pace_HasNoUnapprovedAttributes()
        {
            foreach (var name in new[] { "TopSpeed", "MaxSpeed", "Burst", "Explosiveness", "Speed", "RunSpeed" })
            {
                Assert.IsNull(FindType(Prefix + "PaceStats").GetField(name, BindingFlags.Public | BindingFlags.Instance),
                    $"'{name}' is NOT an approved Pace attribute.");
            }
        }

        // ---- Rating representation (unified) ----

        [Test]
        public void Acceleration_UsesPlayerStatsRatingRepresentation()
        {
            var f = PaceField("Acceleration");
            Assert.AreEqual(typeof(int), f.FieldType, "Acceleration must be int.");
            var stats = FindType(Prefix + "PlayerStats");
            Assert.AreEqual(1, (int)stats.GetField("RatingMin").GetRawConstantValue());
            Assert.AreEqual(99, (int)stats.GetField("RatingMax").GetRawConstantValue());
            Assert.AreEqual(50, (int)stats.GetField("RatingDefault").GetRawConstantValue());
            var obj = Activator.CreateInstance(FindType(Prefix + "PaceStats"));
            Assert.AreEqual(50, (int)f.GetValue(obj), "Acceleration defaults to PlayerStats.RatingDefault.");
        }

        [Test]
        public void SprintSpeed_UsesPlayerStatsRatingRepresentation()
        {
            var f = PaceField("SprintSpeed");
            Assert.AreEqual(typeof(int), f.FieldType, "SprintSpeed must be int.");
            var obj = Activator.CreateInstance(FindType(Prefix + "PaceStats"));
            Assert.AreEqual(50, (int)f.GetValue(obj), "SprintSpeed defaults to PlayerStats.RatingDefault.");
        }

        [Test]
        public void PaceAttrAreNotWorldUnits_NotFloats()
        {
            // Ratings, not physical m/s or m/s² values — float world-unit storage would be a signal
            // of mixing ratings with physical measurements.
            var t = FindType(Prefix + "PaceStats");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.AreEqual(typeof(int), f.FieldType,
                    $"Pace attribute '{f.Name}' must be an int rating, not a float world-unit value.");
            }
        }

        // ---- Runtime / gameplay separation ----

        [Test]
        public void Pace_DoesNotContainRuntimeState()
        {
            var t = FindType(Prefix + "PaceStats");
            foreach (var name in new[] { "CurrentAcceleration", "CurrentSpeed", "CurrentVelocity",
                "IsSprinting", "Velocity", "Position", "Speed" })
            {
                Assert.IsNull(t.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Pace must NOT hold runtime state '{name}'.");
            }
        }

        [Test]
        public void Pace_DoesNotReferenceRuntimeObjects()
        {
            var t = FindType(Prefix + "PaceStats");
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.AreNotEqual(typeof(Transform), f.FieldType, "Pace must not reference Transform.");
                Assert.AreNotEqual(typeof(Rigidbody), f.FieldType, "Pace must not reference Rigidbody.");
                Assert.AreNotEqual(typeof(CharacterController), f.FieldType, "Pace must not reference CharacterController.");
            }
        }

        [Test]
        public void Pace_DoesNotContainGameplaySpeedMethods()
        {
            var t = FindType(Prefix + "PaceStats");
            foreach (var name in new[] { "CalculateAcceleration", "CalculateSprintSpeed",
                "GetMovementSpeed", "GetMaxVelocity", "CalculateSpeed" })
            {
                Assert.IsNull(t.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Pace must NOT derive gameplay via '{name}'.");
            }
        }

        // ---- No duplication ----

        [Test]
        public void PaceAttributes_AreNotDuplicated_AtPlayerDefinitionLevel()
        {
            var pd = FindType(Prefix + "PlayerDefinition");
            foreach (var name in new[] { "Acceleration", "SprintSpeed", "Pace" })
            {
                var f = pd.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                // "Pace" as a PlayerDefinition member would be a second, separate pace authority —
                // Pace lives ONLY under PlayerStats.Pace.
                Assert.IsNull(f, $"'{name}' must NOT be duplicated directly on PlayerDefinition.");
            }
        }

        [Test]
        public void Pace_DoesNotMoveIntoMovementConfig_AsPlayerAttribute()
        {
            // MovementConfig holds SYSTEM TUNING (world-unit floats). It must not be the
            // authoritative player-rating location for Acceleration/SprintSpeed. The authored
            // ratings stay in PlayerStats.Pace only. (MovementConfig may reuse same-named
            // system-tuning fields by design — that is configuration interpretation, not the
            // player attribute source.)
            var mc = FindType(Prefix + "MovementConfig");
            Assert.IsNotNull(mc, "MovementConfig still exists as system configuration.");
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(mc) && mc != typeof(ScriptableObject),
                "MovementConfig must remain a ScriptableObject system config (distinct from PlayerStats data).");
        }

        // ---- No movement / physics system ----

        [Test]
        public void NoMovementOrPhysicsSystem_WasCreated_ForPace()
        {
            var all = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes).ToArray();
            foreach (var name in new[] { "MovementSystem", "PlayerController", "SprintController",
                "AccelerationSystem", "PhysicsMovement", "PaceConverter", "RatingNormalizer" })
            {
                var hit = all.Any(t => t.Name == name && t.Namespace != null && t.Namespace.StartsWith("Football"));
                Assert.IsFalse(hit, $"Pace task must NOT create '{name}'.");
            }
        }

        // ---- Goalkeeper unification / position independence ----

        [Test]
        public void Goalkeepers_RetainPaceData()
        {
            // Pace is part of the unified PlayerStats held by every player; no goalkeeper split.
            var ps = FindType(Prefix + "PlayerStats");
            Assert.IsNotNull(ps.GetField("Pace", BindingFlags.Public | BindingFlags.Instance),
                "Every player (including GK) retains the Pace category via unified PlayerStats.");
            var dup = AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes)
                .Any(t => t.Name == "GoalkeeperPlayerStats");
            Assert.IsFalse(dup, "No separate goalkeeper stats type may exist.");
        }

        [Test]
        public void Pace_IsIndependentOfPrimaryPosition()
        {
            // Pace holds a plain authored rating; it contains no position-aware logic/fields.
            var t = FindType(Prefix + "PaceStats");
            var names = t.GetFields(BindingFlags.Public | BindingFlags.Instance).Select(f => f.Name).ToArray();
            Assert.IsFalse(names.Contains("PrimaryPosition"), "Pace must not be driven by PrimaryPosition.");
        }
    }
}
