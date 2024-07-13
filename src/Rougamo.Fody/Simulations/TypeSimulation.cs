﻿using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.Concurrent;
using Mono.Cecil.Cil;
using static Mono.Cecil.Cil.Instruction;
using Mono.Cecil.Rocks;
using Rougamo.Fody.Simulations.PlainValues;

namespace Rougamo.Fody.Simulations
{
    internal class TypeSimulation : Simulation, IHost
    {
        private readonly Dictionary<string, object?> _fieldSimulations = [];
        private readonly Dictionary<string, MethodSimulation> _methodSimulations = [];
        private readonly Dictionary<string, PropertySimulation?> _propertySimulations = [];

        public TypeSimulation(TypeReference typeRef, IHost? host, ModuleDefinition moduleDef) : base(moduleDef)
        {
            Ref = typeRef.ImportInto(moduleDef);
            Def = typeRef.Resolve();
            Host = host ?? new This(this);
        }

        public TypeReference Ref { get; set; }

        public TypeDefinition Def { get; }

        public IHost Host { get; }

        public bool IsValueType => Ref.IsValueType;

        public TypeSimulation Type => this;

        public virtual OpCode TrueToken => OpCodes.Brtrue;

        public virtual OpCode FalseToken => OpCodes.Brfalse;

        public virtual IList<Instruction> New(params IParameterSimulation?[] arguments)
        {
            if (IsValueType && arguments.Length == 0)
            {
                return [Create(OpCodes.Initobj, Ref)];
            }
            arguments = arguments.Select(x => x ?? new Null()).ToArray();
            // todo: 考虑泛型参数问题
            var ctorDef = Def.GetConstructors().Single(x => x.Parameters.Count == arguments.Length && x.Parameters.Select(y => y.ParameterType.FullName).SequenceEqual(arguments.Select(y => y!.Type.Ref.FullName)));
            var ctorRef = ctorDef.ImportInto(Module).WithGenericDeclaringType(Ref);
            return ctorDef.Simulate(this).Call(arguments);
        }

        public virtual IList<Instruction> Default()
        {
            if (IsValueType || Ref.IsGenericParameter)
            {
                return [Create(OpCodes.Initobj, Ref)];
            }
            return [Create(OpCodes.Ldnull)];
        }

        public virtual IList<Instruction> LoadForCallingMethod() => Host.LoadForCallingMethod();

        public virtual IList<Instruction> PrepareLoadAddress(MethodSimulation method) => Host.PrepareLoadAddress(method);

        public virtual IList<Instruction> LoadAddress(MethodSimulation method) => Host.LoadAddress(method);

        public IList<Instruction> Load() => Host.Load();

        public virtual IList<Instruction> Cast(TypeReference to)
        {
            if (to.FullName == Ref.FullName) return [];

            if (Ref.IsObject())
            {
                if (to.IsValueType || to.IsGenericParameter)
                {
                    return [Create(OpCodes.Unbox_Any, to)];
                }
                return [Create(OpCodes.Castclass, to)];
            }

            if (to.IsValueType || to.IsGenericParameter) throw new RougamoException($"Cannot convert {Ref} to {to}. Only object types can be converted to value types.");

            var toDef = to.Resolve();
            if (toDef.IsInterface && Ref.Implement(to.FullName) || !toDef.IsInterface && Ref.IsOrDerivesFrom(to.FullName)) return [];

            return [Create(OpCodes.Castclass, to)];
        }

        #region Simulate

        #region Simulate-Method
        protected MethodSimulation<TRet> MethodSimulate<TRet>(string methodName) where TRet : TypeSimulation => MethodSimulate<TRet>(methodName, x => x.Name == methodName);

        protected MethodSimulation<TRet> MethodSimulate<TRet>(string id, Func<MethodDefinition, bool> predicate) where TRet : TypeSimulation
        {
            if (!_methodSimulations.TryGetValue(id, out var simulation))
            {
                simulation = Def.Methods.Single(predicate).Simulate<TRet>(this);
                _methodSimulations[id] = simulation;
            }
            return (MethodSimulation<TRet>)simulation;
        }

