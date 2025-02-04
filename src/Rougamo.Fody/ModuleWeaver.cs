﻿using Fody;
using Mono.Cecil;
using Rougamo.Fody.Models;
using System;
using System.Collections.Generic;

namespace Rougamo.Fody
{
    public partial class ModuleWeaver : SimulationModuleWeaver
    {
        internal TypeReference _tObjectArrayRef;
        internal TypeReference _tListRef;
        internal TypeReference _tExceptionRef;
        internal TypeReference _tCancellationTokenRef;
        internal TypeReference _tIAsyncStateMachineRef;
        internal TypeReference _tAsyncTaskMethodBuilderRef;
        internal TypeReference _tAsyncTaskMethodBuilder1Ref;
        internal TypeReference _tValueTaskRef;
        internal TypeReference _tValueTaskAwaiterRef;
        internal TypeReference _tIMoArrayRef;
        internal TypeReference _tMethodContextRef;
        internal TypeReference _tPoolRef;

        internal MethodReference _ctorObjectRef;
        internal MethodReference _ctorDebuggerStepThroughRef;
        internal MethodReference _ctorCompilerGeneratedAttributeRef;
        internal MethodReference _ctorDebuggerHiddenAttributeRef;
        internal MethodReference _ctorAsyncStateMachineAttributeRef;
        internal MethodReference? _ctorStackTraceHiddenAttributeRef;

        internal MethodReference _mPoolGetRef;
        internal MethodReference _mPoolReturnRef;
        internal MethodReference _mExceptionDispatchInfoCaptureRef;
        internal MethodReference _mIAsyncStateMachineMoveNextRef;
        internal MethodReference _mIAsyncStateMachineSetStateMachineRef;
        internal MethodReference _mExceptionDispatchInfoThrowRef;
        internal Dictionary<string, MethodReference> _stateMachineCtorRefs;

        private List<RouType> _rouTypes;
        private Config? _configuration;

        private Config Configuration
        {
            get
            {
                _configuration ??= GetConfiguration();

                return _configuration;
            }
        }

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        public ModuleWeaver() : base(false) { }

        public ModuleWeaver(bool testRun) : base(testRun) { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        protected override bool Enabled() => Configuration.Enabled;

        protected override void ExecuteInternal()
        {
            FindRous();
            if (_rouTypes.Count == 0) return;
            WeaveMos();
        }

        private Config GetConfiguration()
        {
            var enabled = "true".Equals(GetConfigValue("true", "enabled"), StringComparison.OrdinalIgnoreCase);
            var compositeAccessibility = "true".Equals(GetConfigValue("false", "composite-accessibility"), StringComparison.OrdinalIgnoreCase);
            var skipRefStruct = "true".Equals(GetConfigValue("false", "skip-ref-struct"), StringComparison.OrdinalIgnoreCase);
#if DEBUG
            var recordingIteratorReturns = true;
#else
            var recordingIteratorReturns = "true".Equals(GetConfigValue("false", "iterator-returns"), StringComparison.OrdinalIgnoreCase);
#endif
            var reverseCallNonEntry = "true".Equals(GetConfigValue("true", "reverse-call-nonentry"), StringComparison.OrdinalIgnoreCase);
            var pureStackTrace = "true".Equals(GetConfigValue("true", "pure-stacktrace"), StringComparison.OrdinalIgnoreCase);
            var exceptTypePatterns = GetConfigValue(string.Empty, "except-type-patterns").Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            var mos = new List<Config.Mo>();
            var xMos = Config.Element("Mos");
            if (xMos != null)
            {
                foreach (var xMo in xMos.Elements())
                {
                    if (xMo.Name != "Mo") continue;

                    var assembly = xMo.Attribute("assembly")?.Value;
                    var type = xMo.Attribute("type")?.Value;
                    var pattern = xMo.Attribute("pattern")?.Value;

                    if (assembly == null || type == null) continue;

                    mos.Add(new(assembly, type, pattern));
                }
            }

            return new(enabled, compositeAccessibility, skipRefStruct, recordingIteratorReturns, reverseCallNonEntry, pureStackTrace, exceptTypePatterns, mos.ToArray());
        }

        private void StackTraceHidden(MethodDefinition methodDef)
        {
            if (_ctorStackTraceHiddenAttributeRef == null || !Configuration.PureStackTrace) return;

            methodDef.CustomAttributes.Add(new(_ctorStackTraceHiddenAttributeRef));
        }
    }
}
