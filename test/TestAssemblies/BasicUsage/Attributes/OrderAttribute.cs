﻿using Rougamo;
using Rougamo.Context;
using Rougamo.Flexibility;

namespace BasicUsage.Attributes
{
    public class OrderAttribute : MoAttribute, IFlexibleOrderable
    {
        public virtual double Order { get; set; }

        public override void OnEntry(MethodContext context)
        {
            var useCase = (OrderUseCase)context.Target;
            useCase.Executions.Add(GetType().Name);
        }
    }

    public class OrderM1Attribute : OrderAttribute
    {
        public override double Order => -1;
    }

    public class Order1Attribute : OrderAttribute
    {
        public override double Order { get; set; } = 1;
    }
}