        protected MethodSimulation<TRet> PublicMethodSimulate<TRet>(string methodName) where TRet : TypeSimulation => MethodSimulate<TRet>(methodName, x => x.Name == methodName && x.IsPublic);

        protected MethodSimulation MethodSimulate(string methodName) => MethodSimulate(methodName, x => x.Name == methodName);

        protected MethodSimulation MethodSimulate(string id, Func<MethodDefinition, bool> predicate)
        {
            if (!_methodSimulations.TryGetValue(id, out var simulation))
            {
                simulation = Def.Methods.Single(predicate).Simulate(this);
                _methodSimulations[id] = simulation;
            }
            return simulation;
        }

        protected MethodSimulation PublicMethodSimulate(string methodName) => MethodSimulate(methodName, x => x.Name == methodName && x.IsPublic);
        #endregion Simulate-Method

        #region Simulate-Field
        protected FieldSimulation<T> FieldSimulate<T>(string fieldName) where T : TypeSimulation => FieldSimulate<T>(fieldName, x => x.Name == fieldName);

        protected FieldSimulation<T> FieldSimulate<T>(string id, Func<FieldDefinition, bool> predicate) where T : TypeSimulation
        {
            if (!_fieldSimulations.TryGetValue(id, out var simulation))
            {
                simulation = Def.Fields.Single(predicate).Simulate<T>(this);
                _fieldSimulations[id] = simulation;
            }
            return (FieldSimulation<T>)simulation!;
        }

        protected FieldSimulation<T>[] FieldSimulates<T>(string id, Func<FieldDefinition, bool> predicate) where T : TypeSimulation
        {
            if (!_fieldSimulations.TryGetValue(id, out var simulation))
            {
                simulation = Def.Fields.Where(predicate).Select(x => x.Simulate<T>(this)).ToArray();
                _fieldSimulations[id] = simulation;
            }
            return (FieldSimulation<T>[])simulation!;
        }

        protected FieldSimulation<T>? OptionalFieldSimulate<T>(string fieldName) where T : TypeSimulation => FieldSimulate<T>(fieldName, x => x.Name == fieldName);

        protected FieldSimulation<T>? OptionalFieldSimulate<T>(string id, Func<FieldDefinition, bool> predicate) where T : TypeSimulation
        {
            if (!_fieldSimulations.TryGetValue(id, out var simulation))
            {
                var field = Def.Fields.SingleOrDefault(predicate);
                simulation = field?.Simulate<T>(this);
                _fieldSimulations[id] = simulation;
            }
            return (FieldSimulation<T>?)simulation;
        }

        protected FieldSimulation<T>[]? OptionalFieldSimulates<T>(string id, Func<FieldDefinition, bool> predicate) where T : TypeSimulation
        {
            if (!_fieldSimulations.TryGetValue(id, out var simulation))
            {
                var simulations = Def.Fields.Where(predicate).Select(x => x.Simulate<T>(this)).ToArray();
                simulation = simulations.Length == 0 ? null : simulations;
                _fieldSimulations[id] = simulation;
            }
            return (FieldSimulation<T>[]?)simulation;
        }
        #endregion Simulate-Field

        #region Simulate-Property
        protected PropertySimulation PropertySimulate(string propertyName, bool recursion)
        {
            return PropertySimulateInner(propertyName, recursion, false)!;
        }

        protected PropertySimulation<T> PropertySimulate<T>(string propertyName, bool recursion) where T : TypeSimulation
        {
            return PropertySimulateInner<T>(propertyName, recursion, false)!;
        }

        protected PropertySimulation? OptionalPropertySimulate(string propertyName, bool recursion)
        {
            return PropertySimulateInner(propertyName, recursion, true);
        }

        protected PropertySimulation<T>? OptionalPropertySimulate<T>(string propertyName, bool recursion) where T : TypeSimulation
        {
            return PropertySimulateInner<T>(propertyName, recursion, true);
        }

