﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Jint.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Jint.Tests.Test262
{
    public abstract class Test262Test
    {
        private static readonly string[] Sources;

        private static readonly string BasePath;

        private static readonly TimeZoneInfo _pacificTimeZone;

        private static readonly Dictionary<string, string> _skipReasons =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        static Test262Test()
        {
            //NOTE: The Date tests in test262 assume the local timezone is Pacific Standard Time
            _pacificTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time");

            var assemblyPath = new Uri(typeof(Test262Test).GetTypeInfo().Assembly.CodeBase).LocalPath;
            var assemblyDirectory = new FileInfo(assemblyPath).Directory;

            BasePath = assemblyDirectory.Parent.Parent.Parent.FullName;

            string[] files =
            {
                @"harness\sta.js",
                @"harness\assert.js",
                @"harness\propertyHelper.js",
                @"harness\compareArray.js",
                @"harness\decimalToHexString.js",
            };

            Sources = new string[files.Length];
            for (var i = 0; i < files.Length; i++)
            {
                Sources[i] = File.ReadAllText(Path.Combine(BasePath, files[i]));
            }

            var content = File.ReadAllText(Path.Combine(BasePath, "test/skipped.json"));
            var doc = JArray.Parse(content);
            foreach (var entry in doc.Values<JObject>())
            {
                _skipReasons[entry["source"].Value<string>()] = entry["reason"].Value<string>();
            }
        }

        protected void RunTestCode(string code, bool strict)
        {
            var engine = new Engine(cfg => cfg
                .LocalTimeZone(_pacificTimeZone)
                .Strict(strict));

            for (int i = 0; i < Sources.Length; ++i)
            {
                engine.Execute(Sources[i]);
            }

            string lastError = null;
            try
            {
                engine.Execute(code);
            }
            catch (JavaScriptException j)
            {
                lastError = TypeConverter.ToString(j.Error);
            }
            catch (Exception e)
            {
                lastError = e.ToString();
            }

            Assert.Null(lastError);
        }

        protected void RunTestInternal(SourceFile sourceFile)
        {
            var fullName = sourceFile.FullPath;
            if (!File.Exists(fullName))
            {
                throw new ArgumentException("Could not find source file: " + fullName);
            }

            string code = File.ReadAllText(fullName);
            RunTestCode(code);
        }

        private void RunTestCode(string code)
        {
            if (code.IndexOf("onlyStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(code, strict: false);
            }

            if (code.IndexOf("noStrict", StringComparison.Ordinal) < 0)
            {
                RunTestCode(code, strict: true);
            }
        }

        public static IEnumerable<object[]> SourceFiles(string pathPrefix, bool skipped)
        {
            var results = new List<object[]>();
            var fixturesPath = Path.Combine(BasePath, "test");
            var searchPath = Path.Combine(fixturesPath, pathPrefix);
            var files = Directory.GetFiles(searchPath, "*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var name = file.Substring(fixturesPath.Length + 1).Replace("\\", "/");
                bool skip = _skipReasons.TryGetValue(name, out var reason);

                var sourceFile = new SourceFile(
                    name,
                    file,
                    skip,
                    reason);

                if (skipped == sourceFile.Skip)
                {
                    results.Add(new object[]
                    {
                        sourceFile
                    });
                }
            }

            return results;
        }
    }

    public class SourceFile
    {
        public SourceFile(
            string source,
            string fullPath,
            bool skip,
            string reason)
        {
            Skip = skip;
            Source = source;
            Reason = reason;
            FullPath = fullPath;
        }

        public string Source { get; }
        public bool Skip { get; }
        public string Reason { get; }
        public string FullPath { get; }

        public override string ToString()
        {
            return Source;
        }
    }

}