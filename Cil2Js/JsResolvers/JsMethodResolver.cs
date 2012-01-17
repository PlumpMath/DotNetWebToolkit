﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DotNetWebToolkit.Cil2Js.Ast;
using Mono.Cecil;
using DotNetWebToolkit.Cil2Js.Utils;
using System.Reflection;

namespace DotNetWebToolkit.Cil2Js.JsResolvers {
    public static partial class JsResolver {

        private static Dictionary<M, Func<Ctx, List<TypeReference>, Stmt>> methodMap = new Dictionary<M,Func<Ctx,List<TypeReference>,Stmt>>(M.ValueEqComparer) {
            { M.Def(TVoid, "System.IntPtr..ctor", TInt32), ResolverSystem.IntPtrCtor },
            { M.Def(TVoid, "System.Array.Clear", TArray, TInt32, TInt32), ResolverArray.Clear },
            { M.Def(TType, "System.Object.GetType"), ResolverSystem.Object_GetType },
            { M.Def(TType, "System.RuntimeType.get_BaseType"), ResolverType.get_BaseType },
        };

        public static Stmt ResolveMethod(Ctx ctx, List<TypeReference> newTypesSeen) {
            // Explicit mapping
            var m = new M(ctx.MRef);
            var fn = methodMap.ValueOrDefault(m);
            if (fn != null) {
                var resolved = fn(ctx, newTypesSeen);
                return resolved;
            }
            // Attribute for internal function
            var jsAttr = ctx.MDef.GetCustomAttribute<JsAttribute>();
            if (jsAttr != null) {
                var implType = (TypeDefinition)jsAttr.ConstructorArguments[0].Value;
                var t = typeof(JsResolver).Module.ResolveType(implType.MetadataToken.ToInt32());
                var impl = (IJsImpl)Activator.CreateInstance(t);
                var stmt = impl.GetImpl(ctx);
                return stmt;
            }
            return null;
        }

    }
}
