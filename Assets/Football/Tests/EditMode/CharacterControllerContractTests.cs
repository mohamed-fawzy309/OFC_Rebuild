using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 141 — CharacterController (Entity Foundation).
    ///
    /// Task 141 establishes and verifies the Player's CharacterController as the authoritative
    /// physical character-collision representation, WITHOUT implementing movement. This fixture
    /// locks the CHARACTERCONTROLLER ENTITY CONTRACT only:
    ///
    ///   - Exactly ONE authoritative CharacterController on the Player prefab, located on the
    ///     established "Collision" child (Task 138).
    ///   - Configuration is the existing authored/default configuration (preserved, not re-tuned).
    ///   - No Rigidbody and no additional Collider was introduced (no physics conflict).
    ///   - No runtime code holds a CharacterController reference (no movement consumer exists).
    ///   - No PlayerMovement / CharacterControllerDriver component was created prematurely.
    ///
    /// The full prefab structure/hierarchy is locked by Task 138 PlayerPrefabStructureTests; the
    /// absence of any runtime movement system is locked by Task 55 MovementDebugCategoryTests.
    /// This fixture adds ONLY the CharacterController-specific foundation contract.
    ///
    /// MOVEMENT / gravity / ground / slope / step / airborne / rotation behavior is deliberately
    /// NOT implemented and NOT tested here (future Tasks).
    /// </summary>
    public class CharacterControllerContractTests
    {
        private const string PrefabPath = "Assets/Football/Prefabs/Players/Player.prefab";

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Assert.IsNotNull(prefab, "Player.prefab must be loadable.");
            return prefab;
        }

        private static bool IsCharacterControllerType(Type t)
        {
            if (t == typeof(CharacterController)) return true;
            if (t.IsArray) return IsCharacterControllerType(t.GetElementType());
            if (t.IsGenericType) return t.GetGenericArguments().Any(IsCharacterControllerType);
            return false;
        }

        private static Type[] SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(x => x != null).ToArray(); }
        }

        // ---- 141.1 / 141.3 — Exactly one authoritative CharacterController ----

        [Test]
        [Category("CharacterController")]
        public void PlayerPrefab_ContainsExactlyOneCharacterController()
        {
            var prefab = LoadPrefab();
            var controllers = prefab.GetComponentsInChildren<CharacterController>(true);
            Assert.AreEqual(1, controllers.Length,
                $"Player prefab must contain exactly ONE authoritative CharacterController " +
                $"(found {controllers.Length}).");
        }

        [Test]
        [Category("CharacterController")]
        public void CharacterController_IsLocated_OnCollisionChild()
        {
            var prefab = LoadPrefab();
            var controllers = prefab.GetComponentsInChildren<CharacterController>(true);
            Assert.AreEqual(1, controllers.Length);

            var cc = controllers[0];
            Assert.AreEqual("Collision", cc.gameObject.name,
                "The authoritative CharacterController must live on the 'Collision' child (Task 138).");
            Assert.AreEqual(prefab.transform, cc.transform.parent,
                "The 'Collision' child must be a direct child of the Player root.");
        }

        // ---- 141.2 / 141.4 — Configuration (estalished values + preserved defaults) ----

        [Test]
        [Category("CharacterController")]
        public void CharacterController_EstablishedConfiguration_IsPreserved()
        {
            // Authoritative ENTITY configuration established by earlier architecture work
            // (FootballSetup.cs sets height/radius/center; Task 138 documented them).
            var cc = LoadPrefab().GetComponentsInChildren<CharacterController>(true)[0];

            Assert.AreEqual(1.8f, cc.height, 0.001f,
                "CharacterController height must be 1.8 (established architecture).");
            Assert.AreEqual(0.3f, cc.radius, 0.001f,
                "CharacterController radius must be 0.3 (established architecture).");
            Assert.AreEqual(0f, cc.center.x, 0.001f,
                "CharacterController center.x must be 0 (established architecture).");
            Assert.AreEqual(0.9f, cc.center.y, 0.001f,
                "CharacterController center.y must be 0.9 (established architecture).");
            Assert.AreEqual(0f, cc.center.z, 0.001f,
                "CharacterController center.z must be 0 (established architecture).");
        }

        [Test]
        [Category("CharacterController")]
        public void CharacterController_PreviouslyDefaultedValues_ArePreserved_NotRetuned()
        {
            // These serialized values are Unity defaults at creation (FootballSetup.cs only set
            // height/radius/center). They have NO documented product meaning at this stage and are
            // PRESERVED, not re-tuned. The lock documents current state; product tuning is DEFERRED.
            var cc = LoadPrefab().GetComponentsInChildren<CharacterController>(true)[0];

            Assert.AreEqual(45f, cc.slopeLimit, 0.001f,
                "slopeLimit must remain 45 (Unity default at creation — preserved, not tuned).");
            Assert.AreEqual(0.3f, cc.stepOffset, 0.001f,
                "stepOffset must remain 0.3 (Unity default at creation — preserved, not tuned).");
            Assert.AreEqual(0.08f, cc.skinWidth, 0.0001f,
                "skinWidth must remain 0.08 (Unity default at creation — preserved, not tuned).");
            Assert.AreEqual(0.001f, cc.minMoveDistance, 0.00001f,
                "minMoveDistance must remain 0.001 (Unity default at creation — preserved, not tuned).");

            // detectCollisions / enableOverlapRecovery are not serialized in this Unity version;
            // they keep their runtime property defaults. Assert they are still enabled (component
            // usable), which is the neutral preservation outcome.
            Assert.IsTrue(cc.detectCollisions, "detectCollisions must remain enabled (preserved default).");
            Assert.IsTrue(cc.enableOverlapRecovery, "enableOverlapRecovery must remain enabled (preserved default).");
        }

        // ---- 141.3 / 141.6 — No Rigidbody / no extra collider (no physics conflict) ----

        [Test]
        [Category("CharacterController")]
        public void PlayerPrefab_HasNoRigidbody()
        {
            var prefab = LoadPrefab();
            var rigidbodies = prefab.GetComponentsInChildren<Rigidbody>(true);
            Assert.IsEmpty(rigidbodies,
                "Player prefab must contain no Rigidbody: the CharacterController is the authoritative " +
                "character collision primitive; a Rigidbody would conflict.");
        }

        [Test]
        [Category("CharacterController")]
        public void PlayerPrefab_HasNoCollider_OtherThanTheCharacterController()
        {
            var prefab = LoadPrefab();
            var colliders = prefab.GetComponentsInChildren<Collider>(true);
            Assert.AreEqual(1, colliders.Length,
                $"Player prefab must contain exactly one Collider-derived component (the CharacterController); " +
                $"found {colliders.Length}.");
            Assert.IsInstanceOf<CharacterController>(colliders[0],
                "The sole Collider-derived component must be the authoritative CharacterController.");
        }

        // ---- 141.3 / 141.5 — No runtime CharacterController consumer, no premature component ----

        [Test]
        [Category("CharacterController")]
        public void NoRuntimeCode_HoldsA_CharacterControllerReference()
        {
            // No movement runtime exists. Runtime assemblies that would host a movement consumer
            // (Players / Core / Input) must contain no CharacterController-typed field, property,
            // return type, or parameter — i.e. nothing binds to the CharacterController yet.
            var assemblies = new[]
            {
                typeof(Football.Players.PlayerEntity).Assembly,
                typeof(Football.Core.FootballDebugSettings).Assembly,
                typeof(Football.Core.IPlayerInput).Assembly
            };
            bool found = false;
            string where = null;

            foreach (var asm in assemblies)
            {
                foreach (var t in SafeGetTypes(asm))
                {
                    foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic |
                                                  BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (IsCharacterControllerType(f.FieldType)) { found = true; where = $"{t.FullName}.{f.Name}"; }
                    }
                    foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
                                                      BindingFlags.Instance | BindingFlags.Static))
                    {
                        if (IsCharacterControllerType(p.PropertyType)) { found = true; where = $"{t.FullName}.{p.Name}"; }
                    }
                    foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic |
                                                   BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                    {
                        if (IsCharacterControllerType(m.ReturnType)) { found = true; where = $"{t.FullName}.{m.Name}"; }
                        foreach (var prm in m.GetParameters())
                        {
                            if (IsCharacterControllerType(prm.ParameterType)) { found = true; where = $"{t.FullName}.{m.Name}"; }
                        }
                    }
                    if (found) break;
                }
                if (found) break;
            }

            Assert.IsFalse(found,
                "No runtime code may reference the CharacterController yet (no movement consumer). " +
                (where != null ? $"Found: {where}" : ""));
        }

        [Test]
        [Category("CharacterController")]
        public void NoPlayerMovementComponent_OrDriver_Exists()
        {
            // No premature movement component/driver may exist (movement behavior belongs to later
            // Tasks). Scan the same runtime assemblies for forbidden component/driver type names.
            var forbidden = new[] { "PlayerMovement", "CharacterControllerDriver", "MovementController",
                "PlayerMotion", "CharacterMovement" };
            var assemblies = new[]
            {
                typeof(Football.Players.PlayerEntity).Assembly,
                typeof(Football.Core.FootballDebugSettings).Assembly,
                typeof(Football.Core.IPlayerInput).Assembly
            };
            foreach (var asm in assemblies)
            {
                foreach (var t in SafeGetTypes(asm))
                {
                    Assert.IsFalse(forbidden.Contains(t.Name),
                        $"No '{t.Name}' movement component/driver may be created prematurely (Task 141 " +
                        "is CharacterController entity foundation only).");
                }
            }
        }
    }
}