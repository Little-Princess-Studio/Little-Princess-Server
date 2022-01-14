using System.Reflection;
using LPS.Core.Debug;

namespace LPS.Client
{
    public static class CommandParser
    {
        private static Dictionary<string, Tuple<MethodInfo, string[]>> CmdMapping = new();

        public static void ScanCommands(string @namespace)
        {
            var cmdMethods = Assembly.GetEntryAssembly()!.GetTypes()
                .Select(type => type.GetMethods()
                    .Where(method => method.IsStatic && Attribute.IsDefined(method, typeof(ConsoleCommandAttribute))))
                .SelectMany(method => method);

            foreach (var method in cmdMethods)
            {
                var cmdAttr = method.GetCustomAttribute<ConsoleCommandAttribute>()!;
                var args = method.GetParameters()
                    .Select(param => $"({param.ParameterType.Name}){param.Name}")
                    .ToArray();
                
                CmdMapping[cmdAttr.Name] = Tuple.Create(method, args);
                
                Logger.Debug($"Register: {cmdAttr.Name} => {method.Name}({string.Join(',', args)})");
            }
        }
    }    
}
