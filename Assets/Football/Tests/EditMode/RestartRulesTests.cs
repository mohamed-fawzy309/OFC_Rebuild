using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    /// <summary>
    /// Task 106 - Restart Rules (data architecture only; the FINAL Match Data task).
    ///
    /// Runtime/Data compiles into the predefined Assembly-CSharp (no asmdef) which this test
    /// assembly cannot reference at compile time, so the actual types are inspected by reflection
    /// with real instances (ScriptableObject.CreateInstance).
    ///
    /// Task 106 locks:
    ///   - A strongly typed Match RESTART representation exists: <c>MatchRestartType</c> enum with
    ///     the APPROVED 7-value set KickOff, ThrowIn, GoalKick, CornerKick, FreeKick, PenaltyKick,
    ///     DropBall (the restart set anticipated across Tasks 102/103/104 boundary tests).
    ///   - RestartType is SEPARATE from GameStateId (Football.Core): GameStateId.KickOff is a
    ///     MATCH/GAME STATE in the phase flow, neither replaced nor merged with the restart type.
    ///   - PenaltyKick (a RESTART TYPE here) is SEPARATE from UsePenaltyShootout (Task 101, a
    ///     MATCH/COMPETITION rule). They are never merged; shootout structure is not duplicated.
    ///   - FreeKickAccuracy remains PLAYER DATA (PlayerStats.Passing, Task 70), not match rules.
    ///   - Task 106 is DATA ONLY: no restart execution/system/state, no ball/physics fields, no
    ///     GameClock change, no Player/Team/BallConfig contamination.
    ///   - The representation is a CLOSED enum with no configurable invalid authored state -- the
    ///     Task 97 GetInvalidMatchRulesData boundary remains the single authority (default container
    ///     valid) and never mutates. No independent RestartValidator is created.
    /// </summary>
    public class RestartRulesTests
    {
        private const string DataPrefix = "Football.Data.";
        private const string CorePrefix = "Football.Core.";

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

        private static IEnumerable<Type> AllTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().SelectMany(SafeGetTypes);
        }

        private static bool HasTypeName(params string[] names)
        {
            var all = AllTypes().ToList();
            return names.Any(n => all.Any(t => t.Name == n));
        }

        private static Type MatchRulesType() => FindType(DataPrefix + "MatchRulesDefinition");

        private static Type MatchRestartType() => FindType(DataPrefix + "MatchRestartType");

        private static Type GameStateIdType() => FindType(CorePrefix + "GameStateId");

        private static object NewMatchRules() => ScriptableObject.CreateInstance(MatchRulesType());

        private static FieldInfo FieldOf(Type t, string name) =>
            t.GetField(name, BindingFlags.Public | BindingFlags.Instance);

        private static List<string> GetInvalidMatchRulesData(object rules) =>
            (List<string>)MatchRulesType()
                .GetMethod("GetInvalidMatchRulesData", BindingFlags.Public | BindingFlags.Instance)
                .Invoke(rules, null);

        // ---- 106.1 Representation ----

        [Test]
        public void RestartRepresentationExists()
        {
            var t = MatchRestartType();
            Assert.IsTrue(t.IsEnum, "MatchRestartType must be an enum (strongly typed representation).");
            Assert.AreEqual(7, Enum.GetNames(t).Length,
                "MatchRestartType must define exactly the seven approved restart types.");
        }

        [Test]
        public void RestartTypeIsStronglyTyped()
        {
            var t = MatchRestartType();
            Assert.IsTrue(t.IsEnum, "MatchRestartType must be an enum, not a string/magic integer.");
            Assert.AreNotEqual(typeof(string), t, "No string restartType representation.");
        }

        // ---- 106.2 Supported restart types ----

        [Test]
        public void AllSupportedRestartsPresent()
        {
            var names = Enum.GetNames(MatchRestartType());
            foreach (var expected in new[] { "KickOff", "ThrowIn", "GoalKick", "CornerKick",
                                             "FreeKick", "PenaltyKick", "DropBall" })
            {
                Assert.IsTrue(names.Contains(expected), $"MatchRestartType must define {expected}.");
            }
        }

        [Test]
        public void RestartsAreDistinct()
        {
            var names = Enum.GetNames(MatchRestartType());
            Assert.AreEqual(7, names.Distinct().Count(),
                "The seven restart types must be distinct enum values.");
        }

        // ---- 106.5 GameStateId vs RestartType ----

        [Test]
        public void RestartTypeIsSeparateFromGameStateId()
        {
            var restart = MatchRestartType();
            var gameState = GameStateIdType();
            Assert.IsNotNull(gameState, "GameStateId (Football.Core) must continue to exist.");
            Assert.AreNotEqual(restart, gameState,
                "MatchRestartType must be a distinct enum from GameStateId.");
            Assert.IsTrue(Enum.GetNames(gameState).Contains("KickOff"),
                "GameStateId.KickOff must remain a game-state value (not replaced/merged).");
        }

        [Test]
        public void GameStateIdKickOffRemainsUnchanged()
        {
            var gameState = GameStateIdType();
            // GameStateId is a phase-flow state enum; KickOff membership preserved and distinct from any restart type.
            var stateNames = Enum.GetNames(gameState);
            Assert.Contains("KickOff", stateNames);
            Assert.Contains("Playing", stateNames);
            Assert.Contains("HalfTime", stateNames);
        }

        // ---- 106.4 PenaltyKick vs PenaltyShootout ----

        [Test]
        public void PenaltyKickIsSeparateFromPenaltyShootout()
        {
            var restart = MatchRestartType();
            Assert.IsTrue(Enum.GetNames(restart).Contains("PenaltyKick"),
                "PenaltyKick is a restart type here.");
            // UsePenaltyShootout (Task 101) is a separate authored bool rule, not a restart.
            var f = FieldOf(MatchRulesType(), "UsePenaltyShootout");
            Assert.IsNotNull(f, "UsePenaltyShootout (Task 101) must remain.");
            Assert.AreEqual(typeof(bool), f.FieldType,
                "Penalty Shootout is a MATCH/COMPETITION rule (bool), distinct from the PenaltyKick restart type.");
        }

        [Test]
        public void NoPenaltyShootoutStructureDuplicated()
        {
            // Shootout duration/count/rules are NOT duplicated into restart rules.
            foreach (var n in new[] { "PenaltyShootoutRounds", "PenaltyShootoutKicks",
                                      "ShootoutRounds", "ShootoutKicks" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Restart rules must not duplicate shootout structure; must not create '{n}'.");
            }
        }

        // ---- 106.1 FreeKickAccuracy stays Player Data ----

        [Test]
        public void FreeKickAccuracyIsPlayerDataNotMatchRestart()
        {
            // PlayerStats.Passing.FreeKickAccuracy is Player Data (Task 70); not on match rules.
            Assert.IsNull(FieldOf(MatchRulesType(), "FreeKickAccuracy"),
                "FreeKickAccuracy is Player Data, not a Match restart field.");
        }

        // ---- 106.3 / 106.5 Runtime & ownership boundaries ----

        [Test]
        public void NoRuntimeRestartStateOnMatchRules()
        {
            foreach (var n in new[] { "CurrentRestart", "ActiveRestart", "PendingRestart",
                                      "LastRestart", "RestartState", "RestartTimer",
                                      "RestartExecutor", "CurrentRestartPlayer",
                                      "CurrentRestartPosition" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"MatchRulesDefinition is authored config; must not hold runtime restart state '{n}'.");
            }
        }

        [Test]
        public void NoRestartSystemsOrRuleTypesCreated()
        {
            Assert.IsFalse(HasTypeName("RestartSystem", "RestartManager", "KickoffSystem",
                    "KickoffManager", "ThrowInSystem", "GoalKickSystem", "CornerKickSystem",
                    "FreeKickSystem", "PenaltyKickSystem", "DropBallSystem", "FoulToRestartSystem"),
                "Task 106 is data only; no restart/referee gameplay systems may be created.");
            Assert.IsFalse(HasTypeName("KickOffRule", "ThrowInRule", "GoalKickRule", "CornerKickRule",
                    "FreeKickRule", "PenaltyKickRule", "DropBallRule"),
                "The 7-value MatchRestartType enum is the single restart representation; no per-restart rule types.");
        }

        [Test]
        public void NoBallPhysicsInRestartData()
        {
            // BallConfig remains the sole ball physics owner; no ball fields on match rules.
            foreach (var n in new[] { "RestartBallSpeed", "RestartBallForce", "RestartBallDirection",
                                      "BallRadius", "BallMass", "GravityMultiplier",
                                      "RestartDelay", "RestartDistance", "RestartPlayerCount" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Match Rules must not own ball/physics or speculative restart gameplay; must not create '{n}'.");
            }
        }

        [Test]
        public void NoPlayerOrTeamDataContamination()
        {
            foreach (var n in new[] { "KickTaker", "RestartTaker", "CornerTaker", "ThrowInTaker" })
            {
                Assert.IsNull(FieldOf(MatchRulesType(), n),
                    $"Restart taker is runtime match state, not match rule data; must not create '{n}'.");
            }
        }

        // ---- 106.6 Validation ----

        [Test]
        public void DefaultRestartConfigIsValid()
        {
            // Closed enum (no configurable invalid authored state); default container validates cleanly.
            Assert.IsEmpty(GetInvalidMatchRulesData(NewMatchRules()));
        }

        [Test]
        public void NoIndependentRestartValidator()
        {
            Assert.IsFalse(HasTypeName("RestartValidator", "MatchRestartValidator", "KickoffValidator"),
                "Restart validation stays within the Task 97 GetInvalidMatchRulesData boundary; no independent validator.");
        }

        // ---- 106.3 Duplicate authority ----

        [Test]
        public void NoDuplicateRestartAuthority()
        {
            Assert.IsFalse(HasTypeName("RestartRule", "RestartRules", "RestartDefinition",
                    "MatchRestartRule", "MatchRestart"),
                "MatchRestartType is the single restart taxonomy; no duplicate restart type/authority.");
            Assert.IsFalse(HasTypeName("RestartConfig", "KickoffConfig", "FreeKickConfig"),
                "No per-restart configuration assets/types (taxonomy is sufficient).");
        }
    }
}
