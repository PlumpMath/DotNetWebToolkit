﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using DotNetWebToolkit.Web;
using NUnit.Framework;

namespace Test.BrowserTests {

    [TestFixture]
    public class TestXmlHttpRequest : BrowserTestBase {

        [Test]
        public void TestSendRecvString() {
            this.SetUrl("/xhr", () => "abc");
            Action f = () => {
                var xhr = new XMLHttpRequest();
                xhr.Open("GET", "xhr");
                xhr.OnReadyStateChange = () => {
                    if (xhr.ReadyState == XMLHttpRequestReadyState.Done) {
                        Done(xhr.ResponseText == "abc");
                    }
                };
                xhr.Send();
            };
            this.Start(f);
        }

        class RecvCustomObject {
            public class Inner {
                public string S { get; set; }
            }
            public RecvCustomObject(int i, string s, char c, bool b) {
                this.I = i;
                this.S = s;
                this.C = c;
                this.Cnull = '\0';
                this.B = b;
                this.Inull = null;
                this.In = new Inner();
                this.In.S = "inner";
                this.InNull = null;
            }
            public int I { get; private set; }
            public string S { get; private set; }
            public char C { get; private set; }
            public char Cnull { get; private set; }
            public bool B { get; private set; }
            public int? Inull { get; private set; }
            public Inner In { get; private set; }
            public Inner InNull { get; private set; }
        }

        [Test]
        public void TestRecvCustomObject() {
            var toSend = new RecvCustomObject(1, "abc", 'x', true);
            var jsoner = new JavaScriptSerializer();
            var toSendJson = jsoner.Serialize(toSend);
            this.SetUrl("/xhr", () => toSendJson);
            Action f = () => {
                var xhr = new XMLHttpRequest();
                xhr.Open("GET", "xhr");
                xhr.OnReadyStateChange = () => {
                    if (xhr.ReadyState == XMLHttpRequestReadyState.Done) {
                        var r = xhr.RecvJson<RecvCustomObject>();
                        Done(r.I == 1 && r.S == "abc" && r.C == 'x' && r.Cnull == '\0' && r.B == true && r.Inull == null && r.In.S == "inner" && r.InNull == null);
                    }
                };
                xhr.Send();
            };
            this.Start(f);
        }

        [Test]
        public void TestSendCustomObject() {
            this.SetUrl("/xhr", () => "true");
            Action f = () => {
                var xhr = new XMLHttpRequest();
                xhr.Open("POST", "xhr");
                xhr.OnReadyStateChange = () => {
                    if (xhr.ReadyState == XMLHttpRequestReadyState.Done) {
                        var r = xhr.RecvJson<bool>();
                        Done(r);
                    }
                };
                var obj = new RecvCustomObject(2, "two", '2', false);
                xhr.SendJson(obj);
                //xhr.Send();
            };
            this.Start(f);
        }

    }

}
