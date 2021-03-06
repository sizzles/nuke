﻿// Copyright Matthias Koch 2017.
// Distributed under the MIT License.
// https://github.com/nuke-build/nuke/blob/master/LICENSE

using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.Git;
using Nuke.Common.Tools.GitLink3;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Tools.InspectCode;
using Nuke.Common.Tools.NuGet;
using Nuke.Common.Tools.OpenCover;
using Nuke.Common.Tools.Xunit2;
using Nuke.Core;
using Nuke.Core.Utilities.Collections;
using static Nuke.Common.Tools.GitLink3.GitLink3Tasks;
using static Nuke.Common.Tools.InspectCode.InspectCodeTasks;
using static Nuke.Common.Tools.MSBuild.MSBuildTasks;
using static Nuke.Common.Tools.NuGet.NuGetTasks;
using static Nuke.Common.Tools.OpenCover.OpenCoverTasks;
using static Nuke.Common.Tools.Xunit2.Xunit2Tasks;
using static Nuke.Core.IO.FileSystemTasks;
using static Nuke.Core.IO.PathConstruction;
using static Nuke.Core.EnvironmentInfo;

class Build : NukeBuild
{
    [Parameter("ApiKey for 'nukebuild' MyGet source.")] readonly string MyGetApiKey;

    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository GitRepository;

    public static int Main () => Execute<Build>(x => x.Pack);

    Target Clean => _ => _
            .Executes(() => DeleteDirectories(GlobDirectories(SourceDirectory, "*/bin", "*/obj")))
            .Executes(() => EnsureCleanDirectory(OutputDirectory));

    Target Restore => _ => _
            .DependsOn(Clean)
            .Executes(() => MSBuild(s => DefaultSettings.MSBuildRestore));

    Target Compile => _ => _
            .DependsOn(Restore)
            .Requires(() => IsUnix || GitVersion != null)
            .Executes(() => MSBuild(s => IsWin
                ? DefaultSettings.MSBuildCompileWithVersion
                : DefaultSettings.MSBuildCompile));

    Target Link => _ => _
            .OnlyWhen(() => false)
            .DependsOn(Compile)
            .Executes(() => GlobFiles(SolutionDirectory, $"*/bin/{Configuration}/*/*.pdb")
                    .Where(x => !x.Contains("ToolGenerator"))
                    .ForEach(x => GitLink3(s => DefaultSettings.GitLink3.SetPdbFile(x))));

    Target Pack => _ => _
            .DependsOn(Restore, Link)
            .Executes(() => MSBuild(s => DefaultSettings.MSBuildPack));

    Target Push => _ => _
            .DependsOn(Pack)
            .Requires(() => MyGetApiKey)
            .Executes(() => GlobFiles (OutputDirectory, "*.nupkg")
                    .Where(x => !x.EndsWith("symbols.nupkg"))
                    .ForEach(x => NuGetPush(s => s
                            .SetTargetPath(x)
                            .SetSource("https://www.myget.org/F/nukebuild/api/v2/package")
                            .SetApiKey(MyGetApiKey)
                            .SetVerbosity(NuGetVerbosity.Detailed))));
    
    Target Analysis => _ => _
            .DependsOn(Restore)
            .Executes(() => InspectCode(s => DefaultSettings.InspectCode
                    .AddExtensions(
                        "EtherealCode.ReSpeller",
                        "PowerToys.CyclomaticComplexity",
                        "ReSharper.ImplicitNullability",
                        "ReSharper.SerializationInspections",
                        "ReSharper.XmlDocInspections")));

    Target Test => _ => _
            .DependsOn(Compile)
            .Executes(() =>
            {
                void TestXunit ()
                    => Xunit2(GlobFiles(SolutionDirectory, $"*/bin/{Configuration}/net4*/Nuke.*.Tests.dll"),
                        s => s.SetResultPath(OutputDirectory / "tests.xml"));

                if (IsWin)
                    OpenCover(TestXunit, s => DefaultSettings.OpenCover
                                .SetOutput(OutputDirectory / "coverage.xml"));
                else
                    TestXunit();
            });

    Target Full => _ => _
            .DependsOn(Compile, Test, Analysis, Push);
}
