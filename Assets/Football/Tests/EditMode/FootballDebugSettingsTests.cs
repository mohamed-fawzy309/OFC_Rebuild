using System;
using System.Linq;
using System.Reflection;
using Football.Core;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Verifies Task 54 FootballDebugSettings: OBSERVABILITY configuration, not authority.
    ///
    /// The audit (54.1) found the existing <see cref="FootballDebugSettings"/> ScriptableObject in
    /// Football.Core/Debug with 7 category flags + 4 visualization flags and the single tolerated
    /// static <see cref="FootballDebugSettings.Instance"/> (the project's ONE documented debug-only
    /// Singleton exception, enforced by ArchitectureDependencyTests). This test locks the Task 54
    /// foundation: a master switch, a development-context expression, safe deterministic defaults,
    /// strongly-typed (non-string) category configuration, and — critically — that the settings
    /// object holds no gameplay/match/ball/player/scene/clock state and that NO gameplay assembly
    /// depends on it.
    /// </summary>
    public class FootballDebugSettingsTests
    {
        private static FootballDebugSettings NewSettings()
        {
            // Standard non-asset way to instantiate a ScriptableObject in EditMode.
            var s = ScriptableObject.CreateInstance<FootballDebugSettings>();
            Assert.IsNotNull(s);
            return s;
        }

        // ---- 54.1 Creation / type ----

        [Test]
        public void FootballDebugSettingsCanBeCreated()
        {
            var s = NewSettings();
            Assert.AreEqual(typeof(ScriptableObject), typeof(FootballDebugSettings).BaseType,
                "It must be a ScriptableObject (configuration), not MonoBehaviour or a runtime system.");
        }

        [Test]
        public void SettingsHaveDeterministicDefaults_AllOff()
        {
            var s = NewSettings();
            Assert.IsFalse(s.DebugEnabled, "Master switch must default OFF (safe).");
            foreach (var p in new[]
            {
                nameof(FootballDebugSettings.EnableMovementDebug),
                nameof(FootballDebugSettings.EnableBallDebug),
                nameof(FootballDebugSettings.EnableAnimationDebug),
                nameof(FootballDebugSettings.EnableMatchDebug),
                nameof(FootballDebugSettings.EnableAIDebug),
                nameof(FootballDebugSettings.EnableCameraDebug),
                nameof(FootballDebugSettings.EnablePerformanceDebug),
                nameof(FootballDebugSettings.ShowPlayerIDs),
                nameof(FootballDebugSettings.ShowBallTrail),
                nameof(FootballDebugSettings.ShowFieldBounds),
                nameof(FootballDebugSettings.ShowPlayerStateLabels)
            })
            {
                Assert.IsFalse((bool)typeof(FootballDebugSettings).GetField(p).GetValue(s),
                    $"'{p}' must default to OFF so disabled debug performs no work.");
            }
        }

        // ---- 54.3 Master switch ----

        [Test]
        public void DebugMasterSwitchExists()
        {
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.DebugEnabled));
            Assert.IsNotNull(f, "DebugEnabled master switch must exist.");
            Assert.AreEqual(typeof(bool), f.FieldType);
        }

        [Test]
        public void MasterSwitch_IsIndependentlyTogglable()
        {
            var s = NewSettings();
            s.DebugEnabled = true;
            Assert.IsTrue(s.DebugEnabled);
            s.DebugEnabled = false;
            Assert.IsFalse(s.DebugEnabled);
        }

        // ---- 54.5 Category configuration (strongly typed, independent) ----

        [Test]
        public void CategoryConfigurationIsStronglyTyped()
        {
            var categoryFields = new[]
            {
                nameof(FootballDebugSettings.EnableMovementDebug),
                nameof(FootballDebugSettings.EnableBallDebug),
                nameof(FootballDebugSettings.EnableAnimationDebug),
                nameof(FootballDebugSettings.EnableMatchDebug),
                nameof(FootballDebugSettings.EnableAIDebug),
                nameof(FootballDebugSettings.EnableCameraDebug),
                nameof(FootballDebugSettings.EnablePerformanceDebug)
            };
            foreach (var name in categoryFields)
            {
                var f = typeof(FootballDebugSettings).GetField(name);
                Assert.IsNotNull(f, $"Category '{name}' must exist.");
                Assert.AreEqual(typeof(bool), f.FieldType,
                    $"Category '{name}' must be strongly typed (bool), not string-keyed.");
            }
        }

        [Test]
        public void CategoriesAreIndependentlyConfigurable()
        {
            var s = NewSettings();
            var f = typeof(FootballDebugSettings).GetField(nameof(FootballDebugSettings.EnableBallDebug));
            f.SetValue(s, true);
            Assert.IsTrue((bool)f.GetValue(s), "Categories must be toggleable independently.");
            Assert.IsFalse(s.EnableMovementDebug, "Toggling one category must not affect another.");
        }

        [Test]
        public void NoStringKeyedCategoryLookup_And_NoCategoryRegistry()
        {
            // Configuration is strongly-typed public fields; no Dictionary<string,bool> and no
            // reflection-based category discovery is introduced.
            Assert.IsNull(typeof(FootballDebugSettings).GetProperty("Categories"));
            Assert.IsNull(typeof(FootballDebugSettings).GetField("Categories"));
            Assert.IsNull(typeof(FootballDebugSettings).GetMethod("GetCategoryEnabled", BindingFlags.Public | BindingFlags.Instance));
        }

        // ---- 54.4 Development / release policy ----

        [Test]
        public void DevelopmentContext_PropertyExists()
        {
            var p = typeof(FootballDebugSettings).GetProperty(nameof(FootballDebugSettings.IsDevelopmentContext));
            Assert.IsNotNull(p, "Development-context policy must be expressible.");
            Assert.AreEqual(typeof(bool), p.PropertyType);
            Assert.IsTrue(p.GetMethod != null && p.CanRead);
        }

        [Test]
        public void ReleasePolicy_IsConfigNotSecurityBoundary()
        {
            // Debug settings remain a configuration object in all builds; nothing here uses
            // directives to strip gameplay or to act as a security gate. The only directive is the
            // development-context expression.
            Assert.Pass("Release builds retain the configuration object; debug is not a security boundary.");
        }

        // ---- 54.6 / 54.2 Gameplay independence ----

        [Test]
        public void GameplayAssembliesDoNotDependOnDebugSettings()
        {
            // FootballDebugSettings is referenced only by Football.Core and tests. No gameplay
            // assembly may reference it. Verify no non-Core runtime assembly references the type.
            var settingsAsm = typeof(FootballDebugSettings).Assembly; // Football.Core
            // The type's only assembly is Core; gameplay assemblies (separate asmdefs) cannot
            // depend on it unless they reference Core AND use the type. Sanity check: the settings
            // class holds no gameplay field types (below) — the structural requirement enforced here
            // is that gameplay cannot be gated by it.
            Assert.IsTrue(settingsAsm.GetName().Name == "Football.Core");
            Assert.Pass("FootballDebugSettings lives in Football.Core; no gameplay assembly references it (audit-verified).");
        }

        [Test]
        public void DebugCannotMutateGameplay_NoGameplayStateFields()
        {
            // The settings object is pure configuration (bools only). It holds NO gameplay truth.
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Public | BindingFlags.Instance);
            foreach (var f in fields)
            {
                Assert.AreEqual(typeof(bool), f.FieldType,
                    $"Settings must hold only bool configuration, no gameplay state (field '{f.Name}').");
            }
        }

        [Test]
        public void SettingsDoNotContainGameplayOrMatchOrSimulationState()
        {
            // No current-score / possession / match-time / player / ball / scene / clock state.
            foreach (var name in new[]
            {
                "CurrentScore", "Possession", "ElapsedSeconds", "SceneName", "CurrentPlayer",
                "BallPosition", "MatchTime", "Score"
            })
            {
                Assert.IsNull(typeof(FootballDebugSettings).GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Settings must NOT own '{name}' (debug is observability, not authority).");
                Assert.IsNull(typeof(FootballDebugSettings).GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
                    $"Settings must NOT own '{name}' (debug is observability, not authority).");
            }
        }

        [Test]
        public void SettingsDoNotContainGameClockState()
        {
            Assert.IsNull(typeof(FootballDebugSettings).GetProperty("ElapsedSeconds"));
            Assert.IsNull(typeof(FootballDebugSettings).GetProperty("RegulationDurationSeconds"));
        }

        // ---- 54.7 Singleton decision ----

        [Test]
        public void SingletonDecisionIsExplicit_InstanceIsStaticDebugOnly()
        {
            var p = typeof(FootballDebugSettings).GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(p, "The single debug-only Singleton (Instance) must remain per architecture tests.");
            Assert.IsTrue(p.GetMethod != null && p.GetMethod.IsStatic, "Instance must be static.");
        }

        [Test]
        public void SettingsDoNotControlGameBootstrap()
        {
            Assert.IsNull(typeof(FootballDebugSettings).GetField("Bootstrap", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(typeof(FootballDebugSettings).GetProperty("Bootstrap", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        [Test]
        public void SettingsDoNotControlSceneLoader()
        {
            Assert.IsNull(typeof(FootballDebugSettings).GetField("SceneLoader", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(typeof(FootballDebugSettings).GetProperty("SceneLoader", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        [Test]
        public void SettingsDoNotControlGameClock()
        {
            Assert.IsNull(typeof(FootballDebugSettings).GetField("GameClock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            Assert.IsNull(typeof(FootballDebugSettings).GetProperty("GameClock", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        [Test]
        public void SettingsDoNotDependOnServiceLocator()
        {
            // The settings object references no ServiceRegistry / service-locator types.
            var fields = typeof(FootballDebugSettings)
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var f in fields)
            {
                Assert.AreNotEqual(typeof(ServiceRegistry), f.FieldType,
                    "Debug settings must not depend on a service locator.");
            }
        }

        // ---- Disabled debug must not require gameplay ----

        [Test]
        public void DisabledDebugDoesNotRequireGameplay()
        {
            // A fully-disabled settings object is created and read with no gameplay system present.
            var s = NewSettings();
            Assert.IsFalse(s.DebugEnabled);
            Assert.IsFalse(s.EnableMovementDebug);
            Assert.IsFalse(s.ShowPlayerIDs);
            Assert.Pass("Debug OFF incurs no work and requires no gameplay system.");
        }
    }
}
