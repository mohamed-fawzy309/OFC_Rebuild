using System;

namespace Football.Core
{
    /// <summary>
    /// A small, fixed taxonomy of bootstrap failure categories.
    ///
    /// This is deliberately NOT a deep hierarchy. The category exists to answer three
    /// questions about a startup failure: how to diagnose it, how to report it, and whether
    /// startup can legitimately continue. In the current project every required bootstrap
    /// failure is FATAL (startup stops and moves to <see cref="BootstrapState.Failed"/>);
    /// recoverable paths are reserved for future optional systems and no "log and continue"
    /// semantics are implied by any category here.
    /// </summary>
    public enum BootstrapErrorCategory
    {
        /// <summary>An illegal configuration of the bootstrap itself or its infrastructure. Fatal.</summary>
        Configuration,

        /// <summary>Service registration, lookup, dependency, or initialization failure. Fatal.</summary>
        Service,

        /// <summary>Initial scene load / scene transition failure. Fatal.</summary>
        SceneLoading,

        /// <summary>Any failure not attributable to the narrow categories above. Fatal.</summary>
        Unexpected
    }

    /// <summary>
    /// An immutable snapshot of a bootstrap failure. There is at most ONE of these per
    /// bootstrap lifecycle: it is created once at the failure boundary, stored by the
    /// <see cref="GameBootstrap"/>, and never mutated.
    ///
    /// The ORIGINAL exception is preserved by reference so its type, message, and stack
    /// trace are never lost or replaced with a generic message. Callers are expected to
    /// read <see cref="Exception"/> (directly or via InnerException) rather than a copied
    /// string.
    /// </summary>
    public sealed class BootstrapError
    {
        public BootstrapError(
            BootstrapErrorCategory category,
            StartupPhase phase,
            string system,
            Exception exception)
        {
            Category = category;
            Phase = phase;
            System = system;
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        }

        /// <summary>The failure category for diagnosis and reporting.</summary>
        public BootstrapErrorCategory Category { get; }

        /// <summary>The startup phase that failed. Deterministic, pipeline-based.</summary>
        public StartupPhase Phase { get; }

        /// <summary>The subsystem boundary that was executing when the failure occurred.</summary>
        public string System { get; }

        /// <summary>The original exception, preserved in full. Never null.</summary>
        public Exception Exception { get; }
    }
}
