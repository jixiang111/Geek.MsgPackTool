// Copyright (c) All contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Locator;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace MessagePack.Generator
{
    public class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var instance = MSBuildLocator.RegisterDefaults();

            Console.WriteLine("Geek.MsgPackTool start....");

            //初始化配置信息
            if (!Setting.Init())
            {
                Console.WriteLine("----配置错误，启动失败----");
                return 1;
            }

            if (args.Length > 0)
            {
                if (IsHelp(args[0]))
                {
                    PrintUsage();
                    return 0;
                }

                if (TryParseModel(args[0], out var model))
                {
                    return await Gen(model) ? 0 : 1;
                }

                PrintUsage();
                return 1;
            }

            while (true)
            {
                Console.WriteLine("请按需输入指令:");
                Console.WriteLine("1.导出服务器");
                Console.WriteLine("2.导出客户端");
                Console.WriteLine("3.导出服务器+客户端");
                Console.WriteLine("4.导出TS");

                var key = Console.ReadKey().KeyChar;
                Console.WriteLine("你输入了:" + key.ToString());
                Task<bool>? task = null;
                switch (key)
                {
                    case '1':
                        task = Gen(1);
                        break;
                    case '2':
                        task = Gen(2);
                        break;
                    case '3':
                        task = Gen(3);
                        break;
                    case '4':
                        task = Gen(4);
                        break;
                    default:
                        Console.WriteLine("输入指令错误");
                        break;
                }
                task?.Wait();
            }
        }

        static bool IsHelp(string value)
        {
            value = value.Trim().ToLowerInvariant();
            return value == "-h" || value == "--help" || value == "help";
        }

        static bool TryParseModel(string value, out int model)
        {
            model = 0;
            value = value.Trim().ToLowerInvariant();

            if (value.StartsWith("mode="))
            {
                value = value.Substring("mode=".Length);
            }

            switch (value)
            {
                case "1":
                case "server":
                    model = 1;
                    return true;
                case "2":
                case "client":
                    model = 2;
                    return true;
                case "3":
                case "all":
                case "both":
                case "client+server":
                case "server+client":
                    model = 3;
                    return true;
                case "4":
                case "ts":
                    model = 4;
                    return true;
                default:
                    return false;
            }
        }

        static void PrintUsage()
        {
            Console.WriteLine("用法：");
            Console.WriteLine("  MessagePack.Generator.exe 1|server          导出服务器协议代码");
            Console.WriteLine("  MessagePack.Generator.exe 2|client          导出客户端协议代码");
            Console.WriteLine("  MessagePack.Generator.exe 3|all             导出服务器+客户端协议代码");
            Console.WriteLine("  MessagePack.Generator.exe 4|ts              导出TS协议代码");
            Console.WriteLine("  MessagePack.Generator.exe mode=3            等价于 3");
            Console.WriteLine("无参数启动时保留交互菜单。");
        }

        static async Task<bool> Gen(int model)
        {
            MpcArgument mpcArgument = new MpcArgument();
            mpcArgument.Input = Setting.Ins.ProjectPath;
            mpcArgument.GeneratedFirst = Setting.Ins.GeneratedFirst;
            mpcArgument.BaseMessageName = Setting.Ins.BaseMessageName;
            mpcArgument.NoExportTypes = Setting.Ins.NoExportList;
            if (model == 1)
            {
                mpcArgument.ServerOutput = Setting.Ins.ServerOutPath;
                mpcArgument.ClientOutput = "no";
            }
            else if (model == 2)
            {
                mpcArgument.ServerOutput = "no";
                mpcArgument.ClientOutput = Setting.Ins.ClientOutPath;
            }
            else if (model == 3)
            {
                mpcArgument.ServerOutput = Setting.Ins.ServerOutPath;
                mpcArgument.ClientOutput = Setting.Ins.ClientOutPath;
            }
            else if (model == 4)
            {
                mpcArgument.targetLangType = TargetLanguageType.TS;
                mpcArgument.TSOutput = Setting.Ins.TSOutPath;
            }
            else
            {
                Console.WriteLine("输入指令错误");
                return false;
            }

            return await RunAsync(mpcArgument);
        }

        public static async Task<bool> RunAsync(MpcArgument args)
        {
            if (args.targetLangType == TargetLanguageType.CS)
            {
                MessagePackCompiler.CodeGenerator.CS.InnerGenerator.BaseMessage = args.BaseMessageName;
                MessagePackCompiler.CodeGenerator.CS.InnerGenerator.NoExportTypes = new List<string>();
                if (args.NoExportTypes != null)
                    MessagePackCompiler.CodeGenerator.CS.InnerGenerator.NoExportTypes.AddRange(args.NoExportTypes);
            }
            if (args.targetLangType == TargetLanguageType.TS)
            {
                MessagePackCompiler.CodeGenerator.TS.InnerGenerator.BaseMessage = args.BaseMessageName;
                MessagePackCompiler.CodeGenerator.TS.InnerGenerator.NoExportTypes = new List<string>();
                if (args.NoExportTypes != null)
                    MessagePackCompiler.CodeGenerator.TS.InnerGenerator.NoExportTypes.AddRange(args.NoExportTypes);
            }

            Workspace? workspace = null;
            try
            {
                Compilation compilation;
                if (Directory.Exists(args.Input))
                {
                    string[]? conditionalSymbols = args.ConditionalSymbol?.Split(',');
                    compilation = await PseudoCompilation.CreateFromDirectoryAsync(args.Input, conditionalSymbols, CancellationToken.None);
                }
                else
                {
                    (workspace, compilation) = await OpenMSBuildProjectAsync(args.Input, CancellationToken.None);
                }
                if (args.targetLangType == TargetLanguageType.CS)
                {
                    await new MessagePackCompiler.CodeGenerator.CS.CodeGenerator(x => Console.WriteLine(x), CancellationToken.None)
                        .GenerateFileAsync(
                            compilation,
                            args.ClientOutput,
                            args.ServerOutput,
                            args.GeneratedFirst,
                            args.ResolverName,
                            args.Namespace,
                            args.UseMapMode,
                            args.MultipleIfDirectiveOutputSymbols,
                            null).ConfigureAwait(false);
                }
                else if (args.targetLangType == TargetLanguageType.TS)
                {
                    await new MessagePackCompiler.CodeGenerator.TS.CodeGenerator(x => Console.WriteLine(x), CancellationToken.None)
                        .GenerateFileAsync(
                            compilation,
                            args.TSOutput,
                            args.Namespace,
                            args.UseMapMode,
                            args.MultipleIfDirectiveOutputSymbols,
                            null).ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine("error:" + e.Message);
                return false;
            }
            finally
            {
                //   MSBuildLocator.Unregister();
            }
        }

        static private async Task<(Workspace Workspace, Compilation Compilation)> OpenMSBuildProjectAsync(string projectPath, CancellationToken cancellationToken)
        {
            var workspace = MSBuildWorkspace.Create();
            try
            {
                var logger = new ConsoleLogger(Microsoft.Build.Framework.LoggerVerbosity.Quiet);
                var project = await workspace.OpenProjectAsync(projectPath, logger, null, cancellationToken);
                var compilation = await project.GetCompilationAsync(cancellationToken);


                if (compilation is null)
                {
                    throw new NotSupportedException("The project does not support creating Compilation.");
                }

                return (workspace, compilation);
            }
            catch
            {
                workspace.Dispose();
                throw;
            }
        }
    }
}
