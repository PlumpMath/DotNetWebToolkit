﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace DotNetWebToolkit.Cil2Js.Ast {

    public enum CodeType {
        Expression,
        Statement,
    }

    public interface ICode : ICloneable {

        CodeType CodeType { get; }
        Ctx Ctx { get; }

    }
}
