using System.Reflection;
using LPS.Common.Core.Debug;

namespace LPS.Client.Console
{
    public static class CommandParser
    {
        private static Dictionary<string, (MethodInfo, string[])> CmdMapping = new();

        public static void ScanCommands(string @namespace)
        {
            var cmdMethods = Assembly.GetEntryAssembly()!.GetTypes()
                .Select(type => type.GetMethods()
                    .Where(method => method.IsStatic && Attribute.IsDefined(method, typeof(ConsoleCommandAttribute))))
                .SelectMany(method => method);

            foreach (var method in cmdMethods)
            {
                if (!ValidateCmdMethod(method))
                {
                    throw new Exception($"Invalid cmd method: " +
                                        $"{method.Name}" +
                                        $"({string.Join(',', method.GetParameters().Select(param => param.ParameterType.Name))})");
                }
                
                var cmdAttr = method.GetCustomAttribute<ConsoleCommandAttribute>()!;
                var args = method.GetParameters()
                    .Select(param => $"({param.ParameterType.Name}){param.Name}")
                    .ToArray();
                
                CmdMapping[cmdAttr.Name] = (method, args);
                
                Logger.Debug($"Register: {cmdAttr.Name} => {method.Name}({string.Join(',', args)})");
            }
        }

        public static bool ValidateCmdMethod(MethodInfo methodInfo)
        {
            var returnType = methodInfo.ReturnType;
            if (returnType != typeof(void))
            {
                return false;
            }

            return methodInfo.GetParameters().All(param => CheckArgTypeValid(param.ParameterType));
        }

        public static bool CheckArgTypeValid(Type argType)
        {
            return argType == typeof(string)
                   || argType == typeof(int)
                   || argType == typeof(float)
                   || argType == typeof(bool);
        }

        public static (string[] CmdNames, string[] CmdDetails) FindSuggestions(string prefix)
        {
            var cmdNames = CmdMapping
                .Where(kv => kv.Key.StartsWith(prefix))
                .Select(kv => kv.Key)
                .ToArray(); 
            var details = cmdNames
                .Select(cmdName => $"{cmdName}[{string.Join(',', CmdMapping[cmdName].Item2)}]")
                .ToArray();
            
            return (cmdNames, details);
        }

        public static void Dispatch(string commandString)
        {
            var cmdArr = commandString.Split(' ',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            var cmdName = cmdArr[0];

            if (!CmdMapping.ContainsKey(cmdName))
            {
                throw new Exception($"Invalid cmd name {cmdName}");
            }

            var methodInfo = CmdMapping[cmdName].Item1;
            var argTypes = methodInfo.GetParameters().Select(param => param.ParameterType).ToArray();

            var argCnt = argTypes.Length;

            if (argCnt != cmdArr.Length - 1)
            {
                throw new Exception($"Invalid arguments nums {cmdArr.Length - 1} for {argCnt}");
            }

            if (argCnt > 0)
            {
                var cmdArgs = cmdArr[1..];
                var args = cmdArgs
                    .Select((literal, idx) => ConverCommandStringArgToObject(argTypes[idx], literal))
                    .ToArray();
                methodInfo.Invoke(null, args);
            }
            else
            {
                methodInfo.Invoke(null, null);
            }
        }

        private static object ConverCommandStringArgToObject(Type argType, string argLiteral)
        {
            return argType switch
            {
                _ when argType == typeof(string) => argLiteral,
                _ when argType == typeof(bool) => Convert.ToBoolean(argLiteral),
                _ when argType == typeof(int) => Convert.ToInt32(argLiteral),
                _ when argType == typeof(float) => Convert.ToSingle(argLiteral),
                _ => throw new Exception("Invalid ")
            };
        }
    }    
}
