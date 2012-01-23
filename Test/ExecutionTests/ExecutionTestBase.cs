﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.Reflection;
using NUnit.Framework;
using DotNetWebToolkit;
using DotNetWebToolkit.Cil2Js.Utils;
using DotNetWebToolkit.Cil2Js.Output;
using Test.Utils;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium.Chrome;
using System.Threading;
using NUnit.Framework.Constraints;

namespace Test.ExecutionTests {
    public class ExecutionTestBase {

        private const int defaultTestIterations = 20;
        private Random rnd = new Random(0);

        public bool Verbose = false;

        class DefaultParamGen : ParamAttribute {

            public override bool GenBool(Random rnd) {
                return rnd.Next(2) == 1;
            }

            public override int GenInt32(Random rnd) {
                return rnd.Next(0, 100);
            }

            public override double GenDouble(Random rnd) {
                return rnd.NextDouble() * 100.0;
            }

            public override string GenString(Random rnd) {
                int length = rnd.Next(10);
                string s = "";
                for (int i = 0; i < length; i++) {
                    s += (char)(65 + rnd.Next(26));
                }
                return s;
            }

            public override char GenChar(Random rnd) {
                var v = rnd.Next(32, 0x7fff);
                return (char)v;
            }

        }

        private readonly DefaultParamGen defaultParamGen = new DefaultParamGen();

        private object[] CreateArgs(MethodInfo methodInfo) {
            List<object> args = new List<object>();
            var parameters = methodInfo.GetParameters();
            foreach (var arg in parameters) {
                object v;
                var paramGen = (ParamAttribute)arg.GetCustomAttributes(typeof(ParamAttribute), false).FirstOrDefault() ?? defaultParamGen;
                var typeCode = Type.GetTypeCode(arg.ParameterType);
                switch (typeCode) {
                case TypeCode.Boolean:
                    v = paramGen.GenBool(this.rnd);
                    break;
                case TypeCode.Int32:
                    v = paramGen.GenInt32(this.rnd);
                    break;
                case TypeCode.Double:
                    v = paramGen.GenDouble(this.rnd);
                    break;
                case TypeCode.String:
                    v = paramGen.GenString(this.rnd);
                    break;
                case TypeCode.Char:
                    v = paramGen.GenChar(this.rnd);
                    break;
                default:
                    throw new NotImplementedException("Cannot handle: " + typeCode);
                }
                args.Add(v);
            }
            return args.ToArray();
        }

        private string ConvertArgToJavascript(object arg) {
            if (arg == null) {
                return "null";
            }
            var tc = Type.GetTypeCode(arg.GetType());
            switch (tc) {
            case TypeCode.Boolean:
                return (bool)arg ? "true" : "false";
            case TypeCode.Int32:
            case TypeCode.Double:
                return arg.ToString();
            case TypeCode.String:
                return "\"" + arg.ToString() + "\"";
            case TypeCode.Char:
                return ((int)(char)arg).ToString();
            default:
                throw new NotImplementedException("Cannot convert: " + tc);
            }
        }

        protected void Test(params Delegate[] ds) {
            foreach (var d in ds) {
                Test(d);
            }
        }

        protected void Test(Delegate d) {
            var mi = d.Method;
            var method = CecilHelper.GetMethod(d);
            var js = Js.CreateFrom(method, this.Verbose);
            if (this.Verbose) {
                Console.WriteLine(js);
            }
            var withinAttr = (WithinAttribute)mi.GetCustomAttributes(typeof(WithinAttribute), false).FirstOrDefault();
            var icAttr = (IterationCountAttribute)mi.GetCustomAttributes(typeof(IterationCountAttribute), false).FirstOrDefault();
            int iterationCount;
            if (icAttr != null) {
                iterationCount = icAttr.IterationCount;
            } else {
                iterationCount = method.Parameters.Any() ? defaultTestIterations : 1;
            }
            var range = Enumerable.Range(0, iterationCount);
            var args = range.Select(i => this.CreateArgs(mi)).ToArray();

            var runResults = range.Select(i => {
                object r = null;
                Exception e = null;
                try {
                    r = d.DynamicInvoke(args[i]);
                } catch (TargetInvocationException ex) {
                    e = ex.InnerException;
                }
                return Tuple.Create(r, e);
            }).ToArray();

            using (var chrome = NamespaceSetup.ChromeService != null ?
                new RemoteWebDriver(NamespaceSetup.ChromeService.ServiceUrl, DesiredCapabilities.Chrome()) :
                new ChromeDriver()) {
                try {
                    for (int i = 0; i < args.Length; i++) {
                        var arg = args[i];
                        if (!mi.IsStatic) {
                            arg = arg.Prepend(null).ToArray();
                        }
                        var jsCall = string.Format("return main({0});", string.Join(", ", arg.Select(x => this.ConvertArgToJavascript(x))));
                        var jsResult = chrome.ExecuteScript(js + jsCall);
                        if (jsResult != null && jsResult.GetType() != d.Method.ReturnType) {
                            jsResult = Convert.ChangeType(jsResult, d.Method.ReturnType);
                        }
                        EqualConstraint equalTo = Is.EqualTo(runResults[i].Item1);
                        IResolveConstraint expected = equalTo;
                        if (withinAttr != null) {
                            expected = equalTo.Within(withinAttr.Delta);
                        }
                        Assert.That(jsResult, expected);
                    }
                } finally {
                    chrome.Quit();
                }
            }

        }

    }

    [SetUpFixture]
    public class NamespaceSetup {

        public static ChromeDriverService ChromeService;

        [SetUp]
        public void Setup() {
            ChromeService = ChromeDriverService.CreateDefaultService();
            ChromeService.Start();

        }

        [TearDown]
        public void Teardown() {
            ChromeService.Dispose();
            ChromeService = null;
        }

    }

}
