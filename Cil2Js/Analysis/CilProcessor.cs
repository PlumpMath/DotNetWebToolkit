﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Cil2Js.Ast;
using Mono.Cecil.Cil;
using Cil2Js.Utils;

namespace Cil2Js.Analysis {
    class CilProcessor {

        public CilProcessor(Ctx ctx, Stack<Expr> stack, Expr[] locals, Expr[] args, Dictionary<Instruction, ExprVarInstResult> instResults) {
            this.ctx = ctx;
            this.stack = stack;
            this.locals = locals;
            this.args = args;
            this.instResults = instResults;
        }

        private Ctx ctx;
        private Stack<Expr> stack;
        private Expr[] locals, args;
        private Dictionary<Instruction, ExprVarInstResult> instResults;

        public Stmt Process(Instruction inst) {
            switch (inst.OpCode.Code) {
            case Code.Nop:
                return null;
            case Code.Ldc_I4_M1:
                return this.Const(-1, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_0:
                return this.Const(0, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_1:
                return this.Const(1, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_2:
                return this.Const(2, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_3:
                return this.Const(3, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_4:
                return this.Const(4, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_5:
                return this.Const(5, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_6:
                return this.Const(6, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_7:
                return this.Const(7, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_8:
                return this.Const(8, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4_S:
                return this.Const((int)(sbyte)inst.Operand, this.ctx.TypeSystem.Int32);
            case Code.Ldc_I4:
                return this.Const((int)inst.Operand, this.ctx.TypeSystem.Int32);
            case Code.Ldnull:
                return this.Const(null, this.ctx.TypeSystem.Object);
            case Code.Ldstr:
                return this.Const((string)inst.Operand, this.ctx.TypeSystem.String);
            case Code.Ldarg_0:
                return this.LdArg(0);
            case Code.Ldarg_1:
                return this.LdArg(1);
            case Code.Ldarg_2:
                return this.LdArg(2);
            case Code.Ldarg_3:
                return this.LdArg(3);
            case Code.Ldarg:
            case Code.Ldarg_S:
                return this.LdArg(((ParameterDefinition)inst.Operand).Index);
            case Code.Starg_S:
                return this.StArg(((ParameterDefinition)inst.Operand).Index);
            case Code.Ldloc_0:
                return this.LdLoc(0);
            case Code.Ldloc_1:
                return this.LdLoc(1);
            case Code.Ldloc_2:
                return this.LdLoc(2);
            case Code.Ldloc_3:
                return this.LdLoc(3);
            case Code.Ldloc_S:
                return this.LdLoc(((VariableDefinition)inst.Operand).Index);
            case Code.Stloc_0:
                return this.StLoc(0);
            case Code.Stloc_1:
                return this.StLoc(1);
            case Code.Stloc_2:
                return this.StLoc(2);
            case Code.Stloc_3:
                return this.StLoc(3);
            case Code.Stloc_S:
                return this.StLoc(((VariableDefinition)inst.Operand).Index);
            case Code.Add:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.Add));
            case Code.Sub:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.Sub));
            case Code.Mul:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.Mul));
            case Code.Div:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.Div));
            case Code.And:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.BitwiseAnd));
            case Code.Or:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.BitwiseOr));
            case Code.Xor:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.BitwiseXor));
            case Code.Ceq:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.Equal, this.ctx.TypeSystem.Boolean));
            case Code.Clt:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.LessThan, this.ctx.TypeSystem.Boolean));
            case Code.Cgt:
                return this.SsaLocalAssignment(this.Binary(BinaryOp.GreaterThan, this.ctx.TypeSystem.Boolean));
            case Code.Br_S:
            case Code.Br:
            case Code.Leave_S:
            case Code.Leave:
            case Code.Endfinally:
                return null;
            case Code.Brtrue_S:
            case Code.Brtrue:
                return this.SsaInstResultAssignment(inst, this.stack.Pop());
            case Code.Brfalse_S:
            case Code.Brfalse:
                return this.SsaInstResultAssignment(inst, this.Unary(UnaryOp.Not));
            case Code.Beq_S:
            case Code.Beq:
                return this.SsaInstResultAssignment(inst, this.Binary(BinaryOp.Equal));
            case Code.Bne_Un_S:
            case Code.Bne_Un:
                return this.SsaInstResultAssignment(inst, this.Binary(BinaryOp.NotEqual));
            case Code.Blt_S:
            case Code.Blt:
                return this.SsaInstResultAssignment(inst, this.Binary(BinaryOp.LessThan));
            case Code.Ble_S:
            case Code.Ble:
                return this.SsaInstResultAssignment(inst, this.Binary(BinaryOp.LessThanOrEqual));
            case Code.Bgt_S:
            case Code.Bgt:
                return this.SsaInstResultAssignment(inst, this.Binary(BinaryOp.GreaterThan));
            case Code.Bge_S:
            case Code.Bge:
                return this.SsaInstResultAssignment(inst, this.Binary(BinaryOp.GreaterThanOrEqual));
            case Code.Pop:
                this.stack.Pop(); return null;
            case Code.Callvirt:
                return this.Call(inst, true);
            case Code.Call:
                return this.Call(inst, false);
            case Code.Newobj:
                return this.NewObj(inst);
            case Code.Newarr:
                return this.NewArray(inst);
            case Code.Ldlen:
                return this.LoadArrayLength();
            case Code.Ldfld:
                return this.LoadField(inst);
            case Code.Stfld:
                return this.StoreField(inst);
            case Code.Ldsfld:
                return this.LoadStaticField(inst);
            case Code.Stsfld:
                return this.StoreStaticField(inst);
            case Code.Ldelem_I4:
                return this.LoadElem(inst);
            case Code.Stelem_I4:
            case Code.Stelem_Ref:
                return this.StoreElem(inst);
            case Code.Conv_I4:
            case Code.Conv_I:
                return null;
            case Code.Throw:
                return new StmtThrow(this.ctx, this.stack.Pop());
            case Code.Ret:
                //return new StmtReturn(this.method.ReturnType.IsVoid() ? null : this.CastIfRequired(this.stack.Pop(), this.method.ReturnType));
                throw new InvalidOperationException("Should not see this here: " + inst);
            default:
                throw new NotImplementedException("Cannot handle: " + inst.OpCode);
            }
        }

        public StmtReturn ProcessReturn() {
            switch (this.stack.Count) {
            case 0:
                return new StmtReturn(this.ctx, null);
            case 1:
                return new StmtReturn(this.ctx, this.CastIfRequired(this.stack.Pop(), this.ctx.Method.ReturnType));
            default:
                throw new InvalidOperationException("Stack size incorrect for return instruction: " + this.stack.Count);
            }
        }

        private Stmt Const(object value, TypeReference type) {
            return this.SsaLocalAssignment(new ExprLiteral(this.ctx, value, type.Resolve()));
        }

        private Stmt LdArg(int idx) {
            Expr expr;
            if (this.ctx.Method.HasThis) {
                if (idx == 0) {
                    expr = new ExprThis(this.ctx, this.ctx.Method.DeclaringType);
                } else {
                    expr = this.args[idx - 1];
                }
            } else {
                expr = this.args[idx];
            }
            return this.SsaLocalAssignment(expr);
        }

        private Stmt StArg(int idx) {
            var expr = this.stack.Pop();
            var target = new ExprVarLocal(this.ctx, expr.Type);
            var assignment = new StmtAssignment(this.ctx, target, expr);
            this.args[idx] = target;
            return assignment;
        }

        private Stmt LdLoc(int idx) {
            return this.SsaLocalAssignment(this.locals[idx]);
        }

        private Stmt StLoc(int idx) {
            var expr = this.stack.Pop();
            var target = new ExprVarLocal(this.ctx, expr.Type);
            var assignment = new StmtAssignment(this.ctx, target, expr);
            this.locals[idx] = target;
            return assignment;
        }

        private ExprUnary Unary(UnaryOp op, TypeReference type = null) {
            var e = this.stack.Pop();
            return new ExprUnary(this.ctx, op, (type ?? e.Type).Resolve(), e);
        }

        private ExprBinary Binary(BinaryOp op, TypeReference type = null) {
            var right = this.stack.Pop();
            var left = this.stack.Pop();
            return new ExprBinary(this.ctx, op, (type ?? left.Type).Resolve(), left, right);
        }

        private Stmt SsaLocalAssignment(Expr expr) {
            var target = new ExprVarLocal(this.ctx, expr.Type);
            var assignment = new StmtAssignment(this.ctx, target, expr);
            this.stack.Push(target);
            return assignment;
        }

        private Stmt SsaInstResultAssignment(Instruction inst, Expr expr) {
            var target = this.instResults[inst];
            var assignment = new StmtAssignment(this.ctx, target, expr);
            return assignment;
        }

        private Expr CastIfRequired(Expr expr, TypeReference requireType) {
            if (expr.Type.IsAssignableTo(requireType)) {
                return expr;
            } else {
                return new ExprCast(this.ctx, expr, requireType);
            }
        }

        private Stmt Call(Instruction inst, bool isVirtual) {
            var calling = ((MethodReference)inst.Operand).Resolve();
            var args = new List<Expr>();
            for (int i = 0; i < calling.Parameters.Count; i++) {
                var expr = this.stack.Pop();
                args.Add(this.CastIfRequired(expr, calling.Parameters[calling.Parameters.Count - 1 - i].ParameterType));
            }
            args.Reverse();
            var obj = calling.IsStatic ? null : this.CastIfRequired(this.stack.Pop(), calling.DeclaringType);
            var exprCall = new ExprCall(this.ctx, calling, obj, args, isVirtual);
            if (calling.ReturnType.IsVoid()) {
                return new StmtWrapExpr(this.ctx, exprCall);
            } else {
                return this.SsaLocalAssignment(exprCall);
            }
        }

        private Stmt NewObj(Instruction inst) {
            var ctor = ((MethodReference)inst.Operand).Resolve();
            var args = new List<Expr>();
            for (int i = 0; i < ctor.Parameters.Count; i++) {
                var expr = this.stack.Pop();
                args.Add(expr);
            }
            args.Reverse();
            return this.SsaLocalAssignment(new ExprNewObj(this.ctx, ctor, args));
        }

        private Stmt NewArray(Instruction inst) {
            var exprNumElements = this.stack.Pop();
            var expr = new ExprNewArray(this.ctx, (TypeReference)inst.Operand, exprNumElements);
            return this.SsaLocalAssignment(expr);
        }

        private Stmt LoadField(Instruction inst) {
            var obj = this.stack.Pop();
            var expr = new ExprFieldAccess(this.ctx, obj, ((FieldReference)inst.Operand).Resolve());
            return this.SsaLocalAssignment(expr);
        }

        private Stmt StoreField(Instruction inst) {
            var value = this.stack.Pop();
            var obj = this.stack.Pop();
            var expr = new ExprFieldAccess(this.ctx, obj, ((FieldReference)inst.Operand).Resolve());
            return new StmtAssignment(this.ctx, expr, value);
        }

        private Stmt LoadStaticField(Instruction inst) {
            var expr = new ExprFieldAccess(this.ctx, null, ((FieldReference)inst.Operand).Resolve());
            return this.SsaLocalAssignment(expr);
        }

        private Stmt StoreStaticField(Instruction inst) {
            var value = this.stack.Pop();
            var expr = new ExprFieldAccess(this.ctx, null, ((FieldReference)inst.Operand).Resolve());
            return new StmtAssignment(this.ctx, expr, value);
        }

        private Stmt LoadElem(Instruction inst) {
            var index = this.stack.Pop();
            var array = this.stack.Pop();
            var arrayAccess = new ExprVarArrayAccess(this.ctx, array, index);
            return this.SsaLocalAssignment(arrayAccess);
        }

        private Stmt StoreElem(Instruction inst) {
            var value = this.stack.Pop();
            var index = this.stack.Pop();
            var array = this.stack.Pop();
            var arrayAccess = new ExprVarArrayAccess(this.ctx, array, index);
            var assignment = new StmtAssignment(this.ctx, arrayAccess, value);
            return assignment;
        }

        private Stmt LoadArrayLength() {
            var array = this.stack.Pop();
            var expr = new ExprArrayLength(this.ctx, array);
            return this.SsaLocalAssignment(expr);
        }

    }
}
