﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aggregates
{
    public interface IHandleComputed<TCompute, TResponse> where TCompute : IComputed<TResponse>
    {
        Task<TResponse> Handle(TCompute compute);
    }
}