        private PropertySimulation? PropertySimulateInner(string propertyName, bool recursion, bool optional)
        {
            if (!_propertySimulations.TryGetValue(propertyName, out var simulation))
            {
                var def = Def;
                PropertyDefinition? propertyDef;
                do
                {
                    propertyDef = def.Properties.SingleOrDefault(x => x.Name == propertyName);
                } while (propertyDef == null && recursion && (def = def.BaseType.Resolve()) != null);
                if (propertyDef == null && !optional) throw new RougamoException($"Cannot find property({propertyName}) from {Def.FullName}");

                simulation = propertyDef?.Simulate(this);
                _propertySimulations[propertyName] = simulation;
            }

            return simulation;
        }

        private PropertySimulation<T>? PropertySimulateInner<T>(string propertyName, bool recursion, bool optional) where T : TypeSimulation
        {
            if (!_propertySimulations.TryGetValue(propertyName, out var simulation))
            {
                var def = Def;
                PropertyDefinition? propertyDef;
                do
                {
                    propertyDef = def.Properties.SingleOrDefault(x => x.Name == propertyName);
                } while (propertyDef == null && recursion && (def = def.BaseType.Resolve()) != null);
                if (propertyDef == null && !optional) throw new RougamoException($"Cannot find property({propertyName}) from {Def.FullName}");

                simulation = propertyDef?.Simulate<T>(this);
                _propertySimulations[propertyName] = simulation;
            }

            return (PropertySimulation<T>?)simulation;
        }
        #endregion Simulate-Property

        #endregion Simulate

        public static implicit operator TypeReference(TypeSimulation value) => value.Ref;
    }

    internal static class TypeSimulationExtensions
    {
        private static readonly ConcurrentDictionary<Type, Func<TypeReference, IHost?, ModuleDefinition, object>> _Cache = [];

        public static TypeSimulation Simulate(this TypeReference typeRef, ModuleDefinition moduleDef) => new(typeRef, null, moduleDef);

        public static TypeSimulation Simulate(this TypeReference typeRef, IHost? host, ModuleDefinition moduleDef) => new(typeRef, host, moduleDef);

        public static T Simulate<T>(this TypeReference typeRef, ModuleDefinition moduleDef) where T : TypeSimulation => Simulate<T>(typeRef, null, moduleDef);

        public static T Simulate<T>(this TypeReference typeRef, IHost? host, ModuleDefinition moduleDef) where T : TypeSimulation
        {
            var ctor = _Cache.GetOrAdd(typeof(T), t =>
            {
                var ctorInfo = t.GetConstructor([typeof(TypeReference), typeof(IHost), typeof(ModuleDefinition)]);
                return (tr, h, md) => ctorInfo.Invoke([tr, h, md]);
            });

            return (T)ctor(typeRef, host, moduleDef);
        }

        public static T ReplaceGenerics<T>(this T simulation, Dictionary<string, GenericParameter> genericMap) where T : TypeSimulation
        {
            simulation.Ref = simulation.Ref.ReplaceGenericArgs(genericMap);

            return simulation;
        }

        public static T ReplaceGenericsWith<T>(this T simulation, TypeSimulation baseSimulation) where T : TypeSimulation
        {
            if (baseSimulation.Ref is not GenericInstanceType git) return simulation;

            return simulation.ReplaceGenerics(git.GenericArguments.Where(x => x is GenericParameter).ToDictionary(x => x.Name, x => (GenericParameter)x));
        }

        public static T SetGenerics<T>(this T simulation, TypeReference[] generics) where T : TypeSimulation
        {
            if (simulation.Ref is not GenericInstanceType git) throw new RougamoException($"Cannot set generic parameters for {simulation.Ref}, it is not a generic type.");
            if (git.GenericArguments.Count != generics.Length) throw new RougamoException($"Cannot set generic parameters for {simulation.Ref}, given generic parameters [{string.Join(",", generics.Select(x => x.ToString()))}]");

            git = new GenericInstanceType(simulation.Ref.GetElementType());
            git.GenericArguments.Add(generics);
            simulation.Ref = git;

            return simulation;
        }
    }
}
