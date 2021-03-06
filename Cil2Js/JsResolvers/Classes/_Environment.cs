﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DotNetWebToolkit.Cil2Js.Ast;
using DotNetWebToolkit.Cil2Js.Output;

namespace DotNetWebToolkit.Cil2Js.JsResolvers.Classes {
    class _Environment {

        [Js]
        public static Expr GetResourceFromDefault(ICall call) {
            var ctx = call.Ctx;
            var pre = new ExprLiteral(ctx, "GetResourceFromDefault_", ctx.String);
            var expr = new ExprBinary(ctx, BinaryOp.Add, ctx.String, pre, call.Args.First());
            return expr;
        }

        [Js]
        public static Expr GetRuntimeResourceString(ICall call) {
            var ctx = call.Ctx;
            var expr = new ExprLiteral(ctx, "<GetRuntimeResourceString>", ctx.String);
            return expr;
        }

        [Js]
        public static Expr GetResourceString(ICall call) {
            var ctx = call.Ctx;
            var expr = new ExprLiteral(ctx, "<GetRuntimeResourceString>", ctx.String);
            return expr;
        }

        [Js]
        public static Expr get_CurrentManagedThreadId(ICall call) {
            return call.Ctx.Literal(0);
        }

        [Js]
        public static Expr get_NewLine(ICall call) {
            return call.Ctx.Literal("\n");
        }

        [Js]
        public static Expr GetStackTrace(ICall call) {
            return call.Ctx.Literal((string)null);
        }

        [Js]
        public static Expr get_TickCount(ICall call) {
            var ctx = call.Ctx;
            return new ExprJsExplicit(ctx, "(~~(new Date))", ctx.Int32);
        }

    }
}
