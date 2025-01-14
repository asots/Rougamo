﻿using Fody;
using Fody.Simulations;
using Mono.Cecil;

namespace Rougamo.Fody.Simulations.Types
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal abstract class TsStateMachine(TypeReference typeRef, IHost? host, SimulationModuleWeaver moduleWeaver) : TypeSimulation(typeRef, host, moduleWeaver), IStateMachine
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        private FieldSimulation _fState;

        public FieldSimulation<TsMo>[]? F_Mos { get; protected set; }

        public FieldSimulation<TsMethodContext> F_MethodContext { get; protected set; }

        public FieldSimulation F_State
        {
            get => _fState ??= FieldSimulate(Constants.FIELD_State);
            set => _fState = value;
        }

        public FieldSimulation[] F_Parameters { get; protected set; }

        public FieldSimulation<TsWeavingTarget>? F_DeclaringThis { get; protected set; }

        public MethodSimulation M_MoveNext => MethodSimulate(Constants.METHOD_MoveNext, false);
    }
}
