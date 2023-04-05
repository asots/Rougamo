﻿using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using Rougamo.Fody.Enhances;
using System;
using System.Collections.Generic;
using System.Linq;
using static Mono.Cecil.Cil.Instruction;

namespace Rougamo.Fody
{
    partial class ModuleWeaver
    {
        private void WeaveMos()
        {
            foreach (var rouType in _rouTypes)
            {
                foreach (var rouMethod in rouType.Methods)
                {
                    if (rouMethod.IsIterator)
                    {
                        IteratorMethodWeave(rouMethod);
                    }
                    else if (rouMethod.IsAsyncIterator)
                    {
                        AiteratorMethodWeave(rouMethod);
                    }
                    else if (rouMethod.IsAsyncTaskOrValueTask)
                    {
                        AsyncTaskMethodWeave(rouMethod);
                    }
                    else
                    {
                        SyncMethodWeave(rouMethod);
                    }
                }
            }
        }

        #region LoadMosOnStack

        private IList<Instruction> CreateTempMoArray(VariableDefinition[] moVariables)
        {
            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldc_I4, moVariables.Length),
                Create(OpCodes.Newarr, _typeIMoRef)
            };
            for (var i = 0; i < moVariables.Length; i++)
            {
                instructions.Add(Create(OpCodes.Dup));
                instructions.Add(Create(OpCodes.Ldc_I4, i));
                instructions.Add(Create(OpCodes.Ldloc, moVariables[i]));
                instructions.Add(Create(OpCodes.Stelem_Ref));
            }

