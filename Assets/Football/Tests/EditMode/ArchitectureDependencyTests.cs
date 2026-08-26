using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Football.Tests.EditMode
{
    public class ArchitectureDependencyTests
    {
        private static readonly string RuntimeRoot = "Assets/Football/Runtime";
        private static readonly string ProjectRoot = "Assets/Football";

        private static readonly Dictionary<string, string[]> ForbiddenDependencies = new()
        {
            { "Football.Core", new[] {
                "Football.Players", "Football.Ball", "Football.Actions",
                "Football.Match", "Football.Teams", "Football.AI",
                "Football.Camera", "Football.UI", "Football.World", "Football.Input"
            }},
            { "Football.Camera", new[] {
                "Football.Players", "Football.Ball", "Football.Actions",
                "Football.Match", "Football.Teams", "Football.AI"
            }},
            { "Football.UI", new[] {
                "Football.Players", "Football.Ball", "Football.Actions",
                "Football.Match", "Football.Teams", "Football.AI"
            }},
        };

        [Test]
        public void CoreAssembly_HasNoReferences()
        {
            var refs = GetAssemblyReferences("Football.Core");
            Assert.AreEqual(0, refs.Length,
                $"Football.Core must have zero references. Found: {string.Join(", ", refs)}");
        }

        [Test]
        public void NoCircularDependenciesExist()
        {
            var asmdefs = GetAllAssemblyDefinitions();
            var graph = new Dictionary<string, HashSet<string>>();

            foreach (var asmdef in asmdefs)
            {
                graph[asmdef.name] = new HashSet<string>(asmdef.references ?? new string[0]);
            }

            foreach (var kvp in graph)
            {
                foreach (var dependency in kvp.Value)
                {
                    if (graph.ContainsKey(dependency) && graph[dependency].Contains(kvp.Key))
                    {
                        Assert.Fail($"Circular dependency detected: {kvp.Key} <-> {dependency}");
                    }
                }
            }
        }

        [Test]
        public void ForbiddenDependencies_AreRejected()
        {
            foreach (var kvp in ForbiddenDependencies)
            {
                var assemblyName = kvp.Key;
                var forbidden = kvp.Value;
                var refs = GetAssemblyReferences(assemblyName);

                foreach (var f in forbidden)
                {
                    Assert.IsFalse(refs.Contains(f),
                        $"{assemblyName} must not depend on {f}. Actual refs: {string.Join(", ", refs)}");
                }
            }
        }

        [Test]
        public void CameraDependsOnlyOnCore()
        {
            var refs = GetAssemblyReferences("Football.Camera");
            Assert.AreEqual(1, refs.Length,
                $"Football.Camera should depend only on Football.Core. Found: {string.Join(", ", refs)}");
            Assert.AreEqual("Football.Core", refs[0]);
        }

        [Test]
        public void UIDependsOnlyOnCore()
        {
            var refs = GetAssemblyReferences("Football.UI");
            Assert.AreEqual(1, refs.Length,
                $"Football.UI should depend only on Football.Core. Found: {string.Join(", ", refs)}");
            Assert.AreEqual("Football.Core", refs[0]);
        }

        [Test]
        public void InputDependsOnlyOnCore()
        {
            var refs = GetAssemblyReferences("Football.Input");
            Assert.AreEqual(1, refs.Length,
                $"Football.Input should depend only on Football.Core. Found: {string.Join(", ", refs)}");
            Assert.AreEqual("Football.Core", refs[0]);
        }

        [Test]
        public void PlayersDependsOnlyOnCore()
        {
            var refs = GetAssemblyReferences("Football.Players");
            Assert.AreEqual(1, refs.Length,
                $"Football.Players should depend only on Football.Core. Found: {string.Join(", ", refs)}");
            Assert.AreEqual("Football.Core", refs[0]);
        }

        [Test]
        public void BallDependsOnlyOnCore()
        {
            var refs = GetAssemblyReferences("Football.Ball");
            Assert.AreEqual(1, refs.Length,
                $"Football.Ball should depend only on Football.Core. Found: {string.Join(", ", refs)}");
            Assert.AreEqual("Football.Core", refs[0]);
        }

        [Test]
        public void AllAssembliesExist()
        {
            var expected = new[]
            {
                "Football.Core", "Football.Input", "Football.Players", "Football.Ball",
                "Football.Actions", "Football.Match", "Football.Teams", "Football.AI",
                "Football.Camera", "Football.UI", "Football.World"
            };

            var existing = GetAllAssemblyDefinitions()
                .Select(a => a.name)
                .ToHashSet();

            foreach (var name in expected)
            {
                Assert.IsTrue(existing.Contains(name), $"Assembly {name}.asmdef not found under {RuntimeRoot}");
            }
        }

        [Test]
        public void CoreHasNoGameplaySingletons()
        {
            var coreDir = Path.Combine(RuntimeRoot, "Core");
            var csFiles = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                Assert.IsFalse(content.Contains("static Instance"),
                    $"Core file has Singleton pattern: {Path.GetRelativePath(ProjectRoot, file)}");
            }
        }

        [Test]
        public void NoDuplicateInterfaceContracts()
        {
            var coreDir = Path.Combine(RuntimeRoot, "Core", "Interfaces");
            if (!Directory.Exists(coreDir)) return;

            var files = Directory.GetFiles(coreDir, "*.cs");
            var names = files.Select(f => Path.GetFileNameWithoutExtension(f)).ToList();

            Assert.IsTrue(names.Contains("IState"), "IState interface missing");
            Assert.IsTrue(names.Contains("IStateMachine"), "IStateMachine interface missing");
            Assert.IsTrue(names.Contains("IStateMachine{TStateId}"), "IStateMachine<TStateId> interface missing");
            Assert.IsTrue(names.Contains("IGameEvent"), "IGameEvent interface missing");
            Assert.IsTrue(names.Contains("IFootballService"), "IFootballService interface missing");
        }

        [Test]
        public void StateMachineContracts_ConsistentDeltaTime()
        {
            var coreDir = Path.Combine(RuntimeRoot, "Core");
            var csFiles = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);

            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                if (content.Contains("void Tick(") && !content.Contains("deltaTime"))
                {
                    Assert.Fail($"State machine Tick method missing deltaTime: {Path.GetRelativePath(ProjectRoot, file)}");
                }
            }
        }

        [Test]
        public void GameEvents_IsThinDispatcher()
        {
            var eventsDir = Path.Combine(RuntimeRoot, "Core", "Events");
            if (!Directory.Exists(eventsDir)) return;

            var gameEventsFile = Path.Combine(eventsDir, "GameEvents.cs");
            Assert.IsTrue(File.Exists(gameEventsFile), "GameEvents.cs not found");

            var content = File.ReadAllText(gameEventsFile);
            Assert.IsTrue(content.Contains("Subscribe"), "GameEvents missing Subscribe");
            Assert.IsTrue(content.Contains("Unsubscribe"), "GameEvents missing Unsubscribe");
            Assert.IsTrue(content.Contains("Raise"), "GameEvents missing Raise");
            Assert.IsTrue(content.Contains("Clear"), "GameEvents missing Clear");

            var lines = content.Split('\n').Length;
            Assert.LessOrEqual(lines, 60,
                $"GameEvents.cs should be thin dispatcher (<=60 lines). Actual: {lines}");
        }

        [Test]
        public void EventStructs_AreImmutable()
        {
            var eventsDir = Path.Combine(RuntimeRoot, "Core", "Events");
            if (!Directory.Exists(eventsDir)) return;

            var eventFiles = Directory.GetFiles(eventsDir, "*Event.cs");
            Assert.Greater(eventFiles.Length, 0, "No event files found");

            foreach (var file in eventFiles)
            {
                var content = File.ReadAllText(file);
                Assert.IsTrue(content.Contains("readonly struct"),
                    $"Event struct should be readonly: {Path.GetFileName(file)}");
            }
        }

        [Test]
        public void ScriptableObjects_InDataDirectory()
        {
            var dataDir = Path.Combine(RuntimeRoot, "Data");
            if (!Directory.Exists(dataDir)) return;

            var csFiles = Directory.GetFiles(dataDir, "*.cs");
            Assert.Greater(csFiles.Length, 0, "No ScriptableObject files found in Data/");

            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                Assert.IsTrue(content.Contains(": ScriptableObject"),
                    $"File in Data/ should be ScriptableObject: {Path.GetFileName(file)}");
                Assert.IsFalse(content.Contains("Update("),
                    $"ScriptableObject in Data/ must not have Update: {Path.GetFileName(file)}");
            }
        }

        [Test]
        public void Prefabs_RequiredStructure()
        {
            Assert.IsTrue(File.Exists($"{ProjectRoot}/Prefabs/Players/Player.prefab"), "Player.prefab missing");
            Assert.IsTrue(File.Exists($"{ProjectRoot}/Prefabs/Ball/SoccerBall.prefab"), "SoccerBall.prefab missing");
            Assert.IsTrue(File.Exists($"{ProjectRoot}/Prefabs/Stadium/Stadium.prefab"), "Stadium.prefab missing");
        }

        [Test]
        public void Scenes_RequiredScenes()
        {
            Assert.IsTrue(File.Exists($"{ProjectRoot}/Scenes/Bootstrap/Bootstrap.unity"), "Bootstrap.unity missing");
            Assert.IsTrue(File.Exists($"{ProjectRoot}/Scenes/Match/Match.unity"), "Match.unity missing");
        }

        private static string[] GetAssemblyReferences(string assemblyName)
        {
            var asmdefs = GetAllAssemblyDefinitions();
            var asmdef = asmdefs.FirstOrDefault(a => a.name == assemblyName);

            if (asmdef == null)
            {
                Assert.Fail($"Assembly definition {assemblyName} not found under {RuntimeRoot}");
                return new string[0];
            }

            return asmdef.references ?? new string[0];
        }

        private static List<AsmDefData> GetAllAssemblyDefinitions()
        {
            var files = Directory.GetFiles(RuntimeRoot, "*.asmdef", SearchOption.AllDirectories);
            var result = new List<AsmDefData>();

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var data = JsonUtility.FromJson<AsmDefData>(text);
                if (data != null && !string.IsNullOrEmpty(data.name))
                    result.Add(data);
            }

            return result;
        }

        [System.Serializable]
        private class AsmDefData
        {
            public string name;
            public string[] references;
        }
    }
}
