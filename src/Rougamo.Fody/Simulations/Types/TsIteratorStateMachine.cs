﻿using Fody;
using Fody.Simulations;
using Fody.Simulations.Types;
using Mono.Cecil;
using Rougamo.Fody.Contexts;
using System.Linq;

namespace Rougamo.Fody.Simulations.Types
{
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    internal class TsIteratorStateMachine(TypeReference typeRef, IHost? host, SimulationModuleWeaver moduleWeaver) : TsStateMachine(typeRef, host, moduleWeaver), IIteratorStateMachine
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
    {
        public FieldSimulation F_Current { get; private set; }

        public FieldSimulation<TsList>? F_Items { get; private set; }

        public FieldSimulation<TsEnumerator> F_Iterator { get; private set; }

        public MethodSimulation M_Dispose => MethodSimulate(Constants.METHOD_FULL_Dispose, false);

        public TsIteratorStateMachine SetFields(IteratorFields fields)
        {
            F_Mos = fields.Mos.Select(x => x.Simulate<TsMo>(this)).ToArray();
            F_MethodContext = fields.MethodContext.Simulate<TsMethodContext>(this);
            F_State = fields.State.Simulate(this);
            F_Parameters = fields.Parameters.Select(x => x!.Simulate(this)).ToArray();
            F_DeclaringThis = fields.DeclaringThis?.Simulate<TsWeavingTarget>(this);
            F_Current = fields.Current.Simulate(this);
            F_Items = fields.RecordedReturn?.Simulate<TsList>(this);
            F_Iterator = fields.Iterator.Simulate<TsEnumerator>(this);

            return this;
        }
    }
}
