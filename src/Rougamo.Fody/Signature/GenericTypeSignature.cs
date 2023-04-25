﻿namespace Rougamo.Fody.Signature
{
    /// <summary>
    /// Generic type
    /// </summary>
    public class GenericTypeSignature : TypeSignature
    {
        public GenericTypeSignature(string name, TypeSignature[] parameters) : base(name, TypeCategory.Generic)
        {
            GenericParameters = parameters;
        }

        /// <summary>
        /// Generic parameters
        /// </summary>
        public TypeSignature[] GenericParameters { get; set; }
    }
}