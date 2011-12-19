﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace Cil2Js.Ast {
    public class ExprThis : Expr {

        public ExprThis(Ctx ctx, TypeReference type)
            : base(ctx) {
            this.type = type;
        }

        private TypeReference type;

        public override Expr.NodeType ExprType {
            get { return NodeType.This; }
        }

        public override Mono.Cecil.TypeReference Type {
            get { return this.type; }
        }

        public override string ToString() {
            return "this";
        }

    }
}
