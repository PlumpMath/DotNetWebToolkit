﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cil2Js.Ast;
using Cil2Js.Utils;

namespace Cil2Js.Analysis {
    public class VisitorDerecurse : AstRecursiveVisitor {

        public static ICode V(ICode ast) {
            var v = new VisitorDerecurse();
            return v.Visit(ast);
        }

        private VisitorDerecurse() { }

        private Dictionary<ICode, ICode> replaces = new Dictionary<ICode, ICode>();

        protected override ICode VisitStmt(Stmt s) {
            var r = this.replaces.ValueOrDefault(s);
            if (r != null) {
                this.map.Add(s, r);
                return r;
            }
            return base.VisitStmt(s);
        }

        protected override ICode VisitContinuation(StmtContinuation s) {
            if (s.To.StmtType != Stmt.NodeType.Block) {
                return base.VisitContinuation(s);
            }
            var block = (StmtBlock)s.To;
            foreach (var stmt in block.Statements) {
                if (stmt.StmtType == Stmt.NodeType.If) {
                    var sIf = (StmtIf)stmt;
                    if (sIf.Else == null && sIf.Then.StmtType == Stmt.NodeType.Continuation) {
                        var sThen = (StmtContinuation)sIf.Then;
                        if (sThen.To == s.To) {
                            // Recursive, so derecurse
                            var condition = sIf.Condition;
                            var bodyStmts = block.Statements.TakeWhile(x => x != stmt).ToArray();
                            var bodyLast = bodyStmts.LastOrDefault();
                            var body = new StmtBlock(s.Ctx, bodyStmts);
                            var loop = new StmtDoLoop(s.Ctx, body, condition);
                            var afterLoop = block.Statements.SkipWhile(x => x != stmt).Skip(1).ToArray();
                            Stmt replaceWith;
                            if (afterLoop.Any()) {
                                var loopAndAfter = new[] { loop }.Concat(afterLoop).ToArray();
                                replaceWith = new StmtBlock(s.Ctx, loopAndAfter);
                            } else {
                                replaceWith = loop;
                            }
                            this.replaces.Add(s.To, replaceWith);
                            return base.VisitContinuation(s);
                        }
                    }
                }
                if (VisitorFindContinuations.Any(stmt)) {
                    // Another continuation present, cannot derecurse
                    break;
                }
            }
            return base.VisitContinuation(s);
        }

    }
}
