﻿using Aggregates.Extensions;
using Aggregates.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Aggregates.Internal
{
    public class VersionRegistrar
    {
        private static readonly ILog Logger = LogProvider.GetLogger("VersionRegistrar");

        public class VersionDefinition
        {
            public int Version { get; private set; }
            public string Name { get; private set; }
            public Type Type { get; private set; }
            public VersionDefinition(string name, int version, Type type)
            {
                this.Version = version;
                this.Name = name;
                this.Type = type;
            }
        }


        private static object _sync = new object();
        private static Dictionary<string, List<VersionDefinition>> NameToType = new Dictionary<string, List<VersionDefinition>>();
        private static Dictionary<Type, VersionDefinition> TypeToDefinition = new Dictionary<Type, VersionDefinition>();

        public static void Load(Type[] types)
        {
            lock (_sync)
            {
                foreach (var type in types)
                {
                    var versionInfo = type.GetCustomAttributes().OfType<Versioned>().SingleOrDefault();
                    if (versionInfo == null)
                    {
                        Logger.WarnEvent("ShouldVersion", "{TypeName} needs a [Versioned] attribute", type.FullName);
                        versionInfo = new Versioned(type.Name, 1);
                    }
                    RegisterType(type, versionInfo.Name, versionInfo.Version);
                }
            }
        }

        private static void RegisterType(Type type, string name, int version)
        {
            if (!NameToType.TryGetValue(name, out var list))
                list = new List<VersionDefinition>();

            var definition = new VersionDefinition(name, version, type);
            list.Add(definition);
            NameToType[name] = list;
            TypeToDefinition[type] = definition;
        }

        public static VersionDefinition GetDefinition(Type messageType)
        {
            if (!TypeToDefinition.ContainsKey(messageType))
                Load(new[] { messageType });
            return TypeToDefinition[messageType];
        }
        public static Type GetNamedType(string name, int? version)
        {
            lock (_sync)
            {
                if (!NameToType.TryGetValue(name, out var definitions) || !definitions.Any())
                    return null;
                if (!version.HasValue)
                    return definitions.OrderByDescending(x => x.Version).First().Type;

                return definitions.SingleOrDefault(x => x.Version == version)?.Type;
            }
        }
    }
}