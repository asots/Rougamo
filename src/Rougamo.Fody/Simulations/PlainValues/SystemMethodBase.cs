﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;
using System.Diagnostics;

namespace Rougamo.Fody.Simulations.PlainValues
{
    [DebuggerDisplay("{methodDef}")]
    internal class SystemMethodBase(MethodDefinition methodDef) : PlainValueSimulation(null!)
    {
        public override TypeSimulation Type => GlobalSimulations.MethodBase;

        public override IList<Instruction> Load()
        {
            return [
                Instruction.Create(OpCodes.Ldtoken, methodDef),
                Instruction.Create(OpCodes.Ldtoken, methodDef.DeclaringType),
                Instruction.Create(OpCodes.Call, GlobalRefs.MrGetMethodFromHandle)
            ];
        }
    }
}
