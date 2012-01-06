﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Cil2Js.Analysis;
using Cil2Js.Ast;
using Cil2Js.JsResolvers;
using Cil2Js.Utils;
using System.Reflection;

namespace Cil2Js.Output {
    public class Js {

        class ExprVarCluster : ExprVar {

            public ExprVarCluster(IEnumerable<ExprVar> vars)
                : base(null) {
                this.Vars = vars;
            }

            public IEnumerable<ExprVar> Vars { get; private set; }

            public override Expr.NodeType ExprType {
                get { throw new NotImplementedException(); }
            }

            public override TypeReference Type {
                get { throw new NotImplementedException(); }
            }

        }

        public static string CreateFrom(MethodReference method, bool verbose = false) {
            return CreateFrom(new[] { method }, verbose);
        }

        public static string CreateFrom(IEnumerable<MethodReference> rootMethods, bool verbose = false) {
            var todo = new Stack<MethodReference>();
            foreach (var method in rootMethods) {
                todo.Push(method);
            }
            // Each method, with the count of how often it is referenced.
            var methodsSeen = new Dictionary<MethodReference, int>(rootMethods.ToDictionary(x => x, x => 1), TypeExtensions.MethodRefEqComparerInstance);
            // Each type, with the count of how often it is referenced directly (newobj only at the moment).
            var typesSeen = new Dictionary<TypeReference, int>(TypeExtensions.TypeRefEqComparerInstance);
            var methodAsts = new Dictionary<MethodReference, ICode>(TypeExtensions.MethodRefEqComparerInstance);
            var nonVirtualMethodCallCounts = new Dictionary<MethodReference, int>(TypeExtensions.MethodRefEqComparerInstance);
            foreach (var rootMethod in rootMethods) {
                nonVirtualMethodCallCounts.Add(rootMethod, 1);
            }
            // Each field, with the count of how often it is referenced.
            var fieldAccesses = new Dictionary<FieldReference, int>(TypeExtensions.FieldReqEqComparerInstance);

            while (todo.Any()) {
                var mRef = todo.Pop();
                var mDef = mRef.Resolve();
                var tRef = mRef.DeclaringType;
                var tDef = tRef.Resolve();
                if (mDef.IsAbstract) {
                    throw new InvalidOperationException("Cannot transcode an abstract method");
                }
                if (mDef.IsInternalCall) {
                    throw new InvalidOperationException("Cannot transcode an internal call");
                }
                if (mDef.IsExternal()) {
                    throw new InvalidOperationException("Cannot transcode an external method");
                }
                if (!mDef.HasBody) {
                    throw new InvalidOperationException("Cannot transcode method without body");
                }
                if (!typesSeen.ContainsKey(tRef)) {
                    typesSeen.Add(tRef, 0);
                }

                var ast = Transcoder.ToAst(mRef, tRef, verbose);
                var ctx = ast.Ctx;
                

                for (int i = 0; ; i++) {
                    var astOrg = ast;
                    var vResolveCalls = new VisitorResolveCalls(JsCallResolver.Resolve);
                    ast = vResolveCalls.Visit(ast);
                    if (ast == astOrg) {
                        break;
                    }
                    if (i > 10) {
                        // After 10 iterations even the most complex method should be sorted out
                        throw new InvalidOperationException("Error: Stuck in loop trying to resolve AST");
                    }
                }

                if (mDef.IsConstructor && !mDef.IsStatic) {
                    // Instance constructor; add instance field initialisation and final return of 'this'
                    var initStmts = tDef.Fields.Where(x => !x.IsStatic)
                        .Select(x => new StmtAssignment(ctx,
                            new ExprFieldAccess(ctx, ctx.This, x),
                            new ExprDefaultValue(ctx, x.FieldType)))
                        .ToArray();
                    var returnStmt = new StmtReturn(ctx, ctx.This);
                    ast = new StmtBlock(ctx, initStmts.Concat((Stmt)ast).Concat(returnStmt));
                }

                var cctors = VisitorFindStaticConstructors.V(ast).Where(x => !TypeExtensions.MethodRefEqComparerInstance.Equals(x, mRef)).ToArray();
                if (cctors.Any()) {
                    // All methods that access static fields or methods must call the static constructor at the very
                    // start of the method. Except the static construtor itself, which must not recurse into itself.
                    var cctorCalls = cctors
                        .Select(x => new StmtWrapExpr(ctx, new ExprCall(ctx, x, null, Enumerable.Empty<Expr>(), false))).ToArray();
                    ast = new StmtBlock(ctx, cctorCalls.Concat((Stmt)ast));
                }

                if (mDef.IsConstructor && mDef.IsStatic) {
                    // At the end of the static constructor, it rewrites itself as an empty function, so it is only called once.
                    var rewrite = new StmtAssignment(ctx, new ExprJsVarMethodReference(ctx, mRef), new ExprJsEmptyFunction(ctx));
                    ast = new StmtBlock(ctx, (Stmt)ast, rewrite);
                }

                methodAsts.Add(mRef, ast);

                var fieldRefs = VisitorFindFieldAccesses.V(ast);
                foreach (var fieldRef in fieldRefs) {
                    fieldAccesses[fieldRef] = fieldAccesses.ValueOrDefault(fieldRef) + 1;
                }

                var calls = VisitorFindCalls.V(ast);
                foreach (var call in calls.Where(x => x.ExprType == Expr.NodeType.NewObj)) {
                    // Add reference to each type constructed (direct access to type variable)
                    typesSeen[call.Type] = typesSeen.ValueOrDefault(call.Type, 0) + 1;
                }
                foreach (var call in calls) {
                    if (methodsSeen.ContainsKey(call.CallMethod)) {
                        methodsSeen[call.CallMethod]++;
                    } else {
                        methodsSeen.Add(call.CallMethod, 1);
                        todo.Push(call.CallMethod);
                    }
                }
            }

            // Locally name all instance fields; base type names must not be re-used in derived types
            var instanceFields = fieldAccesses.Where(x => !x.Key.Resolve().IsStatic).ToArray();
            // TODO: This names all instance fields globally. IT CAN BE DONE BETTER.
            var instanceFieldNameGen = new NameGenerator();
            var instanceFieldNames = instanceFields.OrderByDescending(x => x.Value).Select(x => new { f = x.Key, name = instanceFieldNameGen.GetNewName() }).ToArray();
            // Prepare list of static fields for global naming
            var staticFields = fieldAccesses.Where(x => x.Key.Resolve().IsStatic).ToArray();

            // Prepare local variables for global naming.
            // All locals in all methods are sorted by usage count, then all methods usage counts are combined
            var clusters = methodAsts.Values.SelectMany(x => VisitorPhiClusters.V(x).Select(y => new ExprVarCluster(y))).ToArray();
            var varToCluster = clusters.SelectMany(x => x.Vars.Select(y => new { cluster = x, var = y })).ToDictionary(x => x.var, x => x.cluster);
            var varsWithCount = methodAsts.Values.Select(x => {
                var methodVars = VisitorGetVars.V(x);
                // Parameters need one extra count, as they appear in the method declaration
                methodVars = methodVars.Concat(methodVars.Where(y => y.ExprType == Expr.NodeType.VarParameter).Distinct());
                var ret = methodVars.Select(y => varToCluster.ValueOrDefault(y) ?? y)
                    .GroupBy(y => y)
                    .Select(y => new { var = y.Key, count = y.Count() })
                    .OrderByDescending(y => y.count)
                    .ToArray();
                return ret;
            }).ToArray();
            var localVarCounts = new Dictionary<int, int>();
            foreach (var x in varsWithCount) {
                for (int i = 0; i < x.Length; i++) {
                    localVarCounts[i] = localVarCounts.ValueOrDefault(i) + x[i].count;
                }
            }

            // Globally name all items that require names
            var needNaming =
                localVarCounts.Select(x => new { item = (object)x.Key, count = x.Value })
                .Concat(methodsSeen.Select(x => new { item = (object)x.Key, count = x.Value }))
                .Concat(staticFields.Select(x => new { item = (object)x.Key, count = x.Value }))
                .Concat(typesSeen.Select(x => new { item = (object)x.Key, count = x.Value }))
                .Where(x => x.count > 0)
                .OrderByDescending(x => x.count)
                .ToArray();
            var nameGen = new NameGenerator();
            var globalNames = needNaming.ToDictionary(x => x.item, x => nameGen.GetNewName());

            // Create map of all local variables to their names
            var localVarNames = varsWithCount.Select(x => x.Select((y, i) => new { y.var, name = globalNames[i] }))
                .SelectMany(x => x)
                .SelectMany(x => {
                    var varCluster = x.var as ExprVarCluster;
                    if (varCluster != null) {
                        return varCluster.Vars.Select(y => new { var = y, name = x.name });
                    } else {
                        return new[] { x };
                    }
                })
                .ToDictionary(x => x.var, x => x.name);

            // Create map of all method names
            var methodNames = methodsSeen.Keys.ToDictionary(x => x, x => globalNames[x], TypeExtensions.MethodRefEqComparerInstance);
            methodNames[rootMethods.First()] = "main"; // HACK

            // Create list of all static field names
            var staticFieldNames = staticFields.Select(x => new { f = x.Key, name = globalNames[x.Key] });
            // Create map of all fields
            var fieldNames = instanceFieldNames.Concat(staticFieldNames).ToDictionary(x => x.f, x => x.name);

            // Create map of type names
            var typeNames = typesSeen
                .Where(x => x.Value > 0)
                .ToDictionary(x => x.Key, x => globalNames[x.Key], TypeExtensions.TypeRefEqComparerInstance);

            var resolver = new JsMethod.Resolver {
                LocalVarNames = localVarNames,
                MethodNames = methodNames,
                FieldNames = fieldNames,
                TypeNames = typeNames,
            };

            var js = new StringBuilder();

            // Construct methods
            foreach (var methodInfo in methodAsts) {
                var mRef = methodInfo.Key;
                var ast = methodInfo.Value;
                var mJs = JsMethod.Create(mRef, resolver, ast);
                js.AppendLine(mJs);
            }

            // Construct static fields
            foreach (var field in staticFields.Select(x=>x.Key)) {
                js.AppendFormat("var {0} = {1};", fieldNames[field], DefaultValuer.Get(field.FieldType));
            }

            // Construct type data
            foreach (var type in typesSeen.Where(x => x.Value > 0).Select(x => x.Key)) {
                js.AppendFormat("var {0}={{}};", typeNames[type]);
                js.AppendLine();
            }

            var jsStr = js.ToString();
            return jsStr;
            /*
            var todo = new Queue<MethodReference>();
            foreach (var method in rootMethods) {
                todo.Enqueue(method);
            }
            var seen = new HashSet<MethodReference>(rootMethods, TypeExtensions.MethodRefEqComparerInstance);
            var typesSeen = new HashSet<TypeReference>(TypeExtensions.TypeRefEqComparerInstance);
            var asts = new Dictionary<MethodReference, ICode>(TypeExtensions.MethodRefEqComparerInstance);
            var callResolvers = new Dictionary<ICall, JsResolved>();
            var exports = new List<Tuple<MethodDefinition, string>>();
            var fields = new Dictionary<FieldDefinition, int>();
            // Key is the base-most method (possibly abstract), hashset contains base and all overrides that are not abstract
            var virtualCalls = new Dictionary<MethodDefinition, HashSet<MethodDefinition>>();
            var cctorCalls = new Dictionary<MethodDefinition, IEnumerable<MethodDefinition>>();
            // Which interface methods are referenced; key is interface, hashset contains all referenced methods on key interface
            var interfaceCalls = new Dictionary<TypeDefinition, HashSet<MethodDefinition>>();

            while (todo.Any()) {
                var method = todo.Dequeue();
                var methodDef = method.Resolve();
                if (methodDef.IsAbstract) {
                    continue;
                }
                if (methodDef.IsInternalCall) {
                    throw new ArgumentException("Cannot transcode an internal method");
                }

                typesSeen.Add(method.DeclaringType);
                if (methodDef.IsVirtual && !methodDef.IsAbstract) {
                    var baseMethod = method.GetBasemostMethodInTypeHierarchy();
                    virtualCalls.ValueOrDefault(baseMethod, () => new HashSet<MethodDefinition>(), true).Add(method);
                }
                // Is it exported?
                var export = method.CustomAttributes.FirstOrDefault(x => x.AttributeType.FullName == "Cil2Js.Attributes.ExportAttribute");
                if (export != null) {
                    var exportName = (string)export.ConstructorArguments.FirstOrDefault().Value ?? method.Name;
                    exports.Add(Tuple.Create(method, exportName));
                }
                // Create AST
                var ast = Transcoder.ToAst(method, verbose);
                var methodsCalled = new List<MethodDefinition>();
                for (int i = 0; ; i++) {
                    var astOrg = ast;
                    // Resolve calls
                    var vResolveCalls = new VisitorResolveCalls(call => JsCallResolver.Resolve(call));
                    ast = vResolveCalls.Visit(ast);
                    // Resolve field accesses
                    var vResolveFieldAccess = new VisitorResolveFieldAccess(field => null);
                    ast = vResolveFieldAccess.Visit(ast);
                    // Check if more rewrites may be required
                    if (astOrg == ast) {
                        break;
                    }
                    if (i > 10) {
                        // After 10 iterations even the most complex method should be sorted out
                        throw new InvalidOperationException("Error: Stuck in loop trying to resolve AST");
                    }
                }
                // Examine all called methods
                var calls = VisitorFindCalls.V(ast);
                foreach (var callInfo in calls) {
                    var call = callInfo.Item1;
                    var callMethod = call.CallMethod.Resolve();
                    if (callMethod.DeclaringType.IsInterface) {
                        interfaceCalls.ValueOrDefault(callMethod.DeclaringType, () => new HashSet<MethodDefinition>(), true)
                            .Add(callMethod);
                    } else {
                        var isVirtual = callInfo.Item2;
                        var resolved = JsCallResolver.Resolve(call);
                        if (resolved == null) {
                            if (isVirtual && callMethod.IsVirtual) {
                                var baseMethod = callMethod.GetBasemostMethodInTypeHierarchy();
                                virtualCalls.ValueOrDefault(baseMethod, () => new HashSet<MethodDefinition>(), true);
                            }
                            methodsCalled.Add(callMethod);
                        } else {
                            switch (resolved.Type) {
                            case JsResolvedType.Method:
                            case JsResolvedType.Property:
                                callResolvers.Add(call, resolved);
                                break;
                            default:
                                throw new NotImplementedException("Cannot handle: " + resolved.Type);
                            }
                        }
                    }
                }
                // Record all field accesses
                var fieldAccesses = VisitorFindFieldAccesses.V(ast);
                foreach (var fieldAccess in fieldAccesses) {
                    fields[fieldAccess] = fields.ValueOrDefault(fieldAccess, () => 0, true) + 1;
                }
                // Find all static constructors that need calling/converting
                var staticConstructors = VisitorFindStaticConstructors.V(ast);
                methodsCalled.AddRange(staticConstructors);
                cctorCalls.Add(method, staticConstructors);
                // Record this converted AST
                asts.Add(method, ast);

                // Queue any methods that now need converting to JS
                Action<IEnumerable<MethodDefinition>> addToTodo = toAdd => {
                    foreach (var call in toAdd) {
                        if (seen.Add(call)) {
                            todo.Enqueue(call);
                        }
                    }
                };

                addToTodo(methodsCalled);

                // When run out of methods, add any extra methods needed from virtual calls and interface calls
                if (!todo.Any()) {
                    // Virtual calls
                    var vToAdd =
                        from type in typesSeen
                        from m in type.Methods
                        where !m.IsAbstract && m.IsVirtual
                        let mBase = m.GetBasemostMethodInTypeHierarchy()
                        where virtualCalls.ContainsKey(mBase)
                        select m;
                    var vToAddArray = vToAdd.ToArray();
                    addToTodo(vToAddArray);
                    // Interface calls
                    var iToAdd =
                        from type in typesSeen
                        from iFaceRef in type.GetAllInterfaces()
                        let iFace = iFaceRef.Resolve()
                        let iFaceMethods = interfaceCalls.ValueOrDefault(iFace)
                        where iFaceMethods != null
                        from iFaceMethod in iFaceMethods
                        let m = type.GetInterfaceMethod(iFaceMethod)
                        where !m.IsAbstract
                        select m;
                    var iToAddArray = iToAdd.ToArray();
                    addToTodo(iToAddArray);
                }
            }

            // Name all methods
            // TODO: Improve this
            var methodNamer = new NameGenerator("$");
            var methodNames = asts.Keys.ToDictionary(x => x, x => methodNamer.GetNewName());
            methodNames[rootMethods.First()] = "main"; // TODO: sort this out properly
            // Name all fields
            // TODO: This generates bad quality names as no names are shared over different types, to improve...
            var fieldNamer = new NameGenerator();
            var staticFieldNamer = new NameGenerator("$_");
            var fieldNames = fields.Keys.ToDictionary(x => x, x => {
                if (x.IsStatic) {
                    return staticFieldNamer.GetNewName();
                } else {
                    return fieldNamer.GetNewName();
                }
            });
            // Create vTables for virtual methods
            var vMethodsByType = virtualCalls.SelectMany(x => x.Value.Concat(x.Key).Distinct()).ToLookup(x => x.DeclaringType);
            var allTypesInBaseOrder = typesSeen.OrderByBaseFirst().ToArray();
            var vTable = new Dictionary<TypeDefinition, MethodDefinition[]>();
            var virtualCalls2 = new Dictionary<MethodDefinition, int>();
            foreach (var vType in allTypesInBaseOrder) {
                MethodDefinition[] vTableBase = null;
                var vTypeBase = vType;
                for (; ; ) {
                    vTypeBase = vTypeBase.GetBaseType();
                    if (vTypeBase == null) {
                        break;
                    }
                    vTableBase = vTable.ValueOrDefault(vTypeBase);
                    if (vTableBase != null) {
                        break;
                    }
                }
                if (vTableBase == null) {
                    vTableBase = new MethodDefinition[0];
                }
                var vTypeVTable = new List<MethodDefinition>(vTableBase);
                if (vMethodsByType.Contains(vType)) {
                    foreach (var method in vMethodsByType[vType]) {
                        // Must always be either a new slot or in base type vTable
                        int idx;
                        if (method.IsNewSlot) {
                            idx = vTypeVTable.Count;
                            vTypeVTable.Add(method);
                        } else {
                            idx = Enumerable.Range(0, vTableBase.Length).First(i => vTableBase[i].MethodMatch(method));
                            vTypeVTable[idx] = method;
                        }
                        virtualCalls2.Add(method, idx);
                    }
                }
                if (vTypeVTable.Any()) {
                    vTable.Add(vType, vTypeVTable.ToArray());
                }
            }
            var vTableNamer = new NameGenerator("_");
            var vTableNames = vTable.Keys.ToDictionary(x => x, x => vTableNamer.GetNewName());
            // Create interface tables
            var interfaceNamer = new NameGenerator();
            var interfaceNames = interfaceCalls.Keys.ToDictionary(x => x, x => interfaceNamer.GetNewName());
            var interfaceMethods = interfaceCalls.Select(x => x.Value.Select((m, i) => new { m, i }))
                .SelectMany(x => x).ToDictionary(x => x.m, x => x.i);
            // Key is each class, value: key=interface type; value=list of methods that implement each interface method
            var interfaceCallsNames = new Dictionary<TypeDefinition, Dictionary<TypeDefinition, Tuple<string, IEnumerable<MethodDefinition>>>>();
            foreach (var type in allTypesInBaseOrder) {
                var iTypes = type.GetAllInterfaces();
                foreach (var iType in iTypes) {
                    var iMethods = interfaceCalls.ValueOrDefault(iType);
                    if (iMethods != null) {
                        var map = new MethodDefinition[iMethods.Count];
                        foreach (var iMethod in iMethods) {
                            var implMethod = type.GetInterfaceMethod(iMethod);
                            map[interfaceMethods[iMethod]] = implMethod;
                        }
                        interfaceCallsNames.ValueOrDefault(type, () => new Dictionary<TypeDefinition, Tuple<string, IEnumerable<MethodDefinition>>>(), true)
                            .Add(iType, Tuple.Create(vTableNamer.GetNewName(), (IEnumerable<MethodDefinition>)map));
                    }
                }
            }
            var icn = interfaceCallsNames.ToDictionary(x => x.Key, x => x.Value.ToDictionary(y => y.Key, y => y.Value.Item1));

            var js = new StringBuilder();
            // Declare static fields
            var staticFieldInits = fieldNames.Where(x => x.Key.IsStatic)
                .Select(x => x.Value + " = " + DefaultValuer.Get(x.Key.FieldType)).ToArray();
            if (staticFieldInits.Any()) {
                js.AppendFormat("var {0};", string.Join(", ", staticFieldInits));
                js.AppendLine();
                js.AppendLine();
            }

            // Create JS for all methods
            var resolver = new JsMethod.Resolver(methodNames, fieldNames, vTableNames, virtualCalls2, callResolvers,
                cctorCalls, interfaceNames, interfaceMethods, icn);
            foreach (var kv in asts) {
                var method = kv.Key;
                var ast = kv.Value;
                var s = JsMethod.Create(method, resolver, ast);
                js.Append(s);
                js.AppendLine();
            }

            // vTables
            foreach (var vT in vTable) {
                var contents = string.Join(", ", vT.Value.Select(x => methodNames.ValueOrDefault(x, () => "0")));
                js.AppendFormat("var {0} = [{1}];", vTableNames[vT.Key], contents);
                js.AppendLine();
            }

            // Interface calls
            foreach (var iT in interfaceCallsNames) {
                foreach (var i2 in iT.Value) {
                    var contents = string.Join(", ", i2.Value.Item2.Select(x => methodNames.ValueOrDefault(x, () => "0")));
                    js.AppendFormat("var {0} = [{1}];", i2.Value.Item1, contents);
                    js.AppendLine();
                }
            }

            // Type definitions
            foreach (var type in typesSeen) {

            }

            // Exports
            foreach (var export in exports) {
                js.AppendFormat("window['{0}'] = {1}", export.Item2, methodNames[export.Item1]);
                js.AppendLine();
            }

            var jsStr = js.ToString();
            return jsStr;
            */
        }

    }
}
