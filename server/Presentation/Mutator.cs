﻿using Aggregates;
using NServiceBus;
using System.Collections.Generic;
using Aggregates.Contracts;
using Infrastructure.Commands;
using System;

namespace Example
{
    public class Mutator : IMutate
    {
        public IMutating MutateIncoming(IMutating mutating)
        {
            return mutating;
        }
        public IMutating MutateOutgoing(IMutating mutating)
        {
            if (!(mutating.Message is StampedCommand)) return mutating;

            (mutating.Message as StampedCommand).Stamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            return mutating;
        }
    }
}
