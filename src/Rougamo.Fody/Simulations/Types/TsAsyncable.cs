﻿using Fody;
using Mono.Cecil;

namespace Rougamo.Fody.Simulations.Types
{
    /// <summary>
    /// Any type which support async syntax. etc: Task, ValueTask, Task&lt;T&gt;, ValueTask&lt;T&gt;
    /// </summary>
    internal class TsAsyncable(TypeReference typeRef, IHost? host, BaseModuleWeaver moduldeWeaver) : TypeSimulation(typeRef, host, moduldeWeaver)
    {
        public MethodSimulation<TsAwaiter> M_GetAwaiter => MethodSimulate<TsAwaiter>(Constants.METHOD_GetAwaiter, true);
    }
}
