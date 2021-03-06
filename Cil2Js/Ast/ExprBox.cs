﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace DotNetWebToolkit.Cil2Js.Ast {
    public class ExprBox : Expr {

        public ExprBox(Ctx ctx, Expr expr, TypeReference type)
            : base(ctx) {
            this.Expr = expr;
            this.type = type;
            // Note that 'type' may not be the same as Expr.type, but it will always be assignment compatible
        }

        public Expr Expr { get; private set; }
        private TypeReference type;

        public override Expr.NodeType ExprType {
            get { return NodeType.Box; }
        }

        public override TypeReference Type {
            get { return this.type; }
        }

    }
}