            return instructions;
        }

        private IList<Instruction> CreateTempMoArray(VariableDefinition stateMachineVariable, FieldReference[] moFields)
        {
            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldc_I4, moFields.Length),
                Create(OpCodes.Newarr, _typeIMoRef)
            };
            for (var i = 0; i < moFields.Length; i++)
            {
                instructions.Add(Create(OpCodes.Dup));
                instructions.Add(Create(OpCodes.Ldc_I4, i));
                instructions.Add(stateMachineVariable.LdlocOrA());
                instructions.Add(Create(OpCodes.Ldfld, moFields[i]));
                instructions.Add(Create(OpCodes.Stelem_Ref));
            }

            return instructions;
        }

        private List<Instruction> InitMoArray(Mo[] mos)
        {
            var instructions = new List<Instruction>
            {
                Create(OpCodes.Ldc_I4, mos.Length),
                Create(OpCodes.Newarr, _typeIMoRef)
            };
            var i = 0;
            foreach (var mo in mos)
            {
                instructions.Add(Create(OpCodes.Dup));
                instructions.Add(Create(OpCodes.Ldc_I4, i));
                instructions.AddRange(InitMo(mo));
                instructions.Add(Create(OpCodes.Stelem_Ref));
                i++;
            }

            return instructions;
        }

        private IList<Instruction> InitMo(Mo mo)
        {
            if (mo.Attribute != null)
            {
                var instructions = new List<Instruction>();
                instructions.AddRange(LoadAttributeArgumentIns(mo.Attribute.ConstructorArguments));
                instructions.Add(Create(OpCodes.Newobj, Import(mo.Attribute.Constructor)));
                if (mo.Attribute.HasProperties)
                {
                    instructions.AddRange(LoadAttributePropertyDup(mo.Attribute.AttributeType.Resolve(), mo.Attribute.Properties));
                }

                return instructions;
            }
            
            return new Instruction[] { Create(OpCodes.Newobj, Import(mo.TypeDef!.GetZeroArgsCtor())) };
        }

        private Collection<Instruction> LoadAttributeArgumentIns(Collection<CustomAttributeArgument> arguments)
        {
            var instructions = new Collection<Instruction>();
            foreach (var arg in arguments)
            {
                instructions.Add(LoadValueOnStack(arg.Type, arg.Value));
            }
            return instructions;
        }

        private Collection<Instruction> LoadAttributePropertyDup(TypeDefinition attrTypeDef, Collection<CustomAttributeNamedArgument> properties)
        {
            var ins = new Collection<Instruction>();
            for (var i = 0; i < properties.Count; i++)
            {
                ins.Add(Create(OpCodes.Dup));
                ins.Add(LoadValueOnStack(properties[i].Argument.Type, properties[i].Argument.Value));
                ins.Add(Create(OpCodes.Callvirt, attrTypeDef.RecursionImportPropertySet(ModuleDefinition, properties[i].Name)));
            }

            return ins;
        }

        #endregion LoadMosOnStack

        private List<Instruction> InitMethodContext(MethodDefinition methodDef, bool isAsync, bool isIterator, VariableDefinition? moArrayVariable, VariableDefinition? stateMachineVariable, FieldReference? mosFieldRef)
        {
            var instructions = new List<Instruction>();

            var isAsyncCode = isAsync ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            var isIteratorCode = isIterator ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
            var mosNonEntryFIFO = _config.ReverseCallNonEntry ? OpCodes.Ldc_I4_0 : OpCodes.Ldc_I4_1;
            instructions.Add(LoadThisOnStack(methodDef));
            instructions.AddRange(LoadDeclaringTypeOnStack(methodDef));
            instructions.AddRange(LoadMethodBaseOnStack(methodDef));
            instructions.Add(Create(isAsyncCode));
            instructions.Add(Create(isIteratorCode));
            instructions.Add(Create(mosNonEntryFIFO));
            if (mosFieldRef == null)
            {
                instructions.Add(Create(OpCodes.Ldloc, moArrayVariable));
            }
            else
            {
                instructions.Add(stateMachineVariable!.LdlocOrA());
                instructions.Add(Create(OpCodes.Ldfld, mosFieldRef));
            }
            instructions.AddRange(LoadMethodArgumentsOnStack(methodDef));
            instructions.Add(Create(OpCodes.Newobj, _methodMethodContextCtorRef));

            return instructions;
        }

        private List<Instruction> ExecuteMoMethod(string methodName, MethodDefinition methodDef, Mo[] mos, Instruction loopExit, VariableDefinition? mosVariable, VariableDefinition? contextVariable, FieldReference? mosField, FieldReference? contextField, bool reverseCall)
        {
            var instructions = new List<Instruction>();
            var flagVariable = methodDef.Body.CreateVariable(_typeIntRef);

            Instruction loopFirst;
            Instruction bodyFirst = Create(OpCodes.Nop);
            var updateFlagStart = Create(OpCodes.Ldloc, flagVariable);

            if (reverseCall)
            {
                instructions.Add(Create(OpCodes.Ldc_I4, mos.Length));
                instructions.Add(Create(OpCodes.Ldc_I4_1));
                instructions.Add(Create(OpCodes.Sub));
                instructions.Add(Create(OpCodes.Stloc, flagVariable));
                loopFirst = Create(OpCodes.Ldloc, flagVariable);
                instructions.Add(loopFirst);
                instructions.Add(Create(OpCodes.Ldc_I4_0));
                instructions.Add(Create(OpCodes.Clt));
                instructions.Add(Create(OpCodes.Brtrue, loopExit));
            }
            else
            {
                instructions.Add(Create(OpCodes.Ldc_I4_0));
                instructions.Add(Create(OpCodes.Stloc, flagVariable));
                loopFirst = Create(OpCodes.Ldloc, flagVariable);
                instructions.Add(loopFirst);
                instructions.Add(Create(OpCodes.Ldc_I4, mos.Length));
                instructions.Add(Create(OpCodes.Clt));
                instructions.Add(Create(OpCodes.Brfalse_S, loopExit));
            }

            var matchCount = 0;
            var matches = new bool[mos.Length];
            for (var i = 0; i < mos.Length; i++)
            {
                var isMatch = ((Feature)Enum.Parse(typeof(Feature), methodName)).IsMatch(mos[i].Features);
                if (isMatch) matchCount++;
                matches[i] = isMatch;
            }
            var byMatched = mos.Length - matchCount > matchCount;
            if (byMatched)
            {
                for (int i = 0; i < mos.Length; i++)
                {
                    if (matches[i])
                    {
                        instructions.Add(Create(OpCodes.Ldloc, flagVariable));
                        instructions.Add(Create(OpCodes.Ldc_I4, i));
                        instructions.Add(Create(OpCodes.Beq, bodyFirst));
                    }
                }
                instructions.Add(Create(OpCodes.Br, updateFlagStart));
            }
            else
            {
                for (int i = 0; i < mos.Length; i++)
                {
                    if (!matches[i])
                    {
                        instructions.Add(Create(OpCodes.Ldloc, flagVariable));
                        instructions.Add(Create(OpCodes.Ldc_I4, i));
                        instructions.Add(Create(OpCodes.Beq, updateFlagStart));
                    }
                }
            }

            if (mosVariable == null)
            {
                bodyFirst.OpCode = OpCodes.Ldarg_0;
                instructions.Add(bodyFirst);
                instructions.Add(Create(OpCodes.Ldfld, mosField));
            }
            else
            {
                bodyFirst.OpCode = OpCodes.Ldloc;
                bodyFirst.Operand = mosVariable;
                instructions.Add(bodyFirst);
            }
            instructions.Add(Create(OpCodes.Ldloc, flagVariable));
            instructions.Add(Create(OpCodes.Ldelem_Ref));
            if (contextVariable == null)
            {
                instructions.Add(Create(OpCodes.Ldarg_0));
                instructions.Add(Create(OpCodes.Ldfld, contextField));
            }
            else
            {
                instructions.Add(Create(OpCodes.Ldloc, contextVariable));
            }
            instructions.Add(Create(OpCodes.Callvirt, _methodIMosRef[methodName]));
            instructions.Add(updateFlagStart);
            instructions.Add(Create(OpCodes.Ldc_I4_1));
            if (reverseCall)
            {
                instructions.Add(Create(OpCodes.Sub));
            }
            else
            {
                instructions.Add(Create(OpCodes.Add));
            }
            instructions.Add(Create(OpCodes.Stloc, flagVariable));
            instructions.Add(Create(OpCodes.Br_S, loopFirst));

            return instructions;
        }

        private List<Instruction> ExecuteMoMethod(string methodName, Mo[] mos, VariableDefinition[]? moVariables, VariableDefinition? contextVariable, FieldReference[]? moFields, FieldReference? contextField, bool reverseCall)
        {
            var instructions = new List<Instruction>();

            for (int i = 0; i < mos.Length; i++)
            {
                var j = reverseCall ? mos.Length - i - 1 : i;

                if (!((Feature)Enum.Parse(typeof(Feature), methodName)).IsMatch(mos[j].Features)) continue;

                if (moVariables == null)
                {
                    instructions.Add(Create(OpCodes.Ldarg_0));
                    instructions.Add(Create(OpCodes.Ldfld, moFields![j]));
                    instructions.Add(Create(OpCodes.Ldarg_0));
                    instructions.Add(Create(OpCodes.Ldfld, contextField));
                    instructions.Add(Create(OpCodes.Callvirt, _methodIMosRef[methodName]));
                }
                else
                {
                    instructions.Add(Create(OpCodes.Ldloc, moVariables![j]));
                    instructions.Add(Create(OpCodes.Ldloc, contextVariable));
                    instructions.Add(Create(OpCodes.Callvirt, _methodIMosRef[methodName]));
                }
            }

            return instructions;
        }

        private ExceptionHandler GetOuterExceptionHandler(MethodDefinition methodDef)
        {
            ExceptionHandler? exceptionHandler = null;
            int offset = methodDef.Body.Instructions.First().Offset;
            foreach (var handler in methodDef.Body.ExceptionHandlers)
            {
                if (handler.HandlerType != ExceptionHandlerType.Catch) continue;
                if (handler.TryEnd.Offset > offset)
                {
                    exceptionHandler = handler;
                    offset = handler.TryEnd.Offset;
                }
            }
            return exceptionHandler ?? throw new RougamoException($"[{methodDef.FullName}] can not find outer exception handler");
        }

        private void SetTryCatchFinally(int features, MethodDefinition methodDef, ITryCatchFinallyAnchors anchors)
        {
            if ((features & (int)(Feature.OnException | Feature.OnSuccess | Feature.OnExit)) != 0)
            {
                var exceptionHandler = new ExceptionHandler(ExceptionHandlerType.Catch)
                {
                    CatchType = _typeExceptionRef,
                    TryStart = anchors.TryStart,
                    TryEnd = anchors.CatchStart,
                    HandlerStart = anchors.CatchStart,
                    HandlerEnd = anchors.FinallyStart
                };
                methodDef.Body.ExceptionHandlers.Add(exceptionHandler);
            }

            if ((features & (int)(Feature.OnSuccess | Feature.OnExit)) != 0)
            {
                var finallyHandler = new ExceptionHandler(ExceptionHandlerType.Finally)
                {
                    TryStart = anchors.TryStart,
                    TryEnd = anchors.FinallyStart,
                    HandlerStart = anchors.FinallyStart,
                    HandlerEnd = anchors.FinallyEnd
                };
                methodDef.Body.ExceptionHandlers.Add(finallyHandler);
            }
        }
    }
}
