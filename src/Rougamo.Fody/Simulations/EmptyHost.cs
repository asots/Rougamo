﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using System.Collections.Generic;

namespace Rougamo.Fody.Simulations
{
    internal class EmptyHost: IHost
    {
        public TypeSimulation TypeRef => GlobalSimulations.Object;

        public TypeSimulation Type => throw new System.NotImplementedException();

        public IList<Instruction> Cast(TypeReference to) => [];

        public IList<Instruction> Load() => [];

        public IList<Instruction> LoadAddress(MethodSimulation method) => [];

        public IList<Instruction> LoadForCallingMethod() => [];

        public IList<Instruction> PrepareLoadAddress(MethodSimulation method) => [];
    }
}
