﻿using System.Collections.Generic;

namespace Rougamo.Fody.Signature.Patterns
{
    public class GenericNamePattern
    {
        public GenericNamePattern(string name, ITypePatterns genericPatterns)
        {
            Name = name;
            GenericPatterns = genericPatterns;
        }

        public string Name { get; }

        public ITypePatterns GenericPatterns { get; }

        public void ExtractGenerics(List<GenericParameterTypePattern> list)
        {
            if (GenericPatterns is TypePatterns patterns)
            {
                foreach (var pattern in patterns.Patterns)
                {
                    if (pattern is GenericParameterTypePattern gptp) list.Add(gptp);
                }
            }
        }

        public bool IsMatch(GenericSignature method)
        {
            return Name == method.Name && GenericPatterns.IsMatch(method.Generics);
        }
    }
}